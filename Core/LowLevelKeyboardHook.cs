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

        public LowLevelKeyboardHook()
        {
            _proc = HookCallback;
            InstallHook();
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
            if (nCode >= 0)
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

                // Hotkey combinations with Win key
                if (_isWinDown)
                {
                    if (isKeyDown)
                    {
                        switch (vk)
                        {
                            case Win32Api.VK_OEM_3: // ` or ~
                                _shortcutExecuted = true;
                                bool isCtrl = (Win32Api.GetAsyncKeyState(Win32Api.VK_CONTROL) & 0x8000) != 0;
                                if (isCtrl)
                                {
                                    OnIntervalToggleRequested?.Invoke();
                                }
                                else
                                {
                                    OnToggleRequested?.Invoke();
                                }
                                ForceReleaseWinKey();
                                return (IntPtr)1;

                            case Win32Api.VK_SPACE:  // Space
                                _shortcutExecuted = true;
                                OnPauseToggleRequested?.Invoke();
                                ForceReleaseWinKey();
                                return (IntPtr)1;

                            case Win32Api.VK_BACK: // Backspace
                            case Win32Api.VK_R:    // R
                                _shortcutExecuted = true;
                                OnResetRequested?.Invoke();
                                ForceReleaseWinKey();
                                return (IntPtr)1;

                            case Win32Api.VK_S: // S
                                _shortcutExecuted = true;
                                OnSaveRequested?.Invoke();
                                ForceReleaseWinKey();
                                return (IntPtr)1;

                            case Win32Api.VK_ESCAPE: // Esc
                                _shortcutExecuted = true;
                                OnSwitchModeRequested?.Invoke();
                                ForceReleaseWinKey();
                                return (IntPtr)1;

                            case Win32Api.VK_N: // N (New Timer)
                                _shortcutExecuted = true;
                                OnNewInstanceRequested?.Invoke();
                                ForceReleaseWinKey();
                                return (IntPtr)1;

                            case Win32Api.VK_W:      // W (Close Timer)
                            case Win32Api.VK_DELETE: // Delete
                                _shortcutExecuted = true;
                                OnCloseInstanceRequested?.Invoke();
                                ForceReleaseWinKey();
                                return (IntPtr)1;

                            case Win32Api.VK_UP: // Up arrow
                                if (!_isUpHeld)
                                {
                                    _isUpHeld = true;
                                    _shortcutExecuted = true;
                                    OnAdjustUpStart?.Invoke();
                                }
                                return (IntPtr)1;

                            case Win32Api.VK_DOWN: // Down arrow
                                if (!_isDownHeld)
                                {
                                    _isDownHeld = true;
                                    _shortcutExecuted = true;
                                    OnAdjustDownStart?.Invoke();
                                }
                                return (IntPtr)1;
                        }
                    }
                    else if (isKeyUp)
                    {
                        if (vk == Win32Api.VK_UP && _isUpHeld)
                        {
                            _isUpHeld = false;
                            OnAdjustUpStop?.Invoke();
                            ForceReleaseWinKey();
                            return (IntPtr)1;
                        }
                        else if (vk == Win32Api.VK_DOWN && _isDownHeld)
                        {
                            _isDownHeld = false;
                            OnAdjustDownStop?.Invoke();
                            ForceReleaseWinKey();
                            return (IntPtr)1;
                        }
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

            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
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
