using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TimeBomb.Core
{
    public class LowLevelMouseHook : IDisposable
    {
        private IntPtr _hookId = IntPtr.Zero;
        private readonly Win32Api.LowLevelMouseProc _proc;

        /// <summary>
        /// Callback taking (screenX, screenY). Returns true if the middle click should be suppressed (e.g. Ctrl + Middle Click to unlock Click Through).
        /// </summary>
        public Func<int, int, bool> OnMiddleClickCheck { get; set; }

        private bool _suppressNextMiddleUp = false;

        public LowLevelMouseHook()
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
                    _hookId = Win32Api.SetWindowsHookEx(Win32Api.WH_MOUSE_LL, _proc, moduleHandle, 0);
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
                    if (msg == Win32Api.WM_MBUTTONDOWN)
                    {
                        var hookStruct = (Win32Api.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Api.MSLLHOOKSTRUCT));
                        if (OnMiddleClickCheck != null && OnMiddleClickCheck(hookStruct.pt.X, hookStruct.pt.Y))
                        {
                            _suppressNextMiddleUp = true;
                            return (IntPtr)1; // Suppress middle-click so underlying window does not receive it
                        }
                    }
                    else if (msg == Win32Api.WM_MBUTTONUP && _suppressNextMiddleUp)
                    {
                        _suppressNextMiddleUp = false;
                        return (IntPtr)1; // Suppress corresponding up event
                    }
                }
                catch { }
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
