using System;
using System.Collections.Generic;
using System.IO;

namespace TimeBomb.Core
{
    public class SettingsManager
    {
        private static readonly object _fileLock = new object();
        private readonly string _iniPath;
        private readonly Dictionary<string, Dictionary<string, string>> _data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public int InstanceId { get; set; } = 1;
        public int WindowX { get; set; } = 150;
        public int WindowY { get; set; } = 150;
        public int WindowWidth { get; set; } = 180;
        public int WindowHeight { get; set; } = 82;
        public string Mode { get; set; } = "timer";
        public int LastSetMinutes { get; set; } = 3;
        public int LastSetSeconds { get; set; } = 0;
        public bool ClickThrough { get; set; } = false;
        public bool ShowSubInfo { get; set; } = true;
        public bool ObsHideStream { get; set; } = false;
        public bool ShowInTaskbar { get; set; } = false;
        public bool MinimizeKeepWorking { get; set; } = false;
        public double Opacity { get; set; } = 1.0;
        public bool GamepadEnabled { get; set; } = true;
        public bool GamepadVibration { get; set; } = true;

        // Custom Hotkeys Settings (Modifiers + Key VK)
        public bool KeyToggleHUD_Win { get; set; } = true;
        public bool KeyToggleHUD_Ctrl { get; set; } = false;
        public bool KeyToggleHUD_Alt { get; set; } = false;
        public bool KeyToggleHUD_Shift { get; set; } = false;
        public uint KeyToggleHUD { get; set; } = Win32Api.VK_OEM_3; // `

        public bool KeyPauseToggle_Win { get; set; } = true;
        public bool KeyPauseToggle_Ctrl { get; set; } = false;
        public bool KeyPauseToggle_Alt { get; set; } = false;
        public bool KeyPauseToggle_Shift { get; set; } = false;
        public uint KeyPauseToggle { get; set; } = Win32Api.VK_SPACE; // Space

        public bool KeyReset_Win { get; set; } = true;
        public bool KeyReset_Ctrl { get; set; } = false;
        public bool KeyReset_Alt { get; set; } = false;
        public bool KeyReset_Shift { get; set; } = false;
        public uint KeyReset { get; set; } = Win32Api.VK_BACK; // Backspace

        public bool KeySaveCountdown_Win { get; set; } = true;
        public bool KeySaveCountdown_Ctrl { get; set; } = false;
        public bool KeySaveCountdown_Alt { get; set; } = false;
        public bool KeySaveCountdown_Shift { get; set; } = false;
        public uint KeySaveCountdown { get; set; } = Win32Api.VK_S; // S

        public bool KeySwitchMode_Win { get; set; } = true;
        public bool KeySwitchMode_Ctrl { get; set; } = false;
        public bool KeySwitchMode_Alt { get; set; } = false;
        public bool KeySwitchMode_Shift { get; set; } = false;
        public uint KeySwitchMode { get; set; } = Win32Api.VK_ESCAPE; // Esc

        public bool KeyNewInstance_Win { get; set; } = true;
        public bool KeyNewInstance_Ctrl { get; set; } = false;
        public bool KeyNewInstance_Alt { get; set; } = false;
        public bool KeyNewInstance_Shift { get; set; } = false;
        public uint KeyNewInstance { get; set; } = Win32Api.VK_N; // N

        public bool KeyCloseInstance_Win { get; set; } = true;
        public bool KeyCloseInstance_Ctrl { get; set; } = false;
        public bool KeyCloseInstance_Alt { get; set; } = false;
        public bool KeyCloseInstance_Shift { get; set; } = false;
        public uint KeyCloseInstance { get; set; } = Win32Api.VK_W; // W

        public bool KeyAdjustUp_Win { get; set; } = true;
        public bool KeyAdjustUp_Ctrl { get; set; } = false;
        public bool KeyAdjustUp_Alt { get; set; } = false;
        public bool KeyAdjustUp_Shift { get; set; } = false;
        public uint KeyAdjustUp { get; set; } = Win32Api.VK_UP; // Up

