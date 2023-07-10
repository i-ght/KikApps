using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DankLibWaifuz.SettingsWaifu;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Data;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.Etc;
using DankLibWaifuz.SettingsWaifu.SettingObjects;
using DankLibWaifuz.SettingsWaifu.SettingObjects.Collections;
using KikBot3.Declarations;
using KikBot3.Work;

namespace KikBot3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow 
    {
        private SettingsUi _settingsWaifu;
        private bool _running;

#if BLASTER
        private Label _lblBlasts;
#endif

        public MainWindow()
        {
            InitializeComponent();
            ThreadMonitor.DataContext = this;
            ThreadMonitorSource = new ObservableCollection<DataGridItem>();
        }

        public ObservableCollection<DataGridItem> ThreadMonitorSource { get; }
        public static int ScreenWidth { get; private set; }
        public static int ScreenHeight { get; private set; }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _settingsWaifu.SavePrimitives();
            KillAllFirefox();
            Process.GetCurrentProcess().Kill();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = $"{GeneralHelpers.ApplicationName(false, false)} {GeneralHelpers.ApplicationVersion()}";
            Settings.Load();
            Blacklists.Load();

            ThreadMonitor.LoadingRow += (o, args) =>
            {
                args.Row.Header = (args.Row.GetIndex() + 1).ToString();
            };

            InitSettingsUi();

#if BLASTER
            CreateBlasterUi();
#endif

            var scr = this.GetScreen();
            ScreenWidth = scr.WorkingArea.Width;
            ScreenHeight = scr.WorkingArea.Height;
        }

#if BLASTER
        private void CreateBlasterUi()
        {
            _lblBlasts = new Label
            {
                Content = "Blasts: [0]"
            };
            StsBar.Items.Insert(1, _lblBlasts);

            var column = new DataGridTextColumn
            {
                Width = 55,
                Header = "Blasts",
                Binding = new Binding("Blasts")
            };
            ThreadMonitor.Columns.Insert(2, column);

            var contacts = _settingsWaifu.GetFileStream("Contacts");
            contacts.Blacklists.Add(Blacklists.Collections[BlacklistType.Chat]);
            contacts.Blacklists.Add(Blacklists.Collections[BlacklistType.Blast]);
        }
