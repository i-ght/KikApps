using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using DankLibWaifuz.SettingsWaifu.SettingObjects;
using DankLibWaifuz.SettingsWaifu.SettingObjects.Collections;
using KikRateTester2.Declarations;
using KikRateTester2.Work;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace KikRateTester2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ThreadMonitor.DataContext = this;
            ReceiveThreadMonitor.DataContext = this;
        }

        public ObservableCollection<DataGridItem> ThreadMonitorSource { get; } = new ObservableCollection<DataGridItem>();
        public ObservableCollection<DataGridItem> ReceiveThreadMonitorSource { get; } = new ObservableCollection<DataGridItem>();

        private SettingsUi _settingsWaifu;
        private int _maxWorkers;
        private bool _running;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";

            Settings.Load();
            Blacklists.Load();
            InitSettingsUi();

            ThreadMonitor.LoadingRow += (o, args) =>
            {
                args.Row.Header = (args.Row.GetIndex() + 1).ToString();
            };

            ReceiveThreadMonitor.LoadingRow += (o, args) =>
            {
                args.Row.Header = (args.Row.GetIndex() + 1).ToString();
            };
        }

        private void InitSettingsUi()
        {
            var settingsCollection = new ObservableCollection<SettingObj>
            {
                new SettingPrimitive<int>("Max send account threads", "MaxWorkers", 1),
                new SettingPrimitive<int>("Max login errors", "MaxLoginErrors", 8),
                new SettingPrimitive<int>("Minimum send messsage delay", "MinSendMsgDelay", 10),
                new SettingPrimitive<int>("Maximum send message delay", "MaxSendMsgDelay", 30),
                new SettingPrimitive<int>("Start sending when (x) receive accounts online", "MinReceiveAcctsOnline", 30),
                new SettingQueue("Proxies"),
                new SettingQueue("Receive proxies", "ReceiveProxies"),
                new SettingQueue("Send accounts", "SendAccounts"),
                new SettingQueue("Receive accounts", "ReceiveAccounts"),
                new SettingQueue("Messages to send", "MessagesToSend")
            };

            var settingsPage = (TabItem)TbMain.Items[1];
            var gridContent = (Grid)settingsPage.Content;

            _settingsWaifu = new SettingsUi(this, gridContent, settingsCollection);
            _settingsWaifu.CreateUi();

            Collections.SettingsUi = _settingsWaifu;
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            _settingsWaifu.SavePrimitives();
            Process.GetCurrentProcess().Kill();
        }

        private void InitThreadMonitors(int rcvCnt)
        {
            ThreadMonitorSource.Clear();
            ReceiveThreadMonitorSource.Clear();

            for (var i = 0; i < _maxWorkers; i++)
            {
                var item = new DataGridItem
                {
                    Account = i.ToString(),
                    Status = string.Empty
                };
                ThreadMonitorSource.Add(item);
            }

            for (var i = 0; i < rcvCnt; i++)
            {
                var item = new DataGridItem
                {
                    Account = i.ToString(),
                    Status = string.Empty
                };
                ReceiveThreadMonitorSource.Add(item);
            }
        }

        private async Task DoWork()
        {
            MessageReceiver.Accounts.Clear();
            MessageReceiver.JidsOnline.Clear();
            MessageSender.Accounts.Clear();
            MessageSender.SentMessages.Clear();
            MessageSender.WorkComplete = false;

            string acctStr;
            while (!string.IsNullOrWhiteSpace(acctStr = Collections.SendAccounts.GetNext(false)))
            {
                var acct = new Account(acctStr);
                if (!acct.IsValid)
                    continue;

                if (Blacklists.Dict[BlacklistType.Login].Contains(acct.LoginId))
                    continue;

                MessageSender.Accounts.Enqueue(acct);
            }

            while (!string.IsNullOrWhiteSpace(acctStr = Collections.ReceiveAccounts.GetNext(false)))
            {
                var acct = new Account(acctStr);
                if (!acct.IsValid)
                    continue;

                MessageReceiver.Accounts.Enqueue(acct);
            }

            if (MessageSender.Accounts.Count == 0)
            {
                MessageBox.Show("Invalid send accounts file");
                return;
            }

            if (MessageReceiver.Accounts.Count == 0)
            {
                MessageBox.Show("Invalid receive accounts file");
                return;
            }

            _running = true;
            new Thread(StatsUi).Start();

            var rcvTasks = new List<Task>();
            var cnt = MessageReceiver.Accounts.Count;
            Console.Out.WriteLine("{0} accounts in receive queue", cnt);
            InitThreadMonitors(cnt);

            for (var i = 0; i < cnt; i++)
            {
                var cls = new MessageReceiver(i, ReceiveThreadMonitorSource);
                rcvTasks.Add(cls.Base());
                await Task.Delay(10)
                    .ConfigureAwait(false);
            }

            var sendTasks = new List<Task>();
            for (var i = 0; i < _maxWorkers; i++)
            {
                var cls = new MessageSender(i, ThreadMonitorSource);
                sendTasks.Add(cls.Base());
            }

            await Task.WhenAll(sendTasks);

            MessageSender.WorkComplete = true;

            await Task.WhenAll(rcvTasks);

            MessageSender.SaveRatedAccounts();

            _running = false;
            CmdLaunch.IsEnabled = true;
            MessageBox.Show("Work complete");
        }

        private async void CmdLaunch_OnClick(object sender, RoutedEventArgs e)
        {
            _settingsWaifu.SavePrimitives();

            if (!Collections.IsValid())
            {
                MessageBox.Show("Missing required files");
                return;
            }

            _maxWorkers = Settings.Get<int>("MaxWorkers");

            Collections.Shuffle();

            CmdLaunch.IsEnabled = false;

            await DoWork().ConfigureAwait(false);
        }

        private void StatsUi()
        {
            var start = DateTime.Now;

            while (_running)
            {
                Thread.Sleep(950);

                var runTime = DateTime.Now.Subtract(start);
                Dispatcher.Invoke(delegate
                {
                    Title =
                        $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version} " +
                        $"[{string.Format("{3:D2}:{0:D2}:{1:D2}:{2:D2}", runTime.Hours, runTime.Minutes, runTime.Seconds, runTime.Days)}]";
                    LblUnrated.Content = $"Unrated: [{Stats.Unrated}]";
                    LblSendAcctsInQueue.Content = $"Send accts in queue: [{MessageSender.Accounts.Count.ToString("N0")}]";
                    LblRcvAccountsOnline.Content = $"Rcv accounts online: [{Stats.RcvAccountsOnline.ToString("N0")}]";
                    LblSendAccountsOnline.Content = $"Send accouts online: [{Stats.SendAccountsOnline.ToString("N0")}]";
                });
            }
        }
    }
}
