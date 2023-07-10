using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using DankLibWaifuz.SettingsWaifu.SettingObjects;
using DankLibWaifuz.SettingsWaifu.SettingObjects.Collections;
using KikBot2.Declarations;
using KikBot2.Work;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace KikBot2
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
        }

        private SettingsUi _settingsWaifu;
        private bool _running;
        private int _maxWorkers;

        public ObservableCollection<DataGridItem> ThreadMonitorSource { get; } = new ObservableCollection<DataGridItem>();


        private void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";

            Settings.Load();
            Blacklists.Load();
            InitSettingsUi();

            ThreadMonitor.LoadingRow += (o, args) =>
            {
                args.Row.Header = (args.Row.GetIndex() + 1).ToString();
            };
        }

        private void InitSettingsUi()
        {
            var settings = new ObservableCollection<SettingObj>
            {
                new SettingPrimitive<int>("Max login errors", "MaxLoginErrors", 10),
                new SettingPrimitive<int>("Max keep-alives", "MaxKeepAlives", 3),
                new SettingPrimitive<int>("Keep-alive delay (min)", "KeepAliveDelay", 40),
                new SettingPrimitive<int>("Minimum send message delay (sec)", "MinSendMessageDelay", 20),
                new SettingPrimitive<int>("Maximum send message delay (sec)", "MaxSendMessageDelay", 40),
                new SettingPrimitive<bool>("Use kik browser to share links", "UseKikBrowser", true),
                new SettingPrimitive<bool>("Disable keep-alives?", "DisableKeepAlives", false),
                new SettingPrimitive<bool>("Disable messaging?", "DisableMessaging", false),
                new SettingQueue("Accounts"),
                new SettingQueue("Proxies"),
                new SettingList("Script"),
                new SettingQueue("Links"),
                new SettingQueue("Spoofed link info", "SpoofedLinkInfo"),
                new SettingQueue("Keep-alives", "KeepAlives"),
                new SettingKeywords("Keywords"),
                new SettingList("Restricts"),
                new SettingQueue("Apologies"),
                new SettingPrimitive<string>("Directory of image files", "ImageFilesDir", string.Empty)
            };

            var settingsPage = (TabItem)TbMain.Items[1];
            var gridContent = (Grid)settingsPage.Content;

            _settingsWaifu = new SettingsUi(this, gridContent, settings);
            _settingsWaifu.CreateUi();

            Collections.SettingsUi = _settingsWaifu;
        }

        private void ChkChatLogEnabled_Click(object sender, RoutedEventArgs e)
        {
            var chk = sender as CheckBox;
            if (chk == null)
                return;

            Settings.Save("ChatLogEnabled", chk.IsChecked);
        }

        private void TxtChatLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txt = sender as TextBox;
            if (txt == null)
                return;

            txt.ScrollToEnd();
        }

        private void TxtChatLog_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
                return;

            var txt = sender as TextBox;
            if (txt == null)
                return;

            txt.Focus();
            txt.CaretIndex = txt.Text.Length;
            txt.ScrollToEnd();
        }

        private void CmdClearChatLog_Click(object sender, RoutedEventArgs e)
        {
            TxtChatLog.Clear();
        }

        private void CmdLaunch_Click(object sender, RoutedEventArgs e)
        {
            _settingsWaifu.SavePrimitives();

            if (!Collections.IsValid())
            {
                MessageBox.Show("Missing required files");
                return;
            }

            Collections.Shuffle();

            var accounts = new Queue<Account>();

            string acctStr;
            while (!string.IsNullOrWhiteSpace(acctStr = Collections.Accounts.GetNext(false)))
            {
                var acct = new Account(acctStr);
                if (!acct.IsValid)
                    continue;

                accounts.Enqueue(acct);
            }

            if (accounts.Count == 0)
            {
                MessageBox.Show("Invalid accounts file");
                return;
            }

            CmdLaunch.IsEnabled = false;

            _maxWorkers = accounts.Count;

            InitThreadMonitor();

            DoWork(accounts);
        }

        private void DoWork(Queue<Account> accounts)
        {
            _running = true;

            for (var i = 0; i < _maxWorkers; i++)
            {
                var cls = new Bot(i, ThreadMonitorSource, accounts.GetNext(false), TxtChatLog);
                Maintenance.Bots.Add(cls);
            }

            new Thread(StatsUi).Start();
            new Thread(Maintenance.Base).Start();
        }

        private void InitThreadMonitor()
        {
            ThreadMonitorSource.Clear();

            for (var i = 0; i < _maxWorkers; i++)
            {
                var item = new DataGridItem
                {
                    Account = string.Empty,
                    Status = string.Empty,
                    InCount = 0,
                    OutCount = 0
                };
                ThreadMonitorSource.Add(item);
            }
        }

#if DEBUG
        private void InitThreadMonitorTest()
        {
            ThreadMonitorSource.Clear();

            var r = new Random();
            for (var i = 0; i < 111; i++)
            {
                var item = new DataGridItem
                {
                    Account = string.Empty,
                    Status = string.Empty,
                    InCount = 0,
                    OutCount = r.Next(1, 999)
                };
                ThreadMonitorSource.Add(item);
            }
        }
#endif

        private void StatsUi()
        {
            var start = DateTime.Now;
            while (_running)
            {
                Thread.Sleep(950);

                var runTime = DateTime.Now.Subtract(start);

                Dispatcher.Invoke(() =>
                {
                    Title =
                        $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version} " +
                        $"[{string.Format("{3:D2}:{0:D2}:{1:D2}:{2:D2}", runTime.Hours, runTime.Minutes, runTime.Seconds, runTime.Days)}]";

                    LblOnline.Content = $"Online: [{Maintenance.OnlineCount.ToString("N0")}]";
                    LblConvos.Content = $"Convos: [{Stats.Convos.ToString("N0")}]";
                    LblIn.Content = $"In: [{Stats.In.ToString("N0")}]";
                    LblOut.Content = $"Out: [{Stats.Out.ToString("N0")}]";
                    LblLinksStat.Content = $"Links: [{Stats.Links.ToString("N0")}]";
                    LblCompleted.Content = $"Completed: [{Stats.Completed.ToString("N0")}]";
                    LblRestrictsStat.Content = $"Restricts: [{Stats.Restricts.ToString("N0")}]";
                    LblKeepAlivesStat.Content = $"Keep-alives: [{Stats.KeepAlives.ToString("N0")}]";
                });
            }
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _settingsWaifu.SavePrimitives();
            Process.GetCurrentProcess().Kill();
        }
    }
}