        public bool KeyAdjustDown_Win { get; set; } = true;
        public bool KeyAdjustDown_Ctrl { get; set; } = false;
        public bool KeyAdjustDown_Alt { get; set; } = false;
        public bool KeyAdjustDown_Shift { get; set; } = false;
        public uint KeyAdjustDown { get; set; } = Win32Api.VK_DOWN; // Down

        // Interval Timer settings
        public int IntervalPrepare { get; set; } = 5;
        public int IntervalWork { get; set; } = 30;
        public int IntervalRest { get; set; } = 10;
        public int IntervalEnd { get; set; } = 5;
        public int IntervalLoops { get; set; } = 3;
        public int IntervalWindowX { get; set; } = 250;
        public int IntervalWindowY { get; set; } = 250;

        public SettingsManager(int instanceId = 1)
        {
            InstanceId = instanceId;

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string portableDir = Path.Combine(appDir, ".portable");
            try
            {
                if (!Directory.Exists(portableDir))
                {
                    Directory.CreateDirectory(portableDir);
                }
            }
            catch { }

            _iniPath = Path.Combine(portableDir, "config.ini");

            // Migrate legacy gui_state/config.ini if .portable/config.ini doesn't exist yet
            try
            {
                if (!File.Exists(_iniPath))
                {
                    string legacyIni = Path.Combine(appDir, "gui_state", "config.ini");
                    if (File.Exists(legacyIni))
                    {
                        File.Copy(legacyIni, _iniPath, true);
                    }
                }
            }
            catch { }

            Load();
        }

