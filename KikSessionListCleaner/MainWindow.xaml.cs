using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using DankLibWaifuz.SettingsWaifu.SettingObjects;
using DankLibWaifuz.SettingsWaifu.SettingObjects.Collections;

namespace KikSessionListCleaner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SettingsUi _settingsWaifu;
        private MenuItem _cmdLaunch;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitSettingsUi();
        }

        private Queue<string> _oldSessions;
        private Queue<string> _newSessions;

        private void InitSettingsUi()
        {
            const string old = "Old sessions";
            const string n = "New sessions";
            var settings = new ObservableCollection<SettingObj>
            {
                new SettingQueue(old, Constants.OldSessionSetting),
                new SettingQueue(n, Constants.NewSessionsSetting)
            };
            var gridContent = (Grid)Content;

            _settingsWaifu = new SettingsUi(this, gridContent, settings);
            _settingsWaifu.CreateUi();

            var mnu = new ContextMenu();
            _cmdLaunch = new MenuItem
            {
                Header = "Launch"
            };
            _cmdLaunch.Click += CmdLaunch_OnClick;
            mnu.Items.Add(_cmdLaunch);
            _settingsWaifu.DataGrid.ContextMenu = mnu;

            _oldSessions = _settingsWaifu.GetQueue(Constants.OldSessionSetting);
            _newSessions = _settingsWaifu.GetQueue(Constants.NewSessionsSetting);
        }

        private void CmdLaunch_OnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            if (_oldSessions.Count == 0 || _newSessions.Count == 0)
            {
                MessageBox.Show(@"Missing required files");
                return;
            }

            _cmdLaunch.IsEnabled = false;

            new Thread(Work).Start();
        }

        private void Work()
        {
            var newSessions = new Dictionary<string, string>();
            var oldSessions = new Dictionary<string, string>();
            var tmp = new HashSet<string>();

            try
            {
                string s;
                while (!string.IsNullOrWhiteSpace(s = _oldSessions.GetNext(false)))
                {
                    var split = s.Split(':');
                    if (split.Length < 3)
                        continue;

                    var jid = split[2];

                    if (oldSessions.ContainsKey(jid))
                        continue;

                    oldSessions.Add(jid, s);
                }

                while (!string.IsNullOrWhiteSpace(s = _newSessions.GetNext(false)))
                {
                    var split = s.Split(':');
                    if (split.Length < 3)
                        continue;

                    var jid = split[2];
                    if (newSessions.ContainsKey(jid))
                        continue;

                    newSessions.Add(jid, s);
                }

                foreach (var key in oldSessions.Keys)
                {
                    if (newSessions.ContainsKey(key))
                        continue;

                    tmp.Add(oldSessions[key]);
                    //using (var sw = new StreamWriter("needs_new_session.txt", true))
                    //    sw.WriteLineAsync(oldSessions[key].ToString());
                }

                File.WriteAllLines("needs_new_session.txt", tmp);
            }
            finally
            {
                tmp.Clear();
                newSessions.Clear();
                oldSessions.Clear();
                Dispatcher.Invoke(() =>
                {
                    _cmdLaunch.IsEnabled = true;
                    MessageBox.Show("Work complete");
                });
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }
    }
}
