using System;
using System.Runtime.InteropServices;

namespace TimeBomb.Core
{
    #region COM Interfaces for Windows 10 Virtual Desktops
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    internal interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4")]
    internal interface IVirtualDesktop
    {
        bool IsViewVisible(object view);
        Guid GetId();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    internal interface IObjectArray
    {
        void GetCount(out int count);
        void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6")]
    internal interface IVirtualDesktopManagerInternal
    {
        int GetCount();
        void MoveViewToDesktop(object view, IVirtualDesktop desktop);
        bool CanViewMoveDesktops(object view);
        IVirtualDesktop GetCurrentDesktop();
        void GetDesktops(out IObjectArray desktops);
        [PreserveSig]
        int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
        void SwitchDesktop(IVirtualDesktop desktop);
        IVirtualDesktop CreateDesktop();
        void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
        IVirtualDesktop FindDesktop(ref Guid desktopid);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
    internal interface IApplicationViewCollection
    {
        int GetViews(out IObjectArray array);
        int GetViewsByZOrder(out IObjectArray array);
        int GetViewsByAppUserModelId(string id, out IObjectArray array);
        int GetViewForHwnd(IntPtr hwnd, [MarshalAs(UnmanagedType.IUnknown)] out object view);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    internal interface IVirtualDesktopManager
    {
        bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
        Guid GetWindowDesktopId(IntPtr topLevelWindow);
        void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }
    #endregion

    public static class VirtualDesktopManagerHelper
    {
        private static readonly Guid CLSID_ImmersiveShell = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        private static readonly Guid CLSID_VirtualDesktopManagerInternal = new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
        private static readonly Guid CLSID_VirtualDesktopManager = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");

        private static IVirtualDesktopManagerInternal _internalManager;
        private static IVirtualDesktopManager _publicManager;
        private static IApplicationViewCollection _viewCollection;
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;
                try
                {
                    var shellType = Type.GetTypeFromCLSID(CLSID_ImmersiveShell);
                    if (shellType != null)
                    {
                        var shell = (IServiceProvider10)Activator.CreateInstance(shellType);
                        if (shell != null)
                        {
                            Guid internalGuid = typeof(IVirtualDesktopManagerInternal).GUID;
                            Guid serviceGuid = CLSID_VirtualDesktopManagerInternal;
                            object internalObj = shell.QueryService(ref serviceGuid, ref internalGuid);
                            _internalManager = internalObj as IVirtualDesktopManagerInternal;

                            Guid viewCollectionGuid = typeof(IApplicationViewCollection).GUID;
                            object viewCollectionObj = shell.QueryService(ref viewCollectionGuid, ref viewCollectionGuid);
                            _viewCollection = viewCollectionObj as IApplicationViewCollection;
                        }
                    }

                    var publicType = Type.GetTypeFromCLSID(CLSID_VirtualDesktopManager);
                    if (publicType != null)
                    {
                        _publicManager = Activator.CreateInstance(publicType) as IVirtualDesktopManager;
                    }
                }
                catch { }
                finally
                {
                    _initialized = true;
                }
            }
        }

        /// <summary>
        /// Moves the window to the next virtual desktop (creating one if none exists), and switches to that desktop.
        /// </summary>
        public static bool MoveWindowToNextDesktop(IntPtr hWnd)
        {
            EnsureInitialized();

            if (_internalManager != null)
            {
                try
                {
                    int count = _internalManager.GetCount();
                    IVirtualDesktop current = _internalManager.GetCurrentDesktop();
                    if (current != null)
                    {
                        IVirtualDesktop targetDesktop = null;

                        // Check if adjacent right desktop exists (direction = 4 is right)
                        int hr = _internalManager.GetAdjacentDesktop(current, 4, out targetDesktop);
                        if (hr != 0 || targetDesktop == null)
                        {
                            // If no next desktop to the right, create a new virtual desktop
                            targetDesktop = _internalManager.CreateDesktop();
                        }

                        if (targetDesktop != null)
                        {
                            // 1. Move window to the target desktop
                            Guid targetGuid = targetDesktop.GetId();
                            bool moved = false;

                            if (_publicManager != null)
                            {
                                try
                                {
                                    _publicManager.MoveWindowToDesktop(hWnd, ref targetGuid);
                                    moved = true;
                                }
                                catch { }
                            }

                            if (!moved && _viewCollection != null)
                            {
                                try
                                {
                                    if (_viewCollection.GetViewForHwnd(hWnd, out object appView) == 0 && appView != null)
                                    {
                                        _internalManager.MoveViewToDesktop(appView, targetDesktop);
                                        moved = true;
                                    }
                                }
                                catch { }
                            }

                            // 2. Switch Windows view to that desktop
                            _internalManager.SwitchDesktop(targetDesktop);
                            return true;
                        }
                    }
                }
                catch { }
            }

            // Fallback for systems where internal COM might differ: simulate Win+Ctrl+D then Win+Ctrl+Right
            FallbackSendKeys();
            return false;
        }

        private static void FallbackSendKeys()
        {
            try
            {
                // Simulate Win + Ctrl + D (Create & switch to new virtual desktop)
                Win32Api.keybd_event((byte)Win32Api.VK_LWIN, 0, 0, UIntPtr.Zero);
                Win32Api.keybd_event((byte)Win32Api.VK_CONTROL, 0, 0, UIntPtr.Zero);
                Win32Api.keybd_event((byte)0x44, 0, 0, UIntPtr.Zero); // 0x44 = D
                Win32Api.keybd_event((byte)0x44, 0, Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
                Win32Api.keybd_event((byte)Win32Api.VK_CONTROL, 0, Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
                Win32Api.keybd_event((byte)Win32Api.VK_LWIN, 0, Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }
    }
}
