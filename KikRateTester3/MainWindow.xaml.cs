using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DankWaifu.Collections;
using DankWaifu.Sys;
using DankWindowsWaifu.WPF;

namespace KikRateTester3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly SettingsDataGrid _settingsDataGrid;

        public MainWindow()
        {
            InitializeComponent();
            SendAccountsUICollection = new ObservableCollection<DataGridItem>();
            ReceiveAccountsUICollection = new ObservableCollection<DataGridItem>();
            SendWorkerMonitor.DataContext = this;
            ReceiveWorkerMonitor.DataContext = this;
            Settings.Load();
            _settingsDataGrid = SettingsDataGrid();

            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";

            SendWorkerMonitor.LoadingRow += (o, args) =>
            {
                args.Row.Header = (args.Row.GetIndex() + 1).ToString();
            };
            ReceiveWorkerMonitor.LoadingRow += (o, args) =>
            {
                args.Row.Header = (args.Row.GetIndex() + 1).ToString();
            };
        }

        public ObservableCollection<DataGridItem> SendAccountsUICollection { get; }
        public ObservableCollection<DataGridItem> ReceiveAccountsUICollection { get; }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            _settingsDataGrid.SavePrimitives();
            Process.GetCurrentProcess().Kill();
        }

        private static ConcurrentQueue<Account> Accounts(ConcurrentQueue<string> input)
        {
            var ret = new ConcurrentQueue<Account>();
            while (input.Count > 0)
            {
                var acct = new Account(input.GetNext(false));
                if (!acct.IsValid)
                    continue;

                ret.Enqueue(acct);
            }

            return ret;
        }

        private async void CmdLaunch_OnClick(object sender, RoutedEventArgs e)
        {
            _settingsDataGrid.SavePrimitives();

            var send = Accounts(_settingsDataGrid.GetConcurrentQueue(Constants.SendAccounts));
            var recv = Accounts(_settingsDataGrid.GetConcurrentQueue(Constants.ReceiveAccounts));
            using (var collections = new Collections(_settingsDataGrid, send, recv))
            {
                try
                {
                    SanityCheck(collections);
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(this, ex.Message, ex.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                var maxWorkers = Settings.Get<int>(Constants.MaxWorkers);
                InitThreadMonitors(maxWorkers);

                await LaunchWorkers(maxWorkers, collections)
                    .ConfigureAwait(false);
            }
        }

        private async Task LaunchWorkers(int maxWorkers, Collections collections)
        {
            CmdLaunch.IsEnabled = false;

            try
            {
                using (var c = new CancellationTokenSource())
                {
                    var stats = new GlobalStats();
                    var statsTask = StatsAsync(c.Token, stats, collections);
                    var tasks = new List<Task>();

                    for (var i = 0; i < maxWorkers; i++)
                    {
                        var cls = new RateTester(i, ReceiveAccountsUICollection[i], SendAccountsUICollection[i], collections, stats);
                        tasks.Add(cls.BaseAsync());
                    }

                    await Task.WhenAll(tasks);
                    await statsTask;
                }
            }
            finally
            {
                CmdLaunch.IsEnabled = true;
            }
        }

        private async Task StatsAsync(CancellationToken c, GlobalStats stats, Collections collections)
        {
            var start = DateTime.Now;
            while (!c.IsCancellationRequested)
            {
                try
                {
                    var runTime = DateTime.Now.Subtract(start);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        Title =
                            $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version} " +
                            $"[{string.Format("{3:D2}:{0:D2}:{1:D2}:{2:D2}", runTime.Hours, runTime.Minutes, runTime.Seconds, runTime.Days)}]";

                        LblRcvAccountsOnline.Content = $"Recv accts online: [{stats.RecvOnline:N0}]";
                        LblSendAccountsOnline.Content = $"Send accts online: [{stats.SendOnline:N0}]";
                        LblUnrated.Content = $"Unrated: [{stats.Unrated:N0}]";
                        LblSendAccountsInQueue.Content = $"Send accounts in queue: [{collections.SendAccounts.Count }]";
                        LblRecvAccountsInQueue.Content = $"Recv accounts in queue: [{collections.ReceiveAccounts.Count}]";
                    });

                    await Task.Delay(950, c)
                        .ConfigureAwait(false);
                }
                catch (TaskCanceledException
#if DEBUG
                t
#endif
                ) {/**/}
            }
        }

        private void InitThreadMonitors(int maxWorkers)
        {
            SendAccountsUICollection.Clear();
            ReceiveAccountsUICollection.Clear();

            for (var i = 0; i < maxWorkers; i++)
            {
                var item = new DataGridItem
                {
                    Account = string.Empty,
                    Status = string.Empty
                };
                SendAccountsUICollection.Add(item);
            }

            for (var i = 0; i < maxWorkers; i++)
            {
                var item = new DataGridItem
                {
                    Account = string.Empty,
                    Status = string.Empty
                };
                ReceiveAccountsUICollection.Add(item);
            }
        }

        private static void SanityCheck(Collections collections)
        {
            if (collections.SendAccounts.Count == 0)
                throw new InvalidOperationException("Send accounts file is empty or has invalid format");

            if (collections.ReceiveAccounts.Count == 0)
                throw new InvalidOperationException("Receive accounts file is empty or has invalid format");

            if (collections.SendProxies.Count == 0)
                throw new InvalidOperationException("Send proxies file is empty");

            if (collections.ReceiveProxies.Count == 0)
                throw new InvalidOperationException("Receive proxies file is empty");
        }

        private SettingsDataGrid SettingsDataGrid()
        {
            var settings = new ObservableCollection<SettingObj>
            {
                new SettingPrimitive<int>("Max workers", Constants.MaxWorkers, 1),
                new SettingPrimitive<int>("Max login errors", Constants.MaxLoginErrors, 3),
                new SettingConcurrentQueue("Send accounts", Constants.SendAccounts),
                new SettingConcurrentQueue("Receive accounts", Constants.ReceiveAccounts),
                new SettingConcurrentQueue("Send proxies", Constants.SendProxies),
                new SettingConcurrentQueue("Receive proxies", Constants.ReceiveProxies),
            };

            var settingsPage = (TabItem)TbMain.Items[1];
            var gridContent = (Grid)settingsPage.Content;
            var ret = new SettingsDataGrid(this, gridContent, settings);
            ret.CreateUi();
            return ret;
        }
    }
}