#endif

        private static void KillAllFirefox()
        {
            var processes = Process.GetProcessesByName("firefox.exe");
            foreach (var p in processes)
                p.Kill();
        }

        private void InitSettingsUi()
        {
            var settings = new ObservableCollection<SettingObj>
            {
                new SettingPrimitive<int>("Max login errors", "MaxLoginErrors", 10),
                new SettingPrimitive<int>("Max captcha errors", "MaxCaptchaErrors", 10),
                new SettingPrimitive<int>("Max keep-alives", "MaxKeepAlives", 3),
                new SettingPrimitive<int>("Keep-alive delay (min)", "KeepAliveDelay", 40),
                new SettingPrimitive<int>("Minimum send message delay (sec)", "MinSendMessageDelay", 20),
                new SettingPrimitive<int>("Maximum send message delay (sec)", "MaxSendMessageDelay", 40),
                new SettingPrimitive<int>("Max concurrent firefox windows", "MaxConcurrentFirefoxWindows", 3),
                new SettingPrimitive<bool>("Use kik browser to share links", "UseKikBrowser", true),
                new SettingPrimitive<bool>("Disable keep-alives?", "DisableKeepAlives", false),
                new SettingPrimitive<bool>("Use message queue?", "UseMessageQueue", true),
                new SettingPrimitive<bool>("Solve captchas?", "SolveCaptchas", true),
                new SettingPrimitive<bool>("Use 2Captcha?", "Use2Captcha", false),
                new SettingPrimitive<bool>("Ignore acked messages upon login?", "IgnoreAckedMessages", false),
                new SettingPrimitive<string>("2CaptchA API key", "2CaptchaAPIKey", string.Empty),
                new SettingPrimitive<string>("Directory of image files", "ImageFilesDir", string.Empty),
                new SettingQueue("Accounts"),
                new SettingQueue("Proxies"),
                new SettingQueue("Web browser proxies", "WebBrowserProxies"),
                new SettingList("Script"),
                new SettingQueue("Links"),
                new SettingQueue("Keep-alives", "KeepAlives"),
                new SettingKeywords("Keywords"),
                new SettingList("Restricts"),
                new SettingQueue("Apologies")
            };

#if BLASTER
            settings.Insert(4, new SettingPrimitive<int>("Minimum blast delay (sec)", "MinBlastDelay", 40));
            settings.Insert(5, new SettingPrimitive<int>("Maximum blast delay (sec)", "MaxBlastDelay", 60));
            settings.Insert(6,
                new SettingPrimitive<int>("Minimum delay between blast sessions (min)", "MinBlastSessionDelay", 40));
            settings.Insert(7,
                new SettingPrimitive<int>("Maximum delay between blast sessions (min)", "MaxBlastSessionDelay", 60));
            settings.Insert(8, new SettingPrimitive<int>("Minimum blasts per session", "MinBlastsPerSession", 2));
            settings.Insert(9, new SettingPrimitive<int>("Maximum blasts per session", "MaxBlastsPerSession", 6));
            settings.Insert(14, new SettingPrimitive<bool>("Enable adding?", "EnableAdding", true));
            settings.Insert(15, new SettingPrimitive<bool>("Disable blasting?", "DisableBlasting", false));
            settings.Insert(23, new SettingQueue("Greets"));
            settings.Insert(27, new SettingFileStream("Contacts"));
#endif

            var settingsPage = (TabItem)TbMain.Items[1];
            var gridContent = (Grid)settingsPage.Content;

            _settingsWaifu = new SettingsUi(this, gridContent, settings);
            _settingsWaifu.CreateUi();

            Collections.SettingsUi = _settingsWaifu;

            ChkChatLogEnabled.IsChecked = Settings.Get<bool>("ChatLogEnabled");
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
            txt?.ScrollToEnd();
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

            InitThreadMonitor(accounts.Count);

            Main(accounts);
        }

        private void Main(Queue<Account> accounts)
        {
            _running = true;

            var maxWorkers = accounts.Count;
            for (var i = 0; i < maxWorkers; i++)
            {
                var cls = new Bot(i, ThreadMonitorSource, accounts.GetNext(false), TxtChatLog);
                Maintenance.Bots.Add(cls);
            }

            new Thread(StatsUi).Start();
            new Thread(Maintenance.Base).Start();
        }

        private void InitThreadMonitor(int maxWorkers)
        {
            ThreadMonitorSource.Clear();

            for (var i = 0; i < maxWorkers; i++)
            {
                var item = new DataGridItem
                {
                    Account = string.Empty,
                    Status = string.Empty,
                    InCount = 0,
                    OutCount = 0
#if BLASTER
                    ,BlastsCount = 0
#endif
                };

                ThreadMonitorSource.Add(item);
            }
        }

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

                    LblOnline.Content = $"Online: [{Maintenance.OnlineCount():N0}]";
                    LblConvos.Content = $"Convos: [{Stats.Convos:N0}]";
                    LblIn.Content = $"In: [{Stats.In:N0}]";
                    LblOut.Content = $"Out: [{Stats.Out:N0}]";
                    LblLinksStat.Content = $"Links: [{Stats.Links:N0}]";
                    LblCompleted.Content = $"Completed: [{Stats.Completed:N0}]";
                    LblRestrictsStat.Content = $"Restricts: [{Stats.Restricts:N0}]";
                    LblKeepAlivesStat.Content = $"Keep-alives: [{Stats.KeepAlives:N0}]";

#if BLASTER
                    _lblBlasts.Content = $"Blasts: [{Stats.Blasts:N0}]";
#endif
                });
            }
        }
    }
}