        public void Load()
        {
            if (!File.Exists(_iniPath))
            {
                if (InstanceId > 1)
                {
                    WindowY += (InstanceId - 1) * 92;
                }
                return;
            }

            try
            {
                ParseIniFile();

                string sec = "Instance_" + InstanceId;
                if (_data.ContainsKey(sec))
                {
                    if (TryGetValue(sec, "x", out string xStr) && int.TryParse(xStr, out int x))
                        WindowX = x;
                    if (TryGetValue(sec, "y", out string yStr) && int.TryParse(yStr, out int y))
                        WindowY = y;
                    if (TryGetValue(sec, "width", out string wStr) && int.TryParse(wStr, out int wVal))
                        WindowWidth = Math.Max(60, Math.Min(1200, wVal));
                    if (TryGetValue(sec, "height", out string hStr) && int.TryParse(hStr, out int hVal))
                        WindowHeight = Math.Max(30, Math.Min(600, hVal));
                    if (TryGetValue(sec, "mode", out string mode))
                        Mode = mode.ToLowerInvariant();
                    if (TryGetValue(sec, "LastSetMinutes", out string minsStr) && int.TryParse(minsStr, out int mins))
                        LastSetMinutes = Math.Max(0, mins);
                    if (TryGetValue(sec, "LastSetSeconds", out string secsStr) && int.TryParse(secsStr, out int secs))
                        LastSetSeconds = Math.Max(0, Math.Min(59, secs));
                    if (LastSetMinutes == 0 && LastSetSeconds == 0)
                        LastSetMinutes = 3;
                    if (TryGetValue(sec, "click_through", out string ctStr) && bool.TryParse(ctStr, out bool ct))
                        ClickThrough = ct;
                    if (TryGetValue(sec, "show_sub_info", out string subStr) && bool.TryParse(subStr, out bool subVal))
                        ShowSubInfo = subVal;
                    if (TryGetValue(sec, "obs_hide_stream", out string obsStr) && bool.TryParse(obsStr, out bool obsVal))
                        ObsHideStream = obsVal;
                    if (TryGetValue(sec, "show_in_taskbar", out string tbStr) && bool.TryParse(tbStr, out bool tbVal))
                        ShowInTaskbar = tbVal;
                    if (TryGetValue(sec, "minimize_keep_working", out string mkStr) && bool.TryParse(mkStr, out bool mkVal))
                        MinimizeKeepWorking = mkVal;
                    if (TryGetValue(sec, "opacity", out string opStr) && double.TryParse(opStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double op))
                        Opacity = Math.Max(0.2, Math.Min(1.0, op));
                }
                else
                {
                    if (TryGetValue("Position", "x", out string xStr) && int.TryParse(xStr, out int x))
                        WindowX = x;
                    if (TryGetValue("Position", "y", out string yStr) && int.TryParse(yStr, out int y))
                        WindowY = y;
                    if (TryGetValue("General", "mode", out string mode))
                        Mode = mode.ToLowerInvariant();
                    if (TryGetValue("Timer", "LastSetMinutes", out string minsStr) && int.TryParse(minsStr, out int mins))
                        LastSetMinutes = Math.Max(0, mins);
                    if (TryGetValue("Timer", "LastSetSeconds", out string secsStr) && int.TryParse(secsStr, out int secs))
                        LastSetSeconds = Math.Max(0, Math.Min(59, secs));
                    if (LastSetMinutes == 0 && LastSetSeconds == 0)
                        LastSetMinutes = 3;
                    if (TryGetValue("Window", "click_through", out string ctStr2) && bool.TryParse(ctStr2, out bool ct2))
                        ClickThrough = ct2;
                    if (TryGetValue("Window", "opacity", out string opStr2) && double.TryParse(opStr2, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double op2))
                        Opacity = Math.Max(0.2, Math.Min(1.0, op2));

                    if (InstanceId > 1)
                    {
                        WindowY += (InstanceId - 1) * 92;
                    }
                }

                if (TryGetValue("Gamepad", "enabled", out string padEnabledStr) && bool.TryParse(padEnabledStr, out bool padEnabled))
                    GamepadEnabled = padEnabled;
                else
                    GamepadEnabled = true;

                if (TryGetValue("Gamepad", "vibration", out string padVibStr) && bool.TryParse(padVibStr, out bool padVib))
                    GamepadVibration = padVib;
                else
                    GamepadVibration = true;

                if (TryGetValue("Interval", "Prepare", out string prepStr) && int.TryParse(prepStr, out int prep))
                    IntervalPrepare = Math.Max(0, prep);
                if (TryGetValue("Interval", "Work", out string workStr) && int.TryParse(workStr, out int work))
                    IntervalWork = Math.Max(1, work);
                if (TryGetValue("Interval", "Rest", out string restStr) && int.TryParse(restStr, out int rest))
                    IntervalRest = Math.Max(0, rest);
                if (TryGetValue("Interval", "End", out string endStr) && int.TryParse(endStr, out int endVal))
                    IntervalEnd = Math.Max(0, endVal);
                if (TryGetValue("Interval", "Loops", out string loopsStr) && int.TryParse(loopsStr, out int loopsVal))
                    IntervalLoops = Math.Max(1, loopsVal);
                if (TryGetValue("Interval", "WindowX", out string ixStr) && int.TryParse(ixStr, out int ix))
                    IntervalWindowX = ix;
                if (TryGetValue("Interval", "WindowY", out string iyStr) && int.TryParse(iyStr, out int iy))
                    IntervalWindowY = iy;

                // Hotkeys Load
                bool w = KeyToggleHUD_Win, c = KeyToggleHUD_Ctrl, a = KeyToggleHUD_Alt, s = KeyToggleHUD_Shift; uint k = KeyToggleHUD;
                LoadHotkeyConfig("ToggleHUD", ref w, ref c, ref a, ref s, ref k);
                KeyToggleHUD_Win = w; KeyToggleHUD_Ctrl = c; KeyToggleHUD_Alt = a; KeyToggleHUD_Shift = s; KeyToggleHUD = k;

                w = KeyPauseToggle_Win; c = KeyPauseToggle_Ctrl; a = KeyPauseToggle_Alt; s = KeyPauseToggle_Shift; k = KeyPauseToggle;
                LoadHotkeyConfig("PauseToggle", ref w, ref c, ref a, ref s, ref k);
                KeyPauseToggle_Win = w; KeyPauseToggle_Ctrl = c; KeyPauseToggle_Alt = a; KeyPauseToggle_Shift = s; KeyPauseToggle = k;

                w = KeyReset_Win; c = KeyReset_Ctrl; a = KeyReset_Alt; s = KeyReset_Shift; k = KeyReset;
                LoadHotkeyConfig("Reset", ref w, ref c, ref a, ref s, ref k);
                KeyReset_Win = w; KeyReset_Ctrl = c; KeyReset_Alt = a; KeyReset_Shift = s; KeyReset = k;

                w = KeySaveCountdown_Win; c = KeySaveCountdown_Ctrl; a = KeySaveCountdown_Alt; s = KeySaveCountdown_Shift; k = KeySaveCountdown;
                LoadHotkeyConfig("SaveCountdown", ref w, ref c, ref a, ref s, ref k);
                KeySaveCountdown_Win = w; KeySaveCountdown_Ctrl = c; KeySaveCountdown_Alt = a; KeySaveCountdown_Shift = s; KeySaveCountdown = k;

                w = KeySwitchMode_Win; c = KeySwitchMode_Ctrl; a = KeySwitchMode_Alt; s = KeySwitchMode_Shift; k = KeySwitchMode;
                LoadHotkeyConfig("SwitchMode", ref w, ref c, ref a, ref s, ref k);
                KeySwitchMode_Win = w; KeySwitchMode_Ctrl = c; KeySwitchMode_Alt = a; KeySwitchMode_Shift = s; KeySwitchMode = k;

                w = KeyNewInstance_Win; c = KeyNewInstance_Ctrl; a = KeyNewInstance_Alt; s = KeyNewInstance_Shift; k = KeyNewInstance;
                LoadHotkeyConfig("NewInstance", ref w, ref c, ref a, ref s, ref k);
                KeyNewInstance_Win = w; KeyNewInstance_Ctrl = c; KeyNewInstance_Alt = a; KeyNewInstance_Shift = s; KeyNewInstance = k;

                w = KeyCloseInstance_Win; c = KeyCloseInstance_Ctrl; a = KeyCloseInstance_Alt; s = KeyCloseInstance_Shift; k = KeyCloseInstance;
                LoadHotkeyConfig("CloseInstance", ref w, ref c, ref a, ref s, ref k);
                KeyCloseInstance_Win = w; KeyCloseInstance_Ctrl = c; KeyCloseInstance_Alt = a; KeyCloseInstance_Shift = s; KeyCloseInstance = k;

                w = KeyAdjustUp_Win; c = KeyAdjustUp_Ctrl; a = KeyAdjustUp_Alt; s = KeyAdjustUp_Shift; k = KeyAdjustUp;
                LoadHotkeyConfig("AdjustUp", ref w, ref c, ref a, ref s, ref k);
                KeyAdjustUp_Win = w; KeyAdjustUp_Ctrl = c; KeyAdjustUp_Alt = a; KeyAdjustUp_Shift = s; KeyAdjustUp = k;

                w = KeyAdjustDown_Win; c = KeyAdjustDown_Ctrl; a = KeyAdjustDown_Alt; s = KeyAdjustDown_Shift; k = KeyAdjustDown;
                LoadHotkeyConfig("AdjustDown", ref w, ref c, ref a, ref s, ref k);
                KeyAdjustDown_Win = w; KeyAdjustDown_Ctrl = c; KeyAdjustDown_Alt = a; KeyAdjustDown_Shift = s; KeyAdjustDown = k;
            }
            catch
            {
                // Fallback to default values if parse fails
            }
        }

