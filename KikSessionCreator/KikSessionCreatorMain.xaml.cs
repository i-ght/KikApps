using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using DankLibWaifuz.SettingsWaifu.SettingObjects;
using DankLibWaifuz.SettingsWaifu.SettingObjects.Collections;
using KikSessionCreator.Declarations;
using KikSessionCreator.Work;
using KikWaifu;
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
using DankLibWaifuz.Etc;

namespace KikSessionCreator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private SettingsUi _settingsWaifu;
        private bool _running;
        private int _maxWorkers;

        public MainWindow()
        {
            InitializeComponent();
            ThreadMonitor.DataContext = this;
        }

        public static int ScreenWidth { get; private set; }
        public static int ScreenHeight { get; private set; }

        public ObservableCollection<DataGridItem> ThreadMonitorSource { get; } = new ObservableCollection<DataGridItem>();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";

            Settings.Load();
#if !DEBUG
            Blacklists.Load();
#endif
            InitSettingsUi();

            ThreadMonitor.LoadingRow += (o, args) =>
            {
                args.Row.Header = (args.Row.GetIndex() + 1).ToString();
            };

            var scr = this.GetScreen();
            ScreenWidth = scr.WorkingArea.Width;
            ScreenHeight = scr.WorkingArea.Height;
        }

        private void InitSettingsUi()
        {
            var settings = new ObservableCollection<SettingObj>
            {
                new SettingPrimitive<int>("Max workers", "MaxWorkers", 1),
                new SettingPrimitive<int>("Max login errors", "MaxLoginErrors", 8),
                new SettingPrimitive<int>("Max firefox launcher threads", "MaxFirefoxLauncherThreads", 1),
                new SettingPrimitive<int>("Solve no more then (x) captchas per attempt", "MaxCaptchaSolveAttempts", 1),
                new SettingPrimitive<int>("Solve captcha timeout", "SolveCaptchaTimeout", 120),
                new SettingPrimitive<bool>("Upload new avatar?", "UploadAvatar", false),
                new SettingPrimitive<bool>("Solve captchas?", "SolveCaptchas", true),
                new SettingPrimitive<bool>("Use 2captcha?", "Use2Captcha", false),
                new SettingPrimitive<bool>("Create new session? (if false, only checks for captcha)", "CreateNewSession", true),
                new SettingPrimitive<bool>("If 2captcha returns 0, restart worker?", "RestartWorkerOn2CaptchaError", false),
                new SettingPrimitive<string>("2Captcha API key", "2CaptchaAPIKey", string.Empty),
                new SettingQueue("Accounts"),
                new SettingQueue("Proxies"),
                new SettingQueue("Web browser proxies", "WebBrowserProxies"),
                new SettingQueue("Submit captcha proxies", "SubmitCaptchaProxies"),
                new SettingQueue("Android devices", "AndroidDevices"),
                new SettingPrimitive<string>("Image directories", "ImageDirs", string.Empty)
            };
            var settingsPage = (TabItem)TbMain.Items[1];
            var gridContent = (Grid)settingsPage.Content;

            _settingsWaifu = new SettingsUi(this, gridContent, settings);
            _settingsWaifu.CreateUi();

            Collections.SettingsUi = _settingsWaifu;

#if DEBUG
            var i = Settings.Get<int>("SolveCaptchaTimeout");
            i = i;
#endif
        }

        private void Window_Closing(object sender, CancelEventArgs args)
        {
            _settingsWaifu.SavePrimitives();
            Process.GetCurrentProcess().Kill();
        }

        private void InitThreadMonitor()
        {
            for (var i = 0; i < _maxWorkers; i++)
            {
                var item = new DataGridItem
                {
                    Account = string.Empty,
                    Status = string.Empty
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

                    LblTtlLoginAttempts.Content = $"Total login attempts: [{Stats.TotalLoginAttempts.ToString("N0")}]";
                    LblSuccessfulLogins.Content = $"Successful logins: [{Stats.SuccessfulLogins.ToString("N0")}]";
                    LblAccountsInQueue.Content = $"Accounts left in queue: [{SessionCreator.Accounts.Count.ToString("N0")}]";
                });
            }
        }

        private async Task DoWork()
        {
            _running = true;
            new Thread(StatsUi).Start();

            string acctStr;
            while (!string.IsNullOrWhiteSpace(acctStr = Collections.Accounts.GetNext(false)))
            {
                var androidDevice = new AndroidDevice(Collections.AndroidDevices.GetNext());
                var acct = new Account(acctStr, androidDevice);
                if (!acct.IsValid)
                    continue;

                SessionCreator.Accounts.Enqueue(acct);
            }

            if (SessionCreator.Accounts.Count == 0)
            {
                MessageBox.Show("Invalid accounts file");
                return;
            }

            SessionCreator.InitSempahore();

            var tasks = new List<Task>();
            for (var i = 0; i < _maxWorkers; i++)
            {
                var cls = new SessionCreator(i, ThreadMonitorSource);
                tasks.Add(cls.Base());
            }

            await Task.WhenAll(tasks);

            _running = false;
            CmdLaunch.IsEnabled = true;
            MessageBox.Show("Work complete");
        }

        private async void CmdLaunch_OnClick(object sender, RoutedEventArgs e)
        {
            _settingsWaifu.SavePrimitives();

            Collections.Shuffle();

            _maxWorkers = Settings.Get<int>("MaxWorkers");

            InitThreadMonitor();

            CmdLaunch.IsEnabled = false;
            await DoWork().ConfigureAwait(false);
        }
    }
}
