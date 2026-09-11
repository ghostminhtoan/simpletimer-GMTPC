using System;
using System.IO;
using Microsoft.Win32;

namespace TimeBomb.Core
{
    public static class StartupManager
    {
        private const string AppName = "TimeBomb";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(AppName);
                        return value != null;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key == null) return false;

                    if (enable)
                    {
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            key.SetValue(AppName, $"\"{exePath}\"");
                            return true;
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
