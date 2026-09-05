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

        public bool IsWinKeyHeld => _isWinDown;
        public bool ShortcutExecuted => _shortcutExecuted;

        public LowLevelKeyboardHook()
        {
            _proc = HookCallback;
            InstallHook();
        }

        private void InstallHook()
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr moduleHandle = Win32Api.GetModuleHandle(curModule.ModuleName);
                _hookId = Win32Api.SetWindowsHookEx(Win32Api.WH_KEYBOARD_LL, _proc, moduleHandle, 0);
            }
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
                                OnToggleRequested?.Invoke();
                                return (IntPtr)1;

                            case Win32Api.VK_RETURN: // Enter
                                _shortcutExecuted = true;
                                OnPauseToggleRequested?.Invoke();
                                return (IntPtr)1;

                            case Win32Api.VK_BACK: // Backspace
                                _shortcutExecuted = true;
                                OnResetRequested?.Invoke();
                                return (IntPtr)1;

                            case Win32Api.VK_S: // S
                                _shortcutExecuted = true;
                                OnSaveRequested?.Invoke();
                                return (IntPtr)1;

                            case Win32Api.VK_ESCAPE: // Esc
                                _shortcutExecuted = true;
                                OnSwitchModeRequested?.Invoke();
                                return (IntPtr)1;

                            case Win32Api.VK_N: // N (New Timer)
                                _shortcutExecuted = true;
                                OnNewInstanceRequested?.Invoke();
                                return (IntPtr)1;

                            case Win32Api.VK_W: // W (Close Timer)
                                _shortcutExecuted = true;
                                OnCloseInstanceRequested?.Invoke();
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
                            return (IntPtr)1;
                        }
                        else if (vk == Win32Api.VK_DOWN && _isDownHeld)
                        {
                            _isDownHeld = false;
                            OnAdjustDownStop?.Invoke();
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
                        }
                        else if (vk == Win32Api.VK_DOWN && _isDownHeld)
                        {
                            _isDownHeld = false;
                            OnAdjustDownStop?.Invoke();
                        }
                    }
                }
            }

            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
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
