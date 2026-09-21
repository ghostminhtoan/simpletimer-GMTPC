using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TimeBomb.Core
{
    public class LowLevelKeyboardHook : IDisposable
    {
        private IntPtr _hookId = IntPtr.Zero;
        private readonly Win32Api.LowLevelKeyboardProc _proc;
        private bool _isWinDown = false;
        private bool _shortcutExecuted = false;
        private bool _isUpHeld = false;
        private bool _isDownHeld = false;
        private bool _suppressNextEscUp = false;

        private SettingsManager _settings;

        public event Action OnToggleRequested;
        public event Action OnIntervalToggleRequested;
        public event Action OnPauseToggleRequested;
        public event Action OnResetRequested;
        public event Action OnSaveRequested;
        public event Action OnSwitchModeRequested;
        public event Action OnNewInstanceRequested;
        public event Action OnCloseInstanceRequested;

        public event Action OnAdjustUpStart;
        public event Action OnAdjustUpStop;
        public event Action OnAdjustDownStart;
        public event Action OnAdjustDownStop;

        public event Action OnWinKeyReleased;
        public event Action OnDismissAlarmRequested;

        public Func<bool> IsAlarmActive { get; set; }

        public bool IsWinKeyHeld => _isWinDown;
        public bool ShortcutExecuted => _shortcutExecuted;

        private System.Windows.Threading.DispatcherTimer _retryHookTimer;
        private System.Threading.Thread _hookThread;
        private uint _hookThreadId = 0;
        private readonly System.Threading.ManualResetEvent _hookStartedEvent = new System.Threading.ManualResetEvent(false);
        private volatile bool _isRunning = false;
        private const uint WM_REHOOK = Win32Api.WM_USER + 1;

        public LowLevelKeyboardHook(SettingsManager settings = null)
        {
            _settings = settings;
            _proc = HookCallback;
            StartHookThread();
        }

        public void SetSettings(SettingsManager settings)
        {
            _settings = settings;
        }

        private void StartHookThread()
        {
            _isRunning = true;
            _hookThread = new System.Threading.Thread(HookThreadProc)
            {
                IsBackground = true,
                Name = "TimeBomb_KeyboardHookThread"
            };
            _hookThread.SetApartmentState(System.Threading.ApartmentState.STA);
            _hookThread.Start();
            _hookStartedEvent.WaitOne(2000);
        }

        private void HookThreadProc()
        {
            _hookThreadId = Win32Api.GetCurrentThreadId();
            InstallHook();
            _hookStartedEvent.Set();

            // Dedicated pure Win32 message pump running 24/7 on background STA thread
            while (_isRunning && Win32Api.GetMessage(out Win32Api.MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_REHOOK)
                {
                    UninstallHook();
                    InstallHook();
                    continue;
                }
                Win32Api.TranslateMessage(ref msg);
                Win32Api.DispatchMessage(ref msg);
            }

            UninstallHook();
        }

        public void EnsureHook()
        {
            if (_hookId == IntPtr.Zero || _hookThread == null || !_hookThread.IsAlive)
            {
                Rehook();
            }
        }

        public void Rehook()
        {
            try
            {
                if (_hookThreadId != 0)
                {
                    Win32Api.PostThreadMessage(_hookThreadId, WM_REHOOK, UIntPtr.Zero, IntPtr.Zero);
                }
                else if (_hookThread == null || !_hookThread.IsAlive)
                {
                    StartHookThread();
                }
            }
            catch { }
        }

        private void InstallHook()
        {
            try
            {
                IntPtr hMod = IntPtr.Zero;
                try
                {
                    using (Process curProcess = Process.GetCurrentProcess())
                    using (ProcessModule curModule = curProcess.MainModule)
                    {
                        if (curModule != null)
                        {
                            hMod = Win32Api.GetModuleHandle(curModule.ModuleName);
                        }
                    }
                }
                catch { }

                if (hMod == IntPtr.Zero)
                {
                    hMod = Win32Api.GetModuleHandle((string)null);
                }
                if (hMod == IntPtr.Zero)
                {
                    hMod = Win32Api.LoadLibrary("user32.dll");
                }

                _hookId = Win32Api.SetWindowsHookEx(Win32Api.WH_KEYBOARD_LL, _proc, hMod, 0);
            }
            catch { }

            // If desktop session is not yet ready (Windows startup), retry after delay
            if (_hookId == IntPtr.Zero)
            {
                ScheduleRetry();
            }
            else
            {
                StopRetry();
            }
        }

        private void UninstallHook()
        {
            try
            {
                if (_hookId != IntPtr.Zero)
                {
                    Win32Api.UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }
            }
            catch { }
        }

        private int _retryCount = 0;
        private void ScheduleRetry()
        {
            if (_retryCount >= 10) return;
            _retryCount++;

            if (_retryHookTimer == null)
            {
                _retryHookTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                _retryHookTimer.Tick += (s, e) =>
                {
                    if (_hookId != IntPtr.Zero)
                    {
                        StopRetry();
                        return;
                    }
                    Rehook();
                };
            }

            if (!_retryHookTimer.IsEnabled)
            {
                _retryHookTimer.Start();
            }
        }

        private void StopRetry()
        {
            if (_retryHookTimer != null && _retryHookTimer.IsEnabled)
            {
                _retryHookTimer.Stop();
            }
        }

        private bool _suppressStartMenuOnWinUp = false;

        private void DispatchAction(Action action)
        {
            if (action == null) return;
            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.HasShutdownStarted)
                {
                    dispatcher.BeginInvoke(action);
                    return;
                }
            }
            catch { }

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { action(); } catch { }
            });
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && lParam != IntPtr.Zero)
            {
                try
                {
                    int msg = wParam.ToInt32();
                    bool isKeyDown = (msg == Win32Api.WM_KEYDOWN || msg == Win32Api.WM_SYSKEYDOWN);
                    bool isKeyUp = (msg == Win32Api.WM_KEYUP || msg == Win32Api.WM_SYSKEYUP);

                    var hookStruct = (Win32Api.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Api.KBDLLHOOKSTRUCT));
                    uint vk = hookStruct.vkCode;

                    // 1. Alarm Active: Pressing ANY key on keyboard immediately dismisses the alarm
                    if (IsAlarmActive != null && IsAlarmActive())
                    {
                        if (isKeyDown)
                        {
                            _suppressStartMenuOnWinUp = true;
                            DispatchAction(() => OnDismissAlarmRequested?.Invoke());
                            return (IntPtr)1;
                        }
                    }

                    // Safety check: verify if Windows key is physically held down (skip check while holding Up/Down)
                    if (_isWinDown && !_isUpHeld && !_isDownHeld && vk != Win32Api.VK_LWIN && vk != Win32Api.VK_RWIN)
                    {
                        bool isWinPhysicallyDown = (Win32Api.GetAsyncKeyState(Win32Api.VK_LWIN) & 0x8000) != 0
                                                || (Win32Api.GetAsyncKeyState(Win32Api.VK_RWIN) & 0x8000) != 0;
                        if (!isWinPhysicallyDown)
                        {
                            _isWinDown = false;
                        }
                    }

                    // Track Windows key (Left or Right)
                    if (vk == Win32Api.VK_LWIN || vk == Win32Api.VK_RWIN)
                    {
                        if (isKeyDown)
                        {
                            _isWinDown = true;
                        }
                        else if (isKeyUp)
                        {
                            _isWinDown = false;
                            DispatchAction(() => OnWinKeyReleased?.Invoke());

                            if (_suppressStartMenuOnWinUp)
                            {
                                _suppressStartMenuOnWinUp = false;
                                try
                                {
                                    // Send dummy unassigned key 0xE8 to notify Windows OS a key was pressed with Win,
                                    // suppressing Start Menu popup ONLY when a hotkey swallowed its key (e.g. Esc).
                                    Win32Api.keybd_event(0xE8, 0, 0, UIntPtr.Zero);
                                    Win32Api.keybd_event(0xE8, 0, Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
                                }
                                catch { }
                            }

                            // Always forward Win KeyUp so other apps (OBS) track modifier keys cleanly without stuck state
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                    }

                    // Read real-time state of physical modifier keys
                    bool currentWin = _isWinDown
                                   || (Win32Api.GetAsyncKeyState(Win32Api.VK_LWIN) & 0x8000) != 0
                                   || (Win32Api.GetAsyncKeyState(Win32Api.VK_RWIN) & 0x8000) != 0;
                    bool currentCtrl = (Win32Api.GetAsyncKeyState(Win32Api.VK_CONTROL) & 0x8000) != 0;
                    bool currentAlt = (Win32Api.GetAsyncKeyState(Win32Api.VK_MENU) & 0x8000) != 0;
                    bool currentShift = (Win32Api.GetAsyncKeyState(Win32Api.VK_SHIFT) & 0x8000) != 0;

                    // Hotkey matching checking
                    if (isKeyDown)
                    {
                        if (CheckHotkeyMatch(_settings, "IntervalTimer", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            _suppressNextEscUp = true;
                            _suppressStartMenuOnWinUp = true;
                            DispatchAction(() => OnIntervalToggleRequested?.Invoke());
                            return (IntPtr)1;
                        }
                        else if (CheckHotkeyMatch(_settings, "ToggleHUD", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            DispatchAction(() => OnToggleRequested?.Invoke());
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        else if (CheckHotkeyMatch(_settings, "PauseToggle", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            // Instant non-blocking passthrough: fire action in background, immediately forward key to OBS
                            DispatchAction(() => OnPauseToggleRequested?.Invoke());
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        else if (CheckHotkeyMatch(_settings, "Reset", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            DispatchAction(() => OnResetRequested?.Invoke());
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        else if (CheckHotkeyMatch(_settings, "SaveCountdown", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            DispatchAction(() => OnSaveRequested?.Invoke());
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        else if (CheckHotkeyMatch(_settings, "SwitchMode", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            _suppressNextEscUp = true;
                            _suppressStartMenuOnWinUp = true;
                            DispatchAction(() => OnSwitchModeRequested?.Invoke());
                            return (IntPtr)1;
                        }
                        else if (CheckHotkeyMatch(_settings, "NewInstance", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            DispatchAction(() => OnNewInstanceRequested?.Invoke());
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        else if (CheckHotkeyMatch(_settings, "CloseInstance", vk, currentWin, currentCtrl, currentAlt, currentShift) || (currentWin && vk == Win32Api.VK_DELETE))
                        {
                            DispatchAction(() => OnCloseInstanceRequested?.Invoke());
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        else if (CheckHotkeyMatch(_settings, "AdjustUp", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            if (!_isUpHeld)
                            {
                                _isUpHeld = true;
                                DispatchAction(() => OnAdjustUpStart?.Invoke());
                            }
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        else if (CheckHotkeyMatch(_settings, "AdjustDown", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            if (!_isDownHeld)
                            {
                                _isDownHeld = true;
                                DispatchAction(() => OnAdjustDownStart?.Invoke());
                            }
                            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                    }
                    else if (isKeyUp)
                    {
                        uint kEsc = _settings != null ? _settings.KeySwitchMode : Win32Api.VK_ESCAPE;
                        uint kEsc2 = (_settings != null && _settings.KeySwitchMode_2_Enabled) ? _settings.KeySwitchMode_2 : 0;
                        uint kInterval = _settings != null ? _settings.KeyIntervalTimer : Win32Api.VK_ESCAPE;
                        uint kInterval2 = (_settings != null && _settings.KeyIntervalTimer_2_Enabled) ? _settings.KeyIntervalTimer_2 : 0;

                        if ((vk == Win32Api.VK_ESCAPE || vk == kEsc || (kEsc2 != 0 && vk == kEsc2) || vk == kInterval || (kInterval2 != 0 && vk == kInterval2)) && _suppressNextEscUp)
                        {
                            _suppressNextEscUp = false;
                            return (IntPtr)1;
                        }

                        uint kUp = _settings != null ? _settings.KeyAdjustUp : Win32Api.VK_UP;
                        uint kUp2 = (_settings != null && _settings.KeyAdjustUp_2_Enabled) ? _settings.KeyAdjustUp_2 : 0;
                        uint kDown = _settings != null ? _settings.KeyAdjustDown : Win32Api.VK_DOWN;
                        uint kDown2 = (_settings != null && _settings.KeyAdjustDown_2_Enabled) ? _settings.KeyAdjustDown_2 : 0;

                        if ((vk == kUp || (kUp2 != 0 && vk == kUp2)) && _isUpHeld)
                        {
                            _isUpHeld = false;
                            DispatchAction(() => OnAdjustUpStop?.Invoke());
                        }
                        else if ((vk == kDown || (kDown2 != 0 && vk == kDown2)) && _isDownHeld)
                        {
                            _isDownHeld = false;
                            DispatchAction(() => OnAdjustDownStop?.Invoke());
                        }
                    }
                    else
                    {
                        // Clean up release flags if Win was released first
                        if (isKeyUp)
                        {
                            uint kUp = _settings != null ? _settings.KeyAdjustUp : Win32Api.VK_UP;
                            uint kUp2 = (_settings != null && _settings.KeyAdjustUp_2_Enabled) ? _settings.KeyAdjustUp_2 : 0;
                            uint kDown = _settings != null ? _settings.KeyAdjustDown : Win32Api.VK_DOWN;
                            uint kDown2 = (_settings != null && _settings.KeyAdjustDown_2_Enabled) ? _settings.KeyAdjustDown_2 : 0;

                            if ((vk == Win32Api.VK_UP || vk == kUp || (kUp2 != 0 && vk == kUp2)) && _isUpHeld)
                            {
                                _isUpHeld = false;
                                DispatchAction(() => OnAdjustUpStop?.Invoke());
                            }
                            else if ((vk == Win32Api.VK_DOWN || vk == kDown || (kDown2 != 0 && vk == kDown2)) && _isDownHeld)
                            {
                                _isDownHeld = false;
                                DispatchAction(() => OnAdjustDownStop?.Invoke());
                            }
                        }
                    }
                }
                catch { }
            }

            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool CheckHotkeyMatch(SettingsManager settings, string name, uint vk, bool isWin, bool isCtrl, bool isAlt, bool isShift)
        {
            if (settings == null) return false;

            // 1. Check Primary Shortcut
            if (GetHotkeyDef(settings, name, 1, out bool reqWin1, out bool reqCtrl1, out bool reqAlt1, out bool reqShift1, out uint reqVk1))
            {
                if (vk == reqVk1 && isWin == reqWin1 && isCtrl == reqCtrl1 && isAlt == reqAlt1 && isShift == reqShift1)
                    return true;
            }

            // 2. Check Secondary Shortcut (if enabled)
            if (GetHotkeyDef(settings, name, 2, out bool reqWin2, out bool reqCtrl2, out bool reqAlt2, out bool reqShift2, out uint reqVk2))
            {
                if (vk == reqVk2 && isWin == reqWin2 && isCtrl == reqCtrl2 && isAlt == reqAlt2 && isShift == reqShift2)
                    return true;
            }

            return false;
        }

        private bool GetHotkeyDef(SettingsManager settings, string name, int index, out bool win, out bool ctrl, out bool alt, out bool shift, out uint vk)
        {
            win = false; ctrl = false; alt = false; shift = false; vk = 0;
            if (index == 1)
            {
                switch (name)
                {
                    case "ToggleHUD": win = settings.KeyToggleHUD_Win; ctrl = settings.KeyToggleHUD_Ctrl; alt = settings.KeyToggleHUD_Alt; shift = settings.KeyToggleHUD_Shift; vk = settings.KeyToggleHUD; return true;
                    case "IntervalTimer": win = settings.KeyIntervalTimer_Win; ctrl = settings.KeyIntervalTimer_Ctrl; alt = settings.KeyIntervalTimer_Alt; shift = settings.KeyIntervalTimer_Shift; vk = settings.KeyIntervalTimer; return true;
                    case "PauseToggle": win = settings.KeyPauseToggle_Win; ctrl = settings.KeyPauseToggle_Ctrl; alt = settings.KeyPauseToggle_Alt; shift = settings.KeyPauseToggle_Shift; vk = settings.KeyPauseToggle; return true;
                    case "Reset": win = settings.KeyReset_Win; ctrl = settings.KeyReset_Ctrl; alt = settings.KeyReset_Alt; shift = settings.KeyReset_Shift; vk = settings.KeyReset; return true;
                    case "SaveCountdown": win = settings.KeySaveCountdown_Win; ctrl = settings.KeySaveCountdown_Ctrl; alt = settings.KeySaveCountdown_Alt; shift = settings.KeySaveCountdown_Shift; vk = settings.KeySaveCountdown; return true;
                    case "SwitchMode": win = settings.KeySwitchMode_Win; ctrl = settings.KeySwitchMode_Ctrl; alt = settings.KeySwitchMode_Alt; shift = settings.KeySwitchMode_Shift; vk = settings.KeySwitchMode; return true;
                    case "NewInstance": win = settings.KeyNewInstance_Win; ctrl = settings.KeyNewInstance_Ctrl; alt = settings.KeyNewInstance_Alt; shift = settings.KeyNewInstance_Shift; vk = settings.KeyNewInstance; return true;
                    case "CloseInstance": win = settings.KeyCloseInstance_Win; ctrl = settings.KeyCloseInstance_Ctrl; alt = settings.KeyCloseInstance_Alt; shift = settings.KeyCloseInstance_Shift; vk = settings.KeyCloseInstance; return true;
                    case "AdjustUp": win = settings.KeyAdjustUp_Win; ctrl = settings.KeyAdjustUp_Ctrl; alt = settings.KeyAdjustUp_Alt; shift = settings.KeyAdjustUp_Shift; vk = settings.KeyAdjustUp; return true;
                    case "AdjustDown": win = settings.KeyAdjustDown_Win; ctrl = settings.KeyAdjustDown_Ctrl; alt = settings.KeyAdjustDown_Alt; shift = settings.KeyAdjustDown_Shift; vk = settings.KeyAdjustDown; return true;
                }
            }
            else if (index == 2)
            {
                switch (name)
                {
                    case "ToggleHUD": if (!settings.KeyToggleHUD_2_Enabled) return false; win = settings.KeyToggleHUD_2_Win; ctrl = settings.KeyToggleHUD_2_Ctrl; alt = settings.KeyToggleHUD_2_Alt; shift = settings.KeyToggleHUD_2_Shift; vk = settings.KeyToggleHUD_2; return true;
                    case "IntervalTimer": if (!settings.KeyIntervalTimer_2_Enabled) return false; win = settings.KeyIntervalTimer_2_Win; ctrl = settings.KeyIntervalTimer_2_Ctrl; alt = settings.KeyIntervalTimer_2_Alt; shift = settings.KeyIntervalTimer_2_Shift; vk = settings.KeyIntervalTimer_2; return true;
                    case "PauseToggle": if (!settings.KeyPauseToggle_2_Enabled) return false; win = settings.KeyPauseToggle_2_Win; ctrl = settings.KeyPauseToggle_2_Ctrl; alt = settings.KeyPauseToggle_2_Alt; shift = settings.KeyPauseToggle_2_Shift; vk = settings.KeyPauseToggle_2; return true;
                    case "Reset": if (!settings.KeyReset_2_Enabled) return false; win = settings.KeyReset_2_Win; ctrl = settings.KeyReset_2_Ctrl; alt = settings.KeyReset_2_Alt; shift = settings.KeyReset_2_Shift; vk = settings.KeyReset_2; return true;
                    case "SaveCountdown": if (!settings.KeySaveCountdown_2_Enabled) return false; win = settings.KeySaveCountdown_2_Win; ctrl = settings.KeySaveCountdown_2_Ctrl; alt = settings.KeySaveCountdown_2_Alt; shift = settings.KeySaveCountdown_2_Shift; vk = settings.KeySaveCountdown_2; return true;
                    case "SwitchMode": if (!settings.KeySwitchMode_2_Enabled) return false; win = settings.KeySwitchMode_2_Win; ctrl = settings.KeySwitchMode_2_Ctrl; alt = settings.KeySwitchMode_2_Alt; shift = settings.KeySwitchMode_2_Shift; vk = settings.KeySwitchMode_2; return true;
                    case "NewInstance": if (!settings.KeyNewInstance_2_Enabled) return false; win = settings.KeyNewInstance_2_Win; ctrl = settings.KeyNewInstance_2_Ctrl; alt = settings.KeyNewInstance_2_Alt; shift = settings.KeyNewInstance_2_Shift; vk = settings.KeyNewInstance_2; return true;
                    case "CloseInstance": if (!settings.KeyCloseInstance_2_Enabled) return false; win = settings.KeyCloseInstance_2_Win; ctrl = settings.KeyCloseInstance_2_Ctrl; alt = settings.KeyCloseInstance_2_Alt; shift = settings.KeyCloseInstance_2_Shift; vk = settings.KeyCloseInstance_2; return true;
                    case "AdjustUp": if (!settings.KeyAdjustUp_2_Enabled) return false; win = settings.KeyAdjustUp_2_Win; ctrl = settings.KeyAdjustUp_2_Ctrl; alt = settings.KeyAdjustUp_2_Alt; shift = settings.KeyAdjustUp_2_Shift; vk = settings.KeyAdjustUp_2; return true;
                    case "AdjustDown": if (!settings.KeyAdjustDown_2_Enabled) return false; win = settings.KeyAdjustDown_2_Win; ctrl = settings.KeyAdjustDown_2_Ctrl; alt = settings.KeyAdjustDown_2_Alt; shift = settings.KeyAdjustDown_2_Shift; vk = settings.KeyAdjustDown_2; return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            _isRunning = false;
            StopRetry();
            if (_hookThreadId != 0)
            {
                try
                {
                    Win32Api.PostThreadMessage(_hookThreadId, Win32Api.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
                }
                catch { }
            }
            if (_hookThread != null && _hookThread.IsAlive)
            {
                try
                {
                    _hookThread.Join(500);
                }
                catch { }
            }
            UninstallHook();
        }
    }
}
