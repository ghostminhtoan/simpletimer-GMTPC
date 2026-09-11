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

        public TimerInstance(int id, SoundManager sound, LowLevelKeyboardHook hook, GamepadManager gamepad = null, SettingsManager settings = null)
        {
            Id = id;
            Settings = settings ?? new SettingsManager(id);
            Manager = new TimeBombManager(sound, Settings, hook, autoWireHook: false);
            Window = new MainWindow(Settings, id) { Manager = Manager };
            Alarm = new AlarmWindow(Manager, id);

            Manager.OnDisplayChanged += (main, prefix, sub, isRed, isBlinkHidden) =>
            {
                Window?.UpdateDisplay(main, prefix, sub, isRed, isBlinkHidden);
            };

            Manager.OnVisibilityToggled += isVisible =>
            {
                try
                {
                    if (Window != null && Window.Dispatcher != null && !Window.Dispatcher.HasShutdownStarted)
                    {
                        Window.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (isVisible) Window.Show();
                                else Window.Hide();
                            }
                            catch { }
                        });
                    }
                }
                catch { }
            };

            Manager.OnAlarmTriggered += () =>
            {
                try
                {
                    gamepad?.StartAlarmVibration();
                    if (Alarm != null && Alarm.Dispatcher != null && !Alarm.Dispatcher.HasShutdownStarted)
                    {
                        Alarm.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Alarm.Show();
                                Alarm.Activate();
                            }
                            catch { }
                        });
                    }
                }
                catch { }
            };

            Manager.OnAlarmDismissed += () =>
            {
                try
                {
                    gamepad?.StopAlarmVibration();
                    if (Alarm != null && Alarm.Dispatcher != null && !Alarm.Dispatcher.HasShutdownStarted)
                    {
                        Alarm.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Alarm.Hide();
                            }
                            catch { }
                        });
                    }
                }
                catch { }
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
        private LowLevelMouseHook _mouseHook;
        private GamepadManager _gamepadManager;
        private SettingsManager _baseSettings;
        private IntervalTimerWindow _intervalWindow;
        private readonly List<TimerInstance> _instances = new List<TimerInstance>();
        private TimerInstance _activeInstance;
        private int _nextInstanceId = 1;

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (s, ev) =>
            {
                ev.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                // Prevent silent process crash from unhandled background domain exceptions
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                ev.SetObserved();
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
                _baseSettings = new SettingsManager(1);
                _hook = new LowLevelKeyboardHook(_baseSettings);
                _mouseHook = new LowLevelMouseHook();
                _gamepadManager = new GamepadManager(_baseSettings);

                WireHookEvents();
                WireMouseHook();
                WireGamepadEvents();
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
            SettingsManager instSettings = (id == 1 && _baseSettings != null) ? _baseSettings : new SettingsManager(id);
            var instance = new TimerInstance(id, _soundManager, _hook, _gamepadManager, instSettings);

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

            instance.Window.OnClickThroughChanged += (w, ct) => UpdateTrayIconMenu();
            instance.Window.OnOpacityChanged += (w, op) => UpdateTrayIconMenu();

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
            var list = _instances.ToArray();
            if (list.Length == 0) return null;

            if (Win32Api.GetCursorPos(out Win32Api.POINT pt))
            {
                for (int i = list.Length - 1; i >= 0; i--)
                {
                    var inst = list[i];
                    if (inst != null && inst.Window != null && inst.Window.IsVisible && inst.Window.IsPointInside(pt.X, pt.Y))
                    {
                        if (_activeInstance != inst)
                        {
                            SetActiveInstance(inst);
                        }
                        return inst;
                    }
                }
            }

            return _activeInstance ?? list[list.Length - 1];
        }

        private void WireHookEvents()
        {
            _hook.IsAlarmActive = () => _instances.Exists(i => i.Manager != null && i.Manager.IsAlarmActive);
            _hook.OnDismissAlarmRequested += () =>
            {
                if (Dispatcher != null && !Dispatcher.HasShutdownStarted)
                {
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var inst in _instances.ToArray())
                        {
                            if (inst != null && inst.Manager != null && inst.Manager.IsAlarmActive)
                            {
                                inst.Manager.DismissAlarm();
                            }
                        }
                    });
                }
            };

            _hook.OnToggleRequested += ToggleAll;
            _hook.OnIntervalToggleRequested += ToggleIntervalTimer;

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

        private void WireGamepadEvents()
        {
            if (_gamepadManager == null) return;

            _gamepadManager.OnToggleRequested += ToggleAll;

            _gamepadManager.OnPauseToggleRequested += () =>
            {
                GetTargetInstance()?.Manager.PauseToggle();
            };

            _gamepadManager.OnResetRequested += () =>
            {
                GetTargetInstance()?.Manager.Reset();
            };

            _gamepadManager.OnSaveRequested += () =>
            {
                GetTargetInstance()?.Manager.SaveCountdown();
            };

            _gamepadManager.OnSwitchModeRequested += () =>
            {
                GetTargetInstance()?.Manager.SwitchMode();
            };

            _gamepadManager.OnAdjustUpStart += () =>
            {
                GetTargetInstance()?.Manager.AdjustUpStart();
            };

            _gamepadManager.OnAdjustUpStop += () =>
            {
                GetTargetInstance()?.Manager.AdjustUpStop();
            };

            _gamepadManager.OnAdjustDownStart += () =>
            {
                GetTargetInstance()?.Manager.AdjustDownStart();
            };

            _gamepadManager.OnAdjustDownStop += () =>
            {
                GetTargetInstance()?.Manager.AdjustDownStop();
            };

            _gamepadManager.OnActionExecuted += () =>
            {
                GetTargetInstance()?.Manager.Unfreeze();
            };

            _gamepadManager.OnNewInstanceRequested += () =>
            {
                Dispatcher.Invoke(() => CreateTimerInstance());
            };

            _gamepadManager.OnCloseInstanceRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    var target = GetTargetInstance();
                    if (target != null) CloseTimerInstance(target);
                });
            };

            _gamepadManager.OnDismissAlarmRequested += () =>
            {
                if (Dispatcher != null && !Dispatcher.HasShutdownStarted)
                {
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var inst in _instances.ToArray())
                        {
                            if (inst != null && inst.Manager != null && inst.Manager.IsAlarmActive)
                            {
                                inst.Manager.DismissAlarm();
                            }
                        }
                    });
                }
            };
        }

        private void WireMouseHook()
        {
            _mouseHook.OnMiddleClickCheck = (screenX, screenY) =>
            {
                // Check if Ctrl key is held down
                bool isCtrlDown = (Win32Api.GetAsyncKeyState(Win32Api.VK_CONTROL) & 0x8000) != 0
                               || (Win32Api.GetKeyState(Win32Api.VK_CONTROL) & 0x8000) != 0;
                if (!isCtrlDown) return false;

                bool suppress = false;
                if (Dispatcher != null && !Dispatcher.HasShutdownStarted)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var list = _instances.ToArray();
                        TimerInstance hit = null;
                        for (int i = list.Length - 1; i >= 0; i--)
                        {
                            var inst = list[i];
                            if (inst != null && inst.Window != null && inst.Window.IsVisible && inst.Window.IsPointInside(screenX, screenY))
                            {
                                hit = inst;
                                break;
                            }
                        }

                        if (hit != null && hit.Window != null)
                        {
                            if (hit.Window.IsClickThrough)
                            {
                                hit.Window.SetClickThrough(false);
                                UpdateTrayIconMenu();
                            }

                            if (hit.Manager != null && (hit.Manager.Mode == AppMode.Timer || hit.Manager.Mode == AppMode.Stopwatch))
                            {
                                hit.Window.OpenTimerInputDialog();
                            }
                            else
                            {
                                _soundManager?.Play("adjust.wav");
                            }

                            suppress = true;
                        }
                    });
                }

                return suppress;
            };
        }

        public void OpenShortcutSettingsDialog()
        {
            if (Dispatcher != null && !Dispatcher.HasShutdownStarted)
            {
                Dispatcher.Invoke(() =>
                {
                    var dlg = new ShortcutSettingsWindow(_baseSettings);
                    dlg.OnShortcutsSaved += () =>
                    {
                        UpdateTrayIconMenu();
                    };
                    dlg.Show();
                    dlg.Activate();
                });
            }
        }

        private void ToggleIntervalTimer()
        {
            if (Dispatcher != null && !Dispatcher.HasShutdownStarted)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_intervalWindow == null)
                    {
                        _intervalWindow = new IntervalTimerWindow(_baseSettings, _soundManager);
                    }

                    if (_intervalWindow.IsVisible)
                    {
                        _intervalWindow.Hide();
                    }
                    else
                    {
                        _intervalWindow.Show();
                        _intervalWindow.Activate();
                    }
                });
            }
        }

        private void ToggleAll()
        {
            if (Dispatcher != null && !Dispatcher.HasShutdownStarted)
            {
                Dispatcher.Invoke(() =>
                {
                    var list = _instances.ToArray();
                    bool anyVisible = Array.Exists(list, i => i != null && i.Window != null && i.Window.IsVisible);
                    if (!anyVisible)
                    {
                        foreach (var inst in list)
                        {
                            inst?.Manager?.Start(playSound: false);
                        }
                    }
                    else
                    {
                        foreach (var inst in list)
                        {
                            inst?.Manager?.Stop();
                        }
                    }
                });
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
            contextMenu.Items.Add("Switch / Show Mode (Win + `)", null, (s, ev) => ToggleAll());
            contextMenu.Items.Add("Interval Timer (Ctrl + Win + `)", null, (s, ev) => ToggleIntervalTimer());
            contextMenu.Items.Add("Pause / Resume Active (Win + Enter)", null, (s, ev) => GetTargetInstance()?.Manager.PauseToggle());
            contextMenu.Items.Add("Reset Active (Win + Backspace)", null, (s, ev) => GetTargetInstance()?.Manager.Reset());
            contextMenu.Items.Add("Switch Mode Active (Win + Esc)", null, (s, ev) => GetTargetInstance()?.Manager.SwitchMode());
            contextMenu.Items.Add("Edit Shortcut (Chỉnh phím tắt)", null, (s, ev) => OpenShortcutSettingsDialog());
            contextMenu.Items.Add(new ToolStripSeparator());

            var activeInst = GetTargetInstance();
            if (activeInst != null)
            {
                var ctItem = new ToolStripMenuItem("👻 Click Through (Bấm xuyên qua)");
                ctItem.Checked = activeInst.Window.IsClickThrough;
                ctItem.ToolTipText = "Khi bật, nhấn Ctrl + Chuột giữa vào HUD để tắt";
                ctItem.Click += (s, ev) =>
                {
                    activeInst.Window.SetClickThrough(!activeInst.Window.IsClickThrough);
                    UpdateTrayIconMenu();
                };
                contextMenu.Items.Add(ctItem);

                var subItem = new ToolStripMenuItem("⏰ Show Start / End Time (Hiện mốc thời gian)");
                subItem.Checked = activeInst.Window.ShowSubInfo;
                subItem.ToolTipText = "Bật / Tắt hiển thị mốc Ends at / Started at ở dòng phụ";
                subItem.Click += (s, ev) =>
                {
                    activeInst.Window.SetShowSubInfo(!activeInst.Window.ShowSubInfo);
                    UpdateTrayIconMenu();
                };
                contextMenu.Items.Add(subItem);

                var moveDeskItem = new ToolStripMenuItem("🖥 Move to Next Desktop (Win + Ctrl + Right)");
                moveDeskItem.ToolTipText = "Chuyển cửa sổ ứng dụng hiện tại sang Virtual Desktop kế tiếp";
                moveDeskItem.Click += (s, ev) =>
                {
                    activeInst.Window.MoveToNextDesktop();
                };
                contextMenu.Items.Add(moveDeskItem);

                var opHeader = new ToolStripMenuItem($"🔆 Opacity: {(int)(activeInst.Window.Opacity * 100)}%") { Enabled = false };
                opHeader.Font = new System.Drawing.Font(opHeader.Font, System.Drawing.FontStyle.Bold);
                contextMenu.Items.Add(opHeader);

                var trackBar = new TrackBar
                {
                    Minimum = 20,
                    Maximum = 100,
                    Value = (int)(activeInst.Window.Opacity * 100),
                    TickFrequency = 10,
                    SmallChange = 5,
                    LargeChange = 10,
                    Width = 150,
                    Height = 28,
                    AutoSize = false
                };

                trackBar.Scroll += (s, ev) =>
                {
                    double newOp = trackBar.Value / 100.0;
                    opHeader.Text = $"🔆 Opacity: {trackBar.Value}%";
                    activeInst.Window.SetWindowOpacity(newOp);
                };

                var host = new ToolStripControlHost(trackBar)
                {
                    Width = 155,
                    Height = 28,
                    Margin = new Padding(12, 1, 12, 1)
                };
                contextMenu.Items.Add(host);
                contextMenu.Items.Add(new ToolStripSeparator());
            }

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

            if (_baseSettings != null)
            {
                var parallelItem = new ToolStripMenuItem("⌨️ Bàn phím & 🎮 Gamepad: Hoạt động song song") { Enabled = false };
                parallelItem.Font = new System.Drawing.Font(parallelItem.Font, System.Drawing.FontStyle.Bold);
                contextMenu.Items.Add(parallelItem);

                var gamepadItem = new ToolStripMenuItem("🎮 Gamepad Control (Steam Deck / Xbox / PS4)");
                gamepadItem.Checked = _baseSettings.GamepadEnabled;
                gamepadItem.Click += (s, ev) =>
                {
                    _baseSettings.GamepadEnabled = !_baseSettings.GamepadEnabled;
                    _baseSettings.Save();
                    _gamepadManager?.SetEnabled(_baseSettings.GamepadEnabled);
                    UpdateTrayIconMenu();
                };
                contextMenu.Items.Add(gamepadItem);

                var vibItem = new ToolStripMenuItem("📳 Gamepad Vibration (Haptics)");
                vibItem.Checked = _baseSettings.GamepadVibration;
                vibItem.Click += (s, ev) =>
                {
                    _baseSettings.GamepadVibration = !_baseSettings.GamepadVibration;
                    _baseSettings.Save();
                    _gamepadManager?.SetVibrationEnabled(_baseSettings.GamepadVibration);
                    UpdateTrayIconMenu();
                };
                contextMenu.Items.Add(vibItem);

                var startupItem = new ToolStripMenuItem("🚀 Start with Windows (Khởi động cùng Windows)");
                startupItem.Checked = _baseSettings.StartWithWindows;
                startupItem.ToolTipText = "Tự động chạy ứng dụng khi khởi động hệ điều hành Windows";
                startupItem.Click += (s, ev) =>
                {
                    bool newState = !_baseSettings.StartWithWindows;
                    if (StartupManager.SetStartup(newState))
                    {
                        _baseSettings.StartWithWindows = newState;
                        _baseSettings.Save();
                    }
                    UpdateTrayIconMenu();
                };
                contextMenu.Items.Add(startupItem);
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

            if (_intervalWindow != null)
            {
                try { _intervalWindow.Close(); } catch { }
                _intervalWindow = null;
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _hook?.Dispose();
            _hook = null;
            _mouseHook?.Dispose();
            _mouseHook = null;
            _gamepadManager?.Dispose();
            _gamepadManager = null;
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