        private void LoadHotkeyConfig(string name, ref bool win, ref bool ctrl, ref bool alt, ref bool shift, ref uint vk)
        {
            if (TryGetValue("Hotkeys", name + "_Win", out string wStr) && bool.TryParse(wStr, out bool w)) win = w;
            if (TryGetValue("Hotkeys", name + "_Ctrl", out string cStr) && bool.TryParse(cStr, out bool c)) ctrl = c;
            if (TryGetValue("Hotkeys", name + "_Alt", out string aStr) && bool.TryParse(aStr, out bool a)) alt = a;
            if (TryGetValue("Hotkeys", name + "_Shift", out string sStr) && bool.TryParse(sStr, out bool s)) shift = s;
            if (TryGetValue("Hotkeys", name, out string kStr) && uint.TryParse(kStr, out uint k)) vk = k;
        }

        // Helper backing fields
        private bool _keyToggleHUD_Win => KeyToggleHUD_Win;
        private bool _keyToggleHUD_Ctrl => KeyToggleHUD_Ctrl;

        public void ResetHotkeysToDefault()
        {
            KeyToggleHUD_Win = true; KeyToggleHUD_Ctrl = false; KeyToggleHUD_Alt = false; KeyToggleHUD_Shift = false; KeyToggleHUD = Win32Api.VK_OEM_3;
            KeyPauseToggle_Win = true; KeyPauseToggle_Ctrl = false; KeyPauseToggle_Alt = false; KeyPauseToggle_Shift = false; KeyPauseToggle = Win32Api.VK_SPACE;
            KeyReset_Win = true; KeyReset_Ctrl = false; KeyReset_Alt = false; KeyReset_Shift = false; KeyReset = Win32Api.VK_BACK;
            KeySaveCountdown_Win = true; KeySaveCountdown_Ctrl = false; KeySaveCountdown_Alt = false; KeySaveCountdown_Shift = false; KeySaveCountdown = Win32Api.VK_S;
            KeySwitchMode_Win = true; KeySwitchMode_Ctrl = false; KeySwitchMode_Alt = false; KeySwitchMode_Shift = false; KeySwitchMode = Win32Api.VK_ESCAPE;
            KeyNewInstance_Win = true; KeyNewInstance_Ctrl = false; KeyNewInstance_Alt = false; KeyNewInstance_Shift = false; KeyNewInstance = Win32Api.VK_N;
            KeyCloseInstance_Win = true; KeyCloseInstance_Ctrl = false; KeyCloseInstance_Alt = false; KeyCloseInstance_Shift = false; KeyCloseInstance = Win32Api.VK_W;
            KeyAdjustUp_Win = true; KeyAdjustUp_Ctrl = false; KeyAdjustUp_Alt = false; KeyAdjustUp_Shift = false; KeyAdjustUp = Win32Api.VK_UP;
            KeyAdjustDown_Win = true; KeyAdjustDown_Ctrl = false; KeyAdjustDown_Alt = false; KeyAdjustDown_Shift = false; KeyAdjustDown = Win32Api.VK_DOWN;
        }

