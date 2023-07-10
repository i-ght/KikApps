using DankLibWaifuz.Etc;
using DankLibWaifuz.SettingsWaifu;
using DankLibWaifuz.SettingsWaifu.SettingObjects;
using DankLibWaifuz.SettingsWaifu.SettingObjects.Collections;
using KikCreator2.Declarations;
using KikCreator2.Work;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DankLibWaifuz.CollectionsWaifu;
using OpenQA.Selenium.Firefox;
using Point = System.Drawing.Point;


namespace KikCreator2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static int ScreenWidth { get; private set; }
        public static int ScreenHeight { get; private set; }

        private SettingsUi _settingsWaifu;
        private bool _running;
        private int _maxWorkers;

        public ObservableCollection<DataGridItem> ThreadMonitorSource { get; } = new ObservableCollection<DataGridItem>();

        public MainWindow()
        {
            InitializeComponent();
            ThreadMonitor.DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";

            ThreadMonitor.LoadingRow += (o, args) =>
            {
                args.Row.Header = (args.Row.GetIndex() + 1).ToString();
            };

            Settings.Load();
            Blacklists.Load();
            InitSettingsUi();

            var scr = this.GetScreen();
            ScreenWidth = scr.WorkingArea.Width;
            ScreenHeight = scr.WorkingArea.Height;

            if (ScreenWidth >= 1920)
            {
                Creator.WindowPositions = new Queue<Point>(new List<Point>
                {
                    new Point(0, MainWindow.ScreenHeight - 568),
                    new Point(320, MainWindow.ScreenHeight - 568),
                    new Point(640, MainWindow.ScreenHeight - 568),
                    new Point(960, MainWindow.ScreenHeight - 568),
                    new Point(1280, MainWindow.ScreenHeight - 568),
                    new Point(1600, MainWindow.ScreenHeight - 568),
                    new Point(0, 0),
                    new Point(320, 0),
                    new Point(640, 0),
                    new Point(960, 0),
                    new Point(1280, 0),
                    new Point(1600, 0)
                });
            }
            else
            {
                Creator.WindowPositions = new Queue<Point>(new List<Point>
                {
                    new Point(0, MainWindow.ScreenHeight - 568),
                    new Point(320, MainWindow.ScreenHeight - 568),
                    new Point(640, MainWindow.ScreenHeight - 568),
                    new Point(960, MainWindow.ScreenHeight - 568),
                    new Point(0, 0),
                    new Point(320, 0),
                    new Point(640, 0),
                    new Point(960, 0),
                });
            }
        }

        private void InitSettingsUi()
        {
            var settings = new ObservableCollection<SettingObj>
            {
                new SettingPrimitive<int>("Max workers", "MaxWorkers", 1),
                new SettingPrimitive<int>("Max creates", "MaxCreates", 1),
                new SettingPrimitive<int>("Minimum delay before checking username", "MinDelayBeforeCheckingUsername", 8),
                new SettingPrimitive<int>("Maximum delay before checking username", "MaxDelayBeforeCheckingUsername", 13),
                new SettingPrimitive<int>("Minimum delay before registration", "MinDelayBeforeRegistration", 15),
                new SettingPrimitive<int>("Maximum delay before registration", "MaxDelayBeforeRegistration", 20),
                new SettingPrimitive<int>("Max firefox launcher threads", "MaxFirefoxLauncherThreads", 3),
                new SettingPrimitive<int>("Solve no more then (x) captchas per attempt", "MaxCaptchaSolveAttempts", 1),
                new SettingPrimitive<int>("Timeout creation attempt after (x) seconds", "CancelAfter", 300),
                new SettingPrimitive<int>("Solve captcha timeout", "SolveCaptchaTimeout", 120),
                new SettingPrimitive<bool>("Use 2captcha?", "Use2Captcha", false),
                new SettingPrimitive<string>("2Captcha API key", "2CaptchaAPIKey", string.Empty),
                new SettingQueue("Proxies"),
                new SettingQueue("WebBrowserProxies"),
                new SettingQueue("SubmitCaptchaProxies"),
                new SettingQueue("Words1"),
                new SettingQueue("Words2"),
                new SettingQueue("First names", "FirstNames"),
                new SettingQueue("Last names", "LastNames"),
                new SettingQueue("AndroidDevices"),
                new SettingPrimitive<string>("Image directories", "ImageDirs", string.Empty)
            };

            var settingsPage = (TabItem)TbMain.Items[1];
            var gridContent = (Grid)settingsPage.Content;
            _settingsWaifu = new SettingsUi(this, gridContent, settings);
            _settingsWaifu.CreateUi();

            Collections.SettingsUi = _settingsWaifu;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _settingsWaifu.SavePrimitives();
            Process.GetCurrentProcess().Kill();
        }

        private async void CmdLaunch_Click(object sender, RoutedEventArgs e)
        {
            _settingsWaifu.SavePrimitives();

            if (!Collections.IsValid())
            {
                MessageBox.Show("Missing required files");
                return;
            }

            Collections.Shuffle();

            _maxWorkers = Settings.Get<int>("MaxWorkers");

            InitThreadMonitor();

            CmdLaunch.IsEnabled = false;

            await DoWork().ConfigureAwait(false);
        }

        private async Task DoWork()
        {
            _running = true;
            new Thread(StatsUi).Start();

            Creator.InitSempahore();

            var tasks = new List<Task>();
            for (var i = 0; i < _maxWorkers; i++)
            {
                var cls = new Creator(i, ThreadMonitorSource);
                tasks.Add(cls.Base());
            }

            await Task.WhenAll(tasks);

            _running = false;
            CmdLaunch.IsEnabled = true;
            MessageBox.Show("Work complete");
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

                    LblAttempts.Content = $"Attempts: [{Stats.Attempts.ToString("N0")}]";
                    LblCreated.Content = $"Created: [{Stats.Created.ToString("N0")}]";
                });
            }
        }

        private void InitThreadMonitor()
        {
            ThreadMonitorSource.Clear();

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
    }
}
