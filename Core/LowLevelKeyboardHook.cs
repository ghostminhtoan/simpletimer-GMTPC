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

        public LowLevelKeyboardHook(SettingsManager settings = null)
        {
            _settings = settings;
            _proc = HookCallback;
            InstallHook();
        }

        public void SetSettings(SettingsManager settings)
        {
            _settings = settings;
        }

        private void InstallHook()
        {
            try
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    IntPtr moduleHandle = Win32Api.GetModuleHandle(curModule.ModuleName);
                    _hookId = Win32Api.SetWindowsHookEx(Win32Api.WH_KEYBOARD_LL, _proc, moduleHandle, 0);
                }
            }
            catch { }
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
                            OnDismissAlarmRequested?.Invoke();
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
                            if (_shortcutExecuted)
                            {
                                _shortcutExecuted = false;
                                OnWinKeyReleased?.Invoke();
                                // Suppress Win key release so Start Menu does not open
                                return (IntPtr)1;
                            }
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
                        if (CheckHotkeyMatch(_settings, "ToggleHUD", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            _shortcutExecuted = true;
                            if (currentCtrl)
                            {
                                OnIntervalToggleRequested?.Invoke();
                            }
                            else
                            {
                                OnToggleRequested?.Invoke();
                            }
                            ForceReleaseWinKey();
                        }
                        else if (CheckHotkeyMatch(_settings, "PauseToggle", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            _shortcutExecuted = true;
                            OnPauseToggleRequested?.Invoke();
                            ForceReleaseWinKey();
                        }
                        else if (CheckHotkeyMatch(_settings, "Reset", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            _shortcutExecuted = true;
                            OnResetRequested?.Invoke();
                            ForceReleaseWinKey();
                        }
                        else if (CheckHotkeyMatch(_settings, "SaveCountdown", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            _shortcutExecuted = true;
                            OnSaveRequested?.Invoke();
                            ForceReleaseWinKey();
                        }
                        else if (CheckHotkeyMatch(_settings, "SwitchMode", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            _shortcutExecuted = true;
                            OnSwitchModeRequested?.Invoke();
                            ForceReleaseWinKey();
                        }
                        else if (CheckHotkeyMatch(_settings, "NewInstance", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            _shortcutExecuted = true;
                            OnNewInstanceRequested?.Invoke();
                            ForceReleaseWinKey();
                        }
                        else if (CheckHotkeyMatch(_settings, "CloseInstance", vk, currentWin, currentCtrl, currentAlt, currentShift) || (currentWin && vk == Win32Api.VK_DELETE))
                        {
                            _shortcutExecuted = true;
                            OnCloseInstanceRequested?.Invoke();
                            ForceReleaseWinKey();
                        }
                        else if (CheckHotkeyMatch(_settings, "AdjustUp", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            if (!_isUpHeld)
                            {
                                _isUpHeld = true;
                                _shortcutExecuted = true;
                                OnAdjustUpStart?.Invoke();
                            }
                        }
                        else if (CheckHotkeyMatch(_settings, "AdjustDown", vk, currentWin, currentCtrl, currentAlt, currentShift))
                        {
                            if (!_isDownHeld)
                            {
                                _isDownHeld = true;
                                _shortcutExecuted = true;
                                OnAdjustDownStart?.Invoke();
                            }
                        }
                    }
                    else if (isKeyUp)
                    {
                        uint kUp = _settings != null ? _settings.KeyAdjustUp : Win32Api.VK_UP;
                        uint kDown = _settings != null ? _settings.KeyAdjustDown : Win32Api.VK_DOWN;

                        if (vk == kUp && _isUpHeld)
                        {
                            _isUpHeld = false;
                            OnAdjustUpStop?.Invoke();
                            ForceReleaseWinKey();
                        }
                        else if (vk == kDown && _isDownHeld)
                        {
                            _isDownHeld = false;
                            OnAdjustDownStop?.Invoke();
                            ForceReleaseWinKey();
                        }
                    }
                    else
                    {
                        // Clean up release flags if Win was released first
                        if (isKeyUp)
                        {
                            if (vk == Win32Api.VK_UP && _isUpHeld)
                            {
                                _isUpHeld = false;
                                OnAdjustUpStop?.Invoke();
                                ForceReleaseWinKey();
                            }
                            else if (vk == Win32Api.VK_DOWN && _isDownHeld)
                            {
                                _isDownHeld = false;
                                OnAdjustDownStop?.Invoke();
                                ForceReleaseWinKey();
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

            bool reqWin = false, reqCtrl = false, reqAlt = false, reqShift = false;
            uint reqVk = 0;

            switch (name)
            {
                case "ToggleHUD": reqWin = settings.KeyToggleHUD_Win; reqCtrl = settings.KeyToggleHUD_Ctrl; reqAlt = settings.KeyToggleHUD_Alt; reqShift = settings.KeyToggleHUD_Shift; reqVk = settings.KeyToggleHUD; break;
                case "PauseToggle": reqWin = settings.KeyPauseToggle_Win; reqCtrl = settings.KeyPauseToggle_Ctrl; reqAlt = settings.KeyPauseToggle_Alt; reqShift = settings.KeyPauseToggle_Shift; reqVk = settings.KeyPauseToggle; break;
                case "Reset": reqWin = settings.KeyReset_Win; reqCtrl = settings.KeyReset_Ctrl; reqAlt = settings.KeyReset_Alt; reqShift = settings.KeyReset_Shift; reqVk = settings.KeyReset; break;
                case "SaveCountdown": reqWin = settings.KeySaveCountdown_Win; reqCtrl = settings.KeySaveCountdown_Ctrl; reqAlt = settings.KeySaveCountdown_Alt; reqShift = settings.KeySaveCountdown_Shift; reqVk = settings.KeySaveCountdown; break;
                case "SwitchMode": reqWin = settings.KeySwitchMode_Win; reqCtrl = settings.KeySwitchMode_Ctrl; reqAlt = settings.KeySwitchMode_Alt; reqShift = settings.KeySwitchMode_Shift; reqVk = settings.KeySwitchMode; break;
                case "NewInstance": reqWin = settings.KeyNewInstance_Win; reqCtrl = settings.KeyNewInstance_Ctrl; reqAlt = settings.KeyNewInstance_Alt; reqShift = settings.KeyNewInstance_Shift; reqVk = settings.KeyNewInstance; break;
                case "CloseInstance": reqWin = settings.KeyCloseInstance_Win; reqCtrl = settings.KeyCloseInstance_Ctrl; reqAlt = settings.KeyCloseInstance_Alt; reqShift = settings.KeyCloseInstance_Shift; reqVk = settings.KeyCloseInstance; break;
                case "AdjustUp": reqWin = settings.KeyAdjustUp_Win; reqCtrl = settings.KeyAdjustUp_Ctrl; reqAlt = settings.KeyAdjustUp_Alt; reqShift = settings.KeyAdjustUp_Shift; reqVk = settings.KeyAdjustUp; break;
                case "AdjustDown": reqWin = settings.KeyAdjustDown_Win; reqCtrl = settings.KeyAdjustDown_Ctrl; reqAlt = settings.KeyAdjustDown_Alt; reqShift = settings.KeyAdjustDown_Shift; reqVk = settings.KeyAdjustDown; break;
            }

            return vk == reqVk && isWin == reqWin && isCtrl == reqCtrl && isAlt == reqAlt && isShift == reqShift;
        }

        private void ForceReleaseWinKey()
        {
            _isWinDown = false;
            _shortcutExecuted = false;

            try
            {
                // Send dummy unassigned key 0xE8 to tell Windows OS a key was pressed with Win,
                // suppressing Start Menu popup without corrupting physical Win key state.
                Win32Api.keybd_event(0xE8, 0, 0, UIntPtr.Zero);
                Win32Api.keybd_event(0xE8, 0, Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                Win32Api.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }
}
