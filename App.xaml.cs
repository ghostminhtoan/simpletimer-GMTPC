using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using TimeBomb.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace TimeBomb
{
    public class TimerInstance : IDisposable
    {
        public int Id { get; }
        public SettingsManager Settings { get; }
        public TimeBombManager Manager { get; }
        public MainWindow Window { get; }
        public AlarmWindow Alarm { get; }

        public TimerInstance(int id, SoundManager sound, LowLevelKeyboardHook hook)
        {
            Id = id;
            Settings = new SettingsManager(id);
            Manager = new TimeBombManager(sound, Settings, hook, autoWireHook: false);
            Window = new MainWindow(Settings, id) { Manager = Manager };
            Alarm = new AlarmWindow(Manager, id);

            Manager.OnDisplayChanged += (main, prefix, sub, isRed, isBlinkHidden) =>
            {
                Window.UpdateDisplay(main, prefix, sub, isRed, isBlinkHidden);
            };

            Manager.OnVisibilityToggled += isVisible =>
            {
                Window.Dispatcher.Invoke(() =>
                {
                    if (isVisible) Window.Show();
                    else Window.Hide();
                });
            };

            Manager.OnAlarmTriggered += () =>
            {
                Window.Dispatcher.Invoke(() =>
                {
                    Alarm.Show();
                    Alarm.Activate();
                });
            };

            Manager.OnAlarmDismissed += () =>
            {
                Window.Dispatcher.Invoke(() =>
                {
                    Alarm.Hide();
                });
            };
        }

        public void Dispose()
        {
            Manager?.Stop();
            Settings?.Save();
            Manager?.Dispose();
            Alarm?.Close();
            Window?.Close();
        }
    }

    public partial class App : Application
    {
        private const string MutexName = "Local\\TimeBomb_SingleInstance_Coordinator_Mutex";
        private const string EventName = "Local\\TimeBomb_NewInstance_Signal_Event";
        private static Mutex _mutex;
        private static EventWaitHandle _newInstanceEvent;
        private RegisteredWaitHandle _waitHandleRegistration;

        private NotifyIcon _notifyIcon;
        private SoundManager _soundManager;
        private LowLevelKeyboardHook _hook;
        private readonly List<TimerInstance> _instances = new List<TimerInstance>();
        private TimerInstance _activeInstance;
        private int _nextInstanceId = 1;

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (s, ev) =>
            {
                ev.Handled = true;
            };

            bool isPrimary = false;
            try
            {
                _mutex = new Mutex(true, MutexName, out bool createdNew);
                if (createdNew)
                {
                    isPrimary = true;
                }
                else
                {
                    isPrimary = _mutex.WaitOne(0, false);
                }
            }
            catch (AbandonedMutexException)
            {
                isPrimary = true;
            }
            catch
            {
                isPrimary = true;
            }

            if (!isPrimary)
            {
                // Signal existing primary instance to open a new timer window
                try
                {
                    using (var evt = EventWaitHandle.OpenExisting(EventName))
                    {
                        evt.Set();
                    }
                }
                catch { }

                Shutdown();
                return;
            }

            base.OnStartup(e);

            try
            {
                SetupIpcEvent();

                _soundManager = new SoundManager();
                _hook = new LowLevelKeyboardHook();

                WireHookEvents();
                CreateTimerInstance();
                SetupTrayIcon();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing TimeBomb: " + ex.Message, "TimeBomb Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void SetupIpcEvent()
        {
            try
            {
                _newInstanceEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName, out bool _);
                _waitHandleRegistration = ThreadPool.RegisterWaitForSingleObject(_newInstanceEvent, (state, timedOut) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CreateTimerInstance();
                    }));
                }, null, -1, false);
            }
            catch { }
        }

        public TimerInstance CreateTimerInstance()
        {
            int id = _nextInstanceId++;
            var instance = new TimerInstance(id, _soundManager, _hook);

            // If there is already a previous instance, position the new instance nicely below it
            if (_instances.Count > 0)
            {
                var prev = _instances[_instances.Count - 1];
                double newLeft = double.IsNaN(prev.Window.Left) ? 150 : prev.Window.Left;
                double prevTop = double.IsNaN(prev.Window.Top) ? 150 : prev.Window.Top;
                double prevH = prev.Window.Height > 0 ? prev.Window.Height : 82;
                double newTop = prevTop + prevH + 10;

                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)newLeft, (int)newTop));
                if (newTop + 82 > screen.WorkingArea.Bottom)
                {
                    newTop = prevTop;
                    newLeft = newLeft - 190;
                    if (newLeft < screen.WorkingArea.Left)
                    {
                        newLeft = (double.IsNaN(prev.Window.Left) ? 150 : prev.Window.Left) + 190;
                    }
                }

                instance.Window.Left = newLeft;
                instance.Window.Top = newTop;
                instance.Settings.WindowX = (int)newLeft;
                instance.Settings.WindowY = (int)newTop;
                instance.Settings.Save();
            }

            instance.Window.OnActivatedByInteraction += instWin =>
            {
                var target = _instances.Find(i => i.Window == instWin);
                if (target != null)
                {
                    SetActiveInstance(target);
                }
            };

            instance.Window.OnRequestNewInstance += () =>
            {
                CreateTimerInstance();
            };

            instance.Window.OnRequestCloseInstance += instWin =>
            {
                var target = _instances.Find(i => i.Window == instWin);
                if (target != null)
                {
                    CloseTimerInstance(target);
                }
            };

            instance.Window.OnRequestExitAll += () =>
            {
                ExitApplication();
            };

            _instances.Add(instance);

            UpdateBadges();
            SetActiveInstance(instance);
            instance.Manager.Start(playSound: true);
            instance.Window.Show();
            UpdateTrayIconMenu();

            return instance;
        }

        public void CloseTimerInstance(TimerInstance instance)
        {
            if (instance == null) return;

            _instances.Remove(instance);
            instance.Dispose();

            if (_instances.Count == 0)
            {
                ExitApplication();
                return;
            }

            if (_activeInstance == instance)
            {
                _activeInstance = _instances[_instances.Count - 1];
                SetActiveInstance(_activeInstance);
            }

            UpdateBadges();
            UpdateTrayIconMenu();
        }

        private void UpdateBadges()
        {
            bool showBadges = _instances.Count > 1;
            foreach (var inst in _instances)
            {
                inst.Window.SetBadge(inst.Id, showBadges);
            }
        }

        public void SetActiveInstance(TimerInstance instance)
        {
            _activeInstance = instance;
            foreach (var inst in _instances)
            {
                inst.Window.SetActive(inst == instance);
            }
        }

        public TimerInstance GetTargetInstance()
        {
            if (_instances.Count == 0) return null;

            if (Win32Api.GetCursorPos(out Win32Api.POINT pt))
            {
                for (int i = _instances.Count - 1; i >= 0; i--)
                {
                    var inst = _instances[i];
                    if (inst.Window.IsVisible && inst.Window.IsPointInside(pt.X, pt.Y))
                    {
                        if (_activeInstance != inst)
                        {
                            SetActiveInstance(inst);
                        }
                        return inst;
                    }
                }
            }

            return _activeInstance ?? _instances[_instances.Count - 1];
        }

        private void WireHookEvents()
        {
            _hook.OnToggleRequested += ToggleAll;

            _hook.OnPauseToggleRequested += () =>
            {
                GetTargetInstance()?.Manager.PauseToggle();
            };

            _hook.OnResetRequested += () =>
            {
                GetTargetInstance()?.Manager.Reset();
            };

            _hook.OnSaveRequested += () =>
            {
                GetTargetInstance()?.Manager.SaveCountdown();
            };

            _hook.OnSwitchModeRequested += () =>
            {
                GetTargetInstance()?.Manager.SwitchMode();
            };

            _hook.OnAdjustUpStart += () =>
            {
                GetTargetInstance()?.Manager.AdjustUpStart();
            };

            _hook.OnAdjustUpStop += () =>
            {
                GetTargetInstance()?.Manager.AdjustUpStop();
            };

            _hook.OnAdjustDownStart += () =>
            {
                GetTargetInstance()?.Manager.AdjustDownStart();
            };

            _hook.OnAdjustDownStop += () =>
            {
                GetTargetInstance()?.Manager.AdjustDownStop();
            };

            _hook.OnWinKeyReleased += () =>
            {
                GetTargetInstance()?.Manager.OnWinKeyReleased();
            };

            _hook.OnNewInstanceRequested += () =>
            {
                Dispatcher.Invoke(() => CreateTimerInstance());
            };

            _hook.OnCloseInstanceRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    var target = GetTargetInstance();
                    if (target != null) CloseTimerInstance(target);
                });
            };
        }

        private void ToggleAll()
        {
            Dispatcher.Invoke(() =>
            {
                bool anyVisible = _instances.Exists(i => i.Window.IsVisible);
                foreach (var inst in _instances)
                {
                    if (anyVisible) inst.Manager.Stop();
                    else inst.Manager.Start(playSound: false);
                }
            });
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

            _notifyIcon.MouseClick += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    ToggleAll();
                }
            };

            UpdateTrayIconMenu();
        }

        private void UpdateTrayIconMenu()
        {
            if (_notifyIcon == null) return;

            var contextMenu = new ContextMenuStrip();

            string titleText = _instances.Count > 1 
                ? $"TimeBomb ({_instances.Count} Timers)" 
                : "TimeBomb";
            var titleItem = new ToolStripMenuItem(titleText) { Enabled = false };
            titleItem.Font = new System.Drawing.Font(titleItem.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(titleItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            contextMenu.Items.Add("New Timer (Win + N)", null, (s, ev) => CreateTimerInstance());
            contextMenu.Items.Add("Show / Hide All (Win + `)", null, (s, ev) => ToggleAll());
            contextMenu.Items.Add("Pause / Resume Active (Win + Enter)", null, (s, ev) => GetTargetInstance()?.Manager.PauseToggle());
            contextMenu.Items.Add("Reset Active (Win + Backspace)", null, (s, ev) => GetTargetInstance()?.Manager.Reset());
            contextMenu.Items.Add("Switch Mode Active (Win + Esc)", null, (s, ev) => GetTargetInstance()?.Manager.SwitchMode());
            contextMenu.Items.Add(new ToolStripSeparator());

            if (_instances.Count > 0)
            {
                foreach (var inst in _instances.ToArray())
                {
                    string label = $"Timer #{inst.Id} [{inst.Manager.Mode}]";
                    var item = new ToolStripMenuItem(label);
                    item.Click += (s, ev) =>
                    {
                        SetActiveInstance(inst);
                        if (!inst.Window.IsVisible) inst.Manager.Start(playSound: false);
                    };
                    contextMenu.Items.Add(item);
                }
                contextMenu.Items.Add(new ToolStripSeparator());
            }

            contextMenu.Items.Add("Exit All", null, (s, ev) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ExitApplication()
        {
            try
            {
                _waitHandleRegistration?.Unregister(null);
                _newInstanceEvent?.Dispose();
                _newInstanceEvent = null;
            }
            catch { }

            foreach (var inst in _instances)
            {
                inst.Dispose();
            }
            _instances.Clear();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _hook?.Dispose();
            _hook = null;
            _soundManager?.Dispose();
            _soundManager = null;

            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
                _mutex = null;
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