        private void ParseIniFile()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_iniPath)) return;

                try
                {
                    _data.Clear();
                    string currentSection = "";
                    foreach (string rawLine in File.ReadAllLines(_iniPath))
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                            continue;

                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            currentSection = line.Substring(1, line.Length - 2).Trim();
                            if (!_data.ContainsKey(currentSection))
                                _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                        else if (!string.IsNullOrEmpty(currentSection) && line.Contains("="))
                        {
                            int idx = line.IndexOf('=');
                            string key = line.Substring(0, idx).Trim();
                            string val = line.Substring(idx + 1).Trim();
                            _data[currentSection][key] = val;
                        }
                    }
                }
                catch { }
            }
        }

        public void Save()
        {
            try
            {
                lock (_fileLock)
                {
                    ParseIniFile();

                    string sec = "Instance_" + InstanceId;
                    SetValue(sec, "x", WindowX.ToString());
                    SetValue(sec, "y", WindowY.ToString());
                    SetValue(sec, "width", WindowWidth.ToString());
                    SetValue(sec, "height", WindowHeight.ToString());
                    SetValue(sec, "mode", Mode);
                    SetValue(sec, "LastSetMinutes", LastSetMinutes.ToString());
                    SetValue(sec, "LastSetSeconds", LastSetSeconds.ToString());
                    SetValue(sec, "click_through", ClickThrough.ToString().ToLowerInvariant());
                    SetValue(sec, "show_sub_info", ShowSubInfo.ToString().ToLowerInvariant());
                    SetValue(sec, "obs_hide_stream", ObsHideStream.ToString().ToLowerInvariant());
                    SetValue(sec, "show_in_taskbar", ShowInTaskbar.ToString().ToLowerInvariant());
                    SetValue(sec, "minimize_keep_working", MinimizeKeepWorking.ToString().ToLowerInvariant());
                    SetValue(sec, "opacity", Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

                    if (InstanceId == 1)
                    {
                        SetValue("Position", "x", WindowX.ToString());
                        SetValue("Position", "y", WindowY.ToString());
                        SetValue("General", "mode", Mode);
                        SetValue("Timer", "LastSetMinutes", LastSetMinutes.ToString());
                        SetValue("Timer", "LastSetSeconds", LastSetSeconds.ToString());
                        SetValue("Window", "click_through", ClickThrough.ToString().ToLowerInvariant());
                        SetValue("Window", "opacity", Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                        SetValue("Gamepad", "enabled", GamepadEnabled.ToString().ToLowerInvariant());
                        SetValue("Gamepad", "vibration", GamepadVibration.ToString().ToLowerInvariant());
                        SetValue("Interval", "Prepare", IntervalPrepare.ToString());
                        SetValue("Interval", "Work", IntervalWork.ToString());
                        SetValue("Interval", "Rest", IntervalRest.ToString());
                        SetValue("Interval", "End", IntervalEnd.ToString());
                        SetValue("Interval", "Loops", IntervalLoops.ToString());
                        SetValue("Interval", "WindowX", IntervalWindowX.ToString());
                        SetValue("Interval", "WindowY", IntervalWindowY.ToString());

                        SaveHotkeyConfig("ToggleHUD", KeyToggleHUD_Win, KeyToggleHUD_Ctrl, KeyToggleHUD_Alt, KeyToggleHUD_Shift, KeyToggleHUD);
                        SaveHotkeyConfig("PauseToggle", KeyPauseToggle_Win, KeyPauseToggle_Ctrl, KeyPauseToggle_Alt, KeyPauseToggle_Shift, KeyPauseToggle);
                        SaveHotkeyConfig("Reset", KeyReset_Win, KeyReset_Ctrl, KeyReset_Alt, KeyReset_Shift, KeyReset);
                        SaveHotkeyConfig("SaveCountdown", KeySaveCountdown_Win, KeySaveCountdown_Ctrl, KeySaveCountdown_Alt, KeySaveCountdown_Shift, KeySaveCountdown);
                        SaveHotkeyConfig("SwitchMode", KeySwitchMode_Win, KeySwitchMode_Ctrl, KeySwitchMode_Alt, KeySwitchMode_Shift, KeySwitchMode);
                        SaveHotkeyConfig("NewInstance", KeyNewInstance_Win, KeyNewInstance_Ctrl, KeyNewInstance_Alt, KeyNewInstance_Shift, KeyNewInstance);
                        SaveHotkeyConfig("CloseInstance", KeyCloseInstance_Win, KeyCloseInstance_Ctrl, KeyCloseInstance_Alt, KeyCloseInstance_Shift, KeyCloseInstance);
                        SaveHotkeyConfig("AdjustUp", KeyAdjustUp_Win, KeyAdjustUp_Ctrl, KeyAdjustUp_Alt, KeyAdjustUp_Shift, KeyAdjustUp);
                        SaveHotkeyConfig("AdjustDown", KeyAdjustDown_Win, KeyAdjustDown_Ctrl, KeyAdjustDown_Alt, KeyAdjustDown_Shift, KeyAdjustDown);
                    }

                    List<string> lines = new List<string>();
                    foreach (var section in _data)
                    {
                        lines.Add("[" + section.Key + "]");
                        foreach (var kvp in section.Value)
                        {
                            lines.Add(kvp.Key + "=" + kvp.Value);
                        }
                        lines.Add("");
                    }

                    File.WriteAllLines(_iniPath, lines);
                }
            }
            catch
            {
                // Ignore IO issues on exit
            }
        }

        private void SaveHotkeyConfig(string name, bool win, bool ctrl, bool alt, bool shift, uint vk)
        {
            SetValue("Hotkeys", name + "_Win", win.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name + "_Ctrl", ctrl.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name + "_Alt", alt.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name + "_Shift", shift.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name, vk.ToString());
        }

        private bool TryGetValue(string section, string key, out string val)
        {
            val = null;
            if (_data.TryGetValue(section, out var dict) && dict.TryGetValue(key, out val))
            {
                return true;
            }
            return false;
        }

        private void SetValue(string section, string key, string val)
        {
            if (!_data.ContainsKey(section))
                _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _data[section][key] = val;
        }
    }
}
