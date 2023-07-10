using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using DankLibWaifuz.SettingsWaifu.SettingObjects;
using DankLibWaifuz.SettingsWaifu.SettingObjects.Collections;
using KikContactVerifier2.Declarations;
using KikContactVerifier2.Work;
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

namespace KikContactVerifier2
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

            Settings.Save("Mode", "username");
        }
        
        private void InitSettingsUi()
        {
            var settings = new ObservableCollection<SettingObj>
            {
                new SettingPrimitive<int>("Max workers", "MaxWorkers", 1),
                new SettingPrimitive<int>("Max login errors", "MaxLoginErrors", 8),
                new SettingPrimitive<int>("Minimum attempts per session", "MinAttemptsPerSession", 50),
                new SettingPrimitive<int>("Maximum attempts per session", "MaxAttemptsPerSession", 100),
                new SettingPrimitive<int>("Minimum seconds to wait between attempts", "MinDelayBetweenAttempts", 8),
                new SettingPrimitive<int>("Maximum seconds to wait between attempts", "MaxDelayBetweenAttempts", 13),
                new SettingPrimitive<int>("Minimum minutes between sessions", "MinDelayBetweenSessions", 10),
                new SettingPrimitive<int>("Maximum minutes between sessions", "MaxDelayBetweenSessions", 20),
                //new SettingPrimitive<string>("Mode (email | username)", "Mode", "username"),
                new SettingPrimitive<int>("Check for rated account every (x) attempts", "CheckIfRatedAt", 50),
                new SettingQueue("Accounts"),
                new SettingQueue("Proxies"),
                new SettingFileStream("Contacts"),
                new SettingQueue("Verified contacts", "VerifiedContacts")
            };
            var settingsPage = (TabItem)TbMain.Items[1];
            var gridContent = (Grid)settingsPage.Content;

            _settingsWaifu = new SettingsUi(this, gridContent, settings);
            _settingsWaifu.CreateUi();

            Collections.SettingsUi = _settingsWaifu;
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
                    Status = string.Empty,
                    AttemptsCount = 0,
                    AttemptsSessionCount = 0,
                    VerifiedCount = 0,
                    VerifiedSessionCount = 0
                };
                ThreadMonitorSource.Add(item);
            }
        }

        private async void CmdLaunch_OnClick(object sender, RoutedEventArgs e)
        {
            _settingsWaifu.SavePrimitives();

            Collections.Shuffle();

            _maxWorkers = Settings.Get<int>("MaxWorkers");

            InitThreadMonitor();

            await DoWork().ConfigureAwait(false);
        }

        private async Task DoWork()
        {
            _running = true;
            new Thread(StatsUi).Start();

            if (Collections.Contacts.ReachedEOF)
            {
                MessageBox.Show("Missing contacts file");
                return;
            }

            var mode = Settings.Get<string>("Mode");
            switch (mode.ToLower())
            {
                case "email":
                case "username":
                    break;

                default:
                    MessageBox.Show("Invalid mode.  Valid options are: username or email");
                    return;
            }

            Collections.Contacts.Blacklists.Clear();
            Collections.Contacts.Blacklists.Add(Blacklists.Dict[BlacklistType.Verify]);
            Verifier.Accounts.Clear();

            string acctStr;
            while (!string.IsNullOrWhiteSpace(acctStr = Collections.Accounts.GetNext(false)))
            {
                var acct = new Account(acctStr);
                if (!acct.IsValid)
                    continue;

                if (Blacklists.Dict[BlacklistType.Rated].Contains(acct.LoginId))
                    continue;

                Verifier.Accounts.Enqueue(acct);
            }

            if (Verifier.Accounts.Count == 0)
            {
                MessageBox.Show("Invalid accounts file");
                return;
            }

            var tasks = new List<Task>();
            for (var i = 0; i < _maxWorkers; i++)
            {
                var cls = new Verifier(i, ThreadMonitorSource);
                tasks.Add(cls.Base());
            }

            await Task.WhenAll(tasks);

            _running = false;
            CmdLaunch.IsEnabled = false;
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

                    LblOnline.Content = $"Online: [{Stats.Online.ToString("N0")}]";
                    LblAttempts.Content = $"Attempts: [{Stats.Attempts.ToString("N0")}]";
                    LblVerified.Content = $"Verified: [{Stats.Verified.ToString("N0")}]";
                });
            }
        }

    }
}
