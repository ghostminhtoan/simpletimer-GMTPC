using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using TimeBomb.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace TimeBomb
{
    public partial class App : Application
    {
        private const string MutexName = "Global\\TimeBomb_WPF_SingleInstance_Mutex";
        private static Mutex _mutex;
        private NotifyIcon _notifyIcon;
        private SoundManager _soundManager;
        private SettingsManager _settingsManager;
        private LowLevelKeyboardHook _hook;
        private TimeBombManager _timeBombManager;
        private MainWindow _mainWindow;
        private AlarmWindow _alarmWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Enforce Single Instance
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("TimeBomb is already running in the background!\nUse Win + ` to show/hide.", "TimeBomb", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            try
            {
                _soundManager = new SoundManager();
                _settingsManager = new SettingsManager();
                _hook = new LowLevelKeyboardHook();
                _timeBombManager = new TimeBombManager(_soundManager, _settingsManager, _hook);

                _mainWindow = new MainWindow(_settingsManager);
                _alarmWindow = new AlarmWindow(_timeBombManager);

                // Wire manager events to UI
                _timeBombManager.OnDisplayChanged += (main, prefix, sub, isRed, isBlinkHidden) =>
                {
                    _mainWindow.UpdateDisplay(main, prefix, sub, isRed, isBlinkHidden);
                };

                _timeBombManager.OnVisibilityToggled += isVisible =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (isVisible)
                        {
                            _mainWindow.Show();
                        }
                        else
                        {
                            _mainWindow.Hide();
                        }
                    });
                };

                _timeBombManager.OnAlarmTriggered += () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _alarmWindow.Show();
                        _alarmWindow.Activate();
                    });
                };

                _timeBombManager.OnAlarmDismissed += () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _alarmWindow.Hide();
                    });
                };

                SetupTrayIcon();

                // Start visible by default
                _timeBombManager.Start(playSound: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing TimeBomb: " + ex.Message, "TimeBomb Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon();

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons", "timebomb.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
            }
            else
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Text = "TimeBomb (Win + `)";
            _notifyIcon.Visible = true;

            var contextMenu = new ContextMenuStrip();

            var titleItem = new ToolStripMenuItem("TimeBomb") { Enabled = false };
            titleItem.Font = new System.Drawing.Font(titleItem.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(titleItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            contextMenu.Items.Add("Show / Hide (Win + `)", null, (s, ev) => _timeBombManager.Toggle());
            contextMenu.Items.Add("Pause / Resume (Win + Enter)", null, (s, ev) => _timeBombManager.PauseToggle());
            contextMenu.Items.Add("Reset (Win + Backspace)", null, (s, ev) => _timeBombManager.Reset());
            contextMenu.Items.Add("Save Countdown (Win + S)", null, (s, ev) => _timeBombManager.SaveCountdown());
            contextMenu.Items.Add("Switch Mode (Win + Esc)", null, (s, ev) => _timeBombManager.SwitchMode());
            contextMenu.Items.Add(new ToolStripSeparator());

            contextMenu.Items.Add("Exit", null, (s, ev) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.MouseClick += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    _timeBombManager.Toggle();
                }
            };
        }

        private void ExitApplication()
        {
            _timeBombManager?.Stop();
            _settingsManager?.Save();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _timeBombManager?.Dispose();
            _alarmWindow?.Close();
            _mainWindow?.Close();

            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
            catch { }

            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ExitApplication();
            base.OnExit(e);
        }
    }
}
