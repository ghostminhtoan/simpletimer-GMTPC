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
        public double Opacity { get; set; } = 1.0;
        public bool GamepadEnabled { get; set; } = false;
        public bool GamepadVibration { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool SoundEnabled { get; set; } = true;

        // Custom Hotkeys Settings (Modifiers + Key VK)
        public bool KeyToggleHUD_Win { get; set; } = true;
        public bool KeyToggleHUD_Ctrl { get; set; } = false;
        public bool KeyToggleHUD_Alt { get; set; } = false;
        public bool KeyToggleHUD_Shift { get; set; } = false;
        public uint KeyToggleHUD { get; set; } = Win32Api.VK_OEM_3; // `

        // Secondary Hotkey (Shortcut 2)
        public bool KeyToggleHUD_2_Enabled { get; set; } = false;
        public bool KeyToggleHUD_2_Win { get; set; } = true;
        public bool KeyToggleHUD_2_Ctrl { get; set; } = false;
        public bool KeyToggleHUD_2_Alt { get; set; } = false;
        public bool KeyToggleHUD_2_Shift { get; set; } = false;
        public uint KeyToggleHUD_2 { get; set; } = Win32Api.VK_OEM_3;

        public bool KeyPauseToggle_Win { get; set; } = true;
        public bool KeyPauseToggle_Ctrl { get; set; } = false;
        public bool KeyPauseToggle_Alt { get; set; } = false;
        public bool KeyPauseToggle_Shift { get; set; } = false;
        public uint KeyPauseToggle { get; set; } = Win32Api.VK_SPACE; // Space

        public bool KeyPauseToggle_2_Enabled { get; set; } = false;
        public bool KeyPauseToggle_2_Win { get; set; } = true;
        public bool KeyPauseToggle_2_Ctrl { get; set; } = false;
        public bool KeyPauseToggle_2_Alt { get; set; } = false;
        public bool KeyPauseToggle_2_Shift { get; set; } = false;
        public uint KeyPauseToggle_2 { get; set; } = Win32Api.VK_RETURN; // Enter

        public bool KeyReset_Win { get; set; } = true;
        public bool KeyReset_Ctrl { get; set; } = false;
        public bool KeyReset_Alt { get; set; } = false;
        public bool KeyReset_Shift { get; set; } = false;
        public uint KeyReset { get; set; } = Win32Api.VK_BACK; // Backspace

        public bool KeyReset_2_Enabled { get; set; } = false;
        public bool KeyReset_2_Win { get; set; } = true;
        public bool KeyReset_2_Ctrl { get; set; } = false;
        public bool KeyReset_2_Alt { get; set; } = false;
        public bool KeyReset_2_Shift { get; set; } = false;
        public uint KeyReset_2 { get; set; } = Win32Api.VK_R; // R

        public bool KeySaveCountdown_Win { get; set; } = true;
        public bool KeySaveCountdown_Ctrl { get; set; } = false;
        public bool KeySaveCountdown_Alt { get; set; } = false;
        public bool KeySaveCountdown_Shift { get; set; } = false;
        public uint KeySaveCountdown { get; set; } = Win32Api.VK_S; // S

        public bool KeySaveCountdown_2_Enabled { get; set; } = false;
        public bool KeySaveCountdown_2_Win { get; set; } = true;
        public bool KeySaveCountdown_2_Ctrl { get; set; } = false;
        public bool KeySaveCountdown_2_Alt { get; set; } = false;
        public bool KeySaveCountdown_2_Shift { get; set; } = false;
        public uint KeySaveCountdown_2 { get; set; } = Win32Api.VK_S;

        public bool KeySwitchMode_Win { get; set; } = true;
        public bool KeySwitchMode_Ctrl { get; set; } = false;
        public bool KeySwitchMode_Alt { get; set; } = false;
        public bool KeySwitchMode_Shift { get; set; } = false;
        public uint KeySwitchMode { get; set; } = Win32Api.VK_ESCAPE; // Esc

        public bool KeySwitchMode_2_Enabled { get; set; } = false;
        public bool KeySwitchMode_2_Win { get; set; } = true;
        public bool KeySwitchMode_2_Ctrl { get; set; } = false;
        public bool KeySwitchMode_2_Alt { get; set; } = false;
        public bool KeySwitchMode_2_Shift { get; set; } = false;
        public uint KeySwitchMode_2 { get; set; } = Win32Api.VK_ESCAPE;

        public bool KeyNewInstance_Win { get; set; } = true;
        public bool KeyNewInstance_Ctrl { get; set; } = false;
        public bool KeyNewInstance_Alt { get; set; } = false;
        public bool KeyNewInstance_Shift { get; set; } = false;
        public uint KeyNewInstance { get; set; } = Win32Api.VK_N; // N

        public bool KeyNewInstance_2_Enabled { get; set; } = false;
        public bool KeyNewInstance_2_Win { get; set; } = true;
        public bool KeyNewInstance_2_Ctrl { get; set; } = false;
        public bool KeyNewInstance_2_Alt { get; set; } = false;
        public bool KeyNewInstance_2_Shift { get; set; } = false;
        public uint KeyNewInstance_2 { get; set; } = Win32Api.VK_N;

        public bool KeyCloseInstance_Win { get; set; } = true;
        public bool KeyCloseInstance_Ctrl { get; set; } = false;
        public bool KeyCloseInstance_Alt { get; set; } = false;
        public bool KeyCloseInstance_Shift { get; set; } = false;
        public uint KeyCloseInstance { get; set; } = Win32Api.VK_W; // W

        public bool KeyCloseInstance_2_Enabled { get; set; } = false;
        public bool KeyCloseInstance_2_Win { get; set; } = true;
        public bool KeyCloseInstance_2_Ctrl { get; set; } = false;
        public bool KeyCloseInstance_2_Alt { get; set; } = false;
        public bool KeyCloseInstance_2_Shift { get; set; } = false;
        public uint KeyCloseInstance_2 { get; set; } = Win32Api.VK_DELETE; // Delete

        public bool KeyAdjustUp_Win { get; set; } = true;
        public bool KeyAdjustUp_Ctrl { get; set; } = false;
        public bool KeyAdjustUp_Alt { get; set; } = false;
        public bool KeyAdjustUp_Shift { get; set; } = false;
        public uint KeyAdjustUp { get; set; } = Win32Api.VK_UP; // Up

        public bool KeyAdjustUp_2_Enabled { get; set; } = false;
        public bool KeyAdjustUp_2_Win { get; set; } = true;
        public bool KeyAdjustUp_2_Ctrl { get; set; } = false;
        public bool KeyAdjustUp_2_Alt { get; set; } = false;
        public bool KeyAdjustUp_2_Shift { get; set; } = false;
        public uint KeyAdjustUp_2 { get; set; } = Win32Api.VK_UP;

        public bool KeyAdjustDown_Win { get; set; } = true;
        public bool KeyAdjustDown_Ctrl { get; set; } = false;
        public bool KeyAdjustDown_Alt { get; set; } = false;
        public bool KeyAdjustDown_Shift { get; set; } = false;
        public uint KeyAdjustDown { get; set; } = Win32Api.VK_DOWN; // Down

        public bool KeyAdjustDown_2_Enabled { get; set; } = false;
        public bool KeyAdjustDown_2_Win { get; set; } = true;
        public bool KeyAdjustDown_2_Ctrl { get; set; } = false;
        public bool KeyAdjustDown_2_Alt { get; set; } = false;
        public bool KeyAdjustDown_2_Shift { get; set; } = false;
        public uint KeyAdjustDown_2 { get; set; } = Win32Api.VK_DOWN;

        public bool KeyIntervalTimer_Win { get; set; } = true;
        public bool KeyIntervalTimer_Ctrl { get; set; } = true;
        public bool KeyIntervalTimer_Alt { get; set; } = false;
        public bool KeyIntervalTimer_Shift { get; set; } = false;
        public uint KeyIntervalTimer { get; set; } = Win32Api.VK_ESCAPE; // Esc

        public bool KeyIntervalTimer_2_Enabled { get; set; } = false;
        public bool KeyIntervalTimer_2_Win { get; set; } = true;
        public bool KeyIntervalTimer_2_Ctrl { get; set; } = true;
        public bool KeyIntervalTimer_2_Alt { get; set; } = false;
        public bool KeyIntervalTimer_2_Shift { get; set; } = false;
        public uint KeyIntervalTimer_2 { get; set; } = Win32Api.VK_ESCAPE;

        // Interval Timer settings
        public int IntervalPrepare { get; set; } = 5;
        public int IntervalWork { get; set; } = 30;
        public int IntervalRest { get; set; } = 10;
        public int IntervalEnd { get; set; } = 5;
        public int IntervalLoops { get; set; } = 3;
        public bool IntervalInfinite { get; set; } = false;
        public int IntervalWindowX { get; set; } = 250;
        public int IntervalWindowY { get; set; } = 250;

        public List<IntervalPreset> IntervalPresets { get; set; } = new List<IntervalPreset>();

        public static List<IntervalPreset> GetDefaultPresets()
        {
            return new List<IntervalPreset>
            {
                new IntervalPreset { Id = 1, Name = "Tabata", Prepare = 5, Work = 20, Rest = 10, End = 5, Loops = 8, IsInfinite = false },
                new IntervalPreset { Id = 2, Name = "30 / 15", Prepare = 5, Work = 30, Rest = 15, End = 5, Loops = 5, IsInfinite = false },
                new IntervalPreset { Id = 3, Name = "45 / 15", Prepare = 5, Work = 45, Rest = 15, End = 5, Loops = 5, IsInfinite = false },
                new IntervalPreset { Id = 4, Name = "3m / 1m", Prepare = 5, Work = 180, Rest = 60, End = 5, Loops = 5, IsInfinite = false },
                new IntervalPreset { Id = 5, Name = "25m / 5m", Prepare = 5, Work = 1500, Rest = 300, End = 5, Loops = 4, IsInfinite = false }
            };
        }

        public void ResetIntervalPreset(int id)
        {
            var defs = GetDefaultPresets();
            int idx = id - 1;
            if (idx >= 0 && idx < defs.Count && idx < IntervalPresets.Count)
            {
                IntervalPresets[idx] = defs[idx];
                Save();
            }
        }

        public void ResetAllIntervalPresets()
        {
            IntervalPresets = GetDefaultPresets();
            Save();
        }

        public SettingsManager(int instanceId = 1)
        {
            InstanceId = instanceId;
            IntervalPresets = GetDefaultPresets();

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
                    GamepadEnabled = false;

                if (TryGetValue("Gamepad", "vibration", out string padVibStr) && bool.TryParse(padVibStr, out bool padVib))
                    GamepadVibration = padVib;
                else
                    GamepadVibration = true;

                if (TryGetValue("General", "sound_enabled", out string soundStr) && bool.TryParse(soundStr, out bool soundVal))
                    SoundEnabled = soundVal;
                else
                    SoundEnabled = true;

                if (TryGetValue("General", "start_with_windows", out string startStr) && bool.TryParse(startStr, out bool startVal))
                    StartWithWindows = startVal;
                else
                    StartWithWindows = StartupManager.IsStartupEnabled();

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
                if (TryGetValue("Interval", "Infinite", out string infStr) && bool.TryParse(infStr, out bool infVal))
                    IntervalInfinite = infVal;
                else
                    IntervalInfinite = (IntervalLoops <= 0);
                if (TryGetValue("Interval", "WindowX", out string ixStr) && int.TryParse(ixStr, out int ix))
                    IntervalWindowX = ix;
                if (TryGetValue("Interval", "WindowY", out string iyStr) && int.TryParse(iyStr, out int iy))
                    IntervalWindowY = iy;

                // Load IntervalPresets
                IntervalPresets.Clear();
                var defaultPresets = GetDefaultPresets();
                for (int i = 1; i <= 5; i++)
                {
                    var def = defaultPresets[i - 1];
                    var p = new IntervalPreset { Id = i };

                    if (TryGetValue("IntervalPresets", $"Preset{i}_Name", out string pNameStr) && !string.IsNullOrWhiteSpace(pNameStr))
                        p.Name = pNameStr.Trim();
                    else
                        p.Name = def.Name;

                    if (TryGetValue("IntervalPresets", $"Preset{i}_Prep", out string pPrepStr) && int.TryParse(pPrepStr, out int pPrepVal))
                        p.Prepare = Math.Max(0, pPrepVal);
                    else
                        p.Prepare = def.Prepare;

                    if (TryGetValue("IntervalPresets", $"Preset{i}_Work", out string pWorkStr) && int.TryParse(pWorkStr, out int pWorkVal))
                        p.Work = Math.Max(1, pWorkVal);
                    else
                        p.Work = def.Work;

                    if (TryGetValue("IntervalPresets", $"Preset{i}_Rest", out string pRestStr) && int.TryParse(pRestStr, out int pRestVal))
                        p.Rest = Math.Max(0, pRestVal);
                    else
                        p.Rest = def.Rest;

                    if (TryGetValue("IntervalPresets", $"Preset{i}_End", out string pEndStr) && int.TryParse(pEndStr, out int pEndVal))
                        p.End = Math.Max(0, pEndVal);
                    else
                        p.End = def.End;

                    if (TryGetValue("IntervalPresets", $"Preset{i}_Loops", out string pLoopStr) && int.TryParse(pLoopStr, out int pLoopVal))
                        p.Loops = Math.Max(1, pLoopVal);
                    else
                        p.Loops = def.Loops;

                    if (TryGetValue("IntervalPresets", $"Preset{i}_Infinite", out string pInfStr) && bool.TryParse(pInfStr, out bool pInfVal))
                        p.IsInfinite = pInfVal;
                    else
                        p.IsInfinite = def.IsInfinite;

                    IntervalPresets.Add(p);
                }

                // Hotkeys Load
                bool w = KeyToggleHUD_Win, c = KeyToggleHUD_Ctrl, a = KeyToggleHUD_Alt, s = KeyToggleHUD_Shift; uint k = KeyToggleHUD;
                LoadHotkeyConfig("ToggleHUD", ref w, ref c, ref a, ref s, ref k);
                KeyToggleHUD_Win = w; KeyToggleHUD_Ctrl = c; KeyToggleHUD_Alt = a; KeyToggleHUD_Shift = s; KeyToggleHUD = k;

                bool e2 = KeyToggleHUD_2_Enabled, w2 = KeyToggleHUD_2_Win, c2 = KeyToggleHUD_2_Ctrl, a2 = KeyToggleHUD_2_Alt, s2 = KeyToggleHUD_2_Shift; uint k2 = KeyToggleHUD_2;
                LoadHotkeyConfig2("ToggleHUD", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeyToggleHUD_2_Enabled = e2; KeyToggleHUD_2_Win = w2; KeyToggleHUD_2_Ctrl = c2; KeyToggleHUD_2_Alt = a2; KeyToggleHUD_2_Shift = s2; KeyToggleHUD_2 = k2;

                w = KeyIntervalTimer_Win; c = KeyIntervalTimer_Ctrl; a = KeyIntervalTimer_Alt; s = KeyIntervalTimer_Shift; k = KeyIntervalTimer;
                LoadHotkeyConfig("IntervalTimer", ref w, ref c, ref a, ref s, ref k);
                KeyIntervalTimer_Win = w; KeyIntervalTimer_Ctrl = c; KeyIntervalTimer_Alt = a; KeyIntervalTimer_Shift = s; KeyIntervalTimer = k;

                e2 = KeyIntervalTimer_2_Enabled; w2 = KeyIntervalTimer_2_Win; c2 = KeyIntervalTimer_2_Ctrl; a2 = KeyIntervalTimer_2_Alt; s2 = KeyIntervalTimer_2_Shift; k2 = KeyIntervalTimer_2;
                LoadHotkeyConfig2("IntervalTimer", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeyIntervalTimer_2_Enabled = e2; KeyIntervalTimer_2_Win = w2; KeyIntervalTimer_2_Ctrl = c2; KeyIntervalTimer_2_Alt = a2; KeyIntervalTimer_2_Shift = s2; KeyIntervalTimer_2 = k2;

                w = KeyPauseToggle_Win; c = KeyPauseToggle_Ctrl; a = KeyPauseToggle_Alt; s = KeyPauseToggle_Shift; k = KeyPauseToggle;
                LoadHotkeyConfig("PauseToggle", ref w, ref c, ref a, ref s, ref k);
                KeyPauseToggle_Win = w; KeyPauseToggle_Ctrl = c; KeyPauseToggle_Alt = a; KeyPauseToggle_Shift = s; KeyPauseToggle = k;

                e2 = KeyPauseToggle_2_Enabled; w2 = KeyPauseToggle_2_Win; c2 = KeyPauseToggle_2_Ctrl; a2 = KeyPauseToggle_2_Alt; s2 = KeyPauseToggle_2_Shift; k2 = KeyPauseToggle_2;
                LoadHotkeyConfig2("PauseToggle", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeyPauseToggle_2_Enabled = e2; KeyPauseToggle_2_Win = w2; KeyPauseToggle_2_Ctrl = c2; KeyPauseToggle_2_Alt = a2; KeyPauseToggle_2_Shift = s2; KeyPauseToggle_2 = k2;

                w = KeyReset_Win; c = KeyReset_Ctrl; a = KeyReset_Alt; s = KeyReset_Shift; k = KeyReset;
                LoadHotkeyConfig("Reset", ref w, ref c, ref a, ref s, ref k);
                KeyReset_Win = w; KeyReset_Ctrl = c; KeyReset_Alt = a; KeyReset_Shift = s; KeyReset = k;

                e2 = KeyReset_2_Enabled; w2 = KeyReset_2_Win; c2 = KeyReset_2_Ctrl; a2 = KeyReset_2_Alt; s2 = KeyReset_2_Shift; k2 = KeyReset_2;
                LoadHotkeyConfig2("Reset", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeyReset_2_Enabled = e2; KeyReset_2_Win = w2; KeyReset_2_Ctrl = c2; KeyReset_2_Alt = a2; KeyReset_2_Shift = s2; KeyReset_2 = k2;

                w = KeySaveCountdown_Win; c = KeySaveCountdown_Ctrl; a = KeySaveCountdown_Alt; s = KeySaveCountdown_Shift; k = KeySaveCountdown;
                LoadHotkeyConfig("SaveCountdown", ref w, ref c, ref a, ref s, ref k);
                KeySaveCountdown_Win = w; KeySaveCountdown_Ctrl = c; KeySaveCountdown_Alt = a; KeySaveCountdown_Shift = s; KeySaveCountdown = k;

                e2 = KeySaveCountdown_2_Enabled; w2 = KeySaveCountdown_2_Win; c2 = KeySaveCountdown_2_Ctrl; a2 = KeySaveCountdown_2_Alt; s2 = KeySaveCountdown_2_Shift; k2 = KeySaveCountdown_2;
                LoadHotkeyConfig2("SaveCountdown", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeySaveCountdown_2_Enabled = e2; KeySaveCountdown_2_Win = w2; KeySaveCountdown_2_Ctrl = c2; KeySaveCountdown_2_Alt = a2; KeySaveCountdown_2_Shift = s2; KeySaveCountdown_2 = k2;

                w = KeySwitchMode_Win; c = KeySwitchMode_Ctrl; a = KeySwitchMode_Alt; s = KeySwitchMode_Shift; k = KeySwitchMode;
                LoadHotkeyConfig("SwitchMode", ref w, ref c, ref a, ref s, ref k);
                KeySwitchMode_Win = w; KeySwitchMode_Ctrl = c; KeySwitchMode_Alt = a; KeySwitchMode_Shift = s; KeySwitchMode = k;

                e2 = KeySwitchMode_2_Enabled; w2 = KeySwitchMode_2_Win; c2 = KeySwitchMode_2_Ctrl; a2 = KeySwitchMode_2_Alt; s2 = KeySwitchMode_2_Shift; k2 = KeySwitchMode_2;
                LoadHotkeyConfig2("SwitchMode", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeySwitchMode_2_Enabled = e2; KeySwitchMode_2_Win = w2; KeySwitchMode_2_Ctrl = c2; KeySwitchMode_2_Alt = a2; KeySwitchMode_2_Shift = s2; KeySwitchMode_2 = k2;

                w = KeyNewInstance_Win; c = KeyNewInstance_Ctrl; a = KeyNewInstance_Alt; s = KeyNewInstance_Shift; k = KeyNewInstance;
                LoadHotkeyConfig("NewInstance", ref w, ref c, ref a, ref s, ref k);
                KeyNewInstance_Win = w; KeyNewInstance_Ctrl = c; KeyNewInstance_Alt = a; KeyNewInstance_Shift = s; KeyNewInstance = k;

                e2 = KeyNewInstance_2_Enabled; w2 = KeyNewInstance_2_Win; c2 = KeyNewInstance_2_Ctrl; a2 = KeyNewInstance_2_Alt; s2 = KeyNewInstance_2_Shift; k2 = KeyNewInstance_2;
                LoadHotkeyConfig2("NewInstance", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeyNewInstance_2_Enabled = e2; KeyNewInstance_2_Win = w2; KeyNewInstance_2_Ctrl = c2; KeyNewInstance_2_Alt = a2; KeyNewInstance_2_Shift = s2; KeyNewInstance_2 = k2;

                w = KeyCloseInstance_Win; c = KeyCloseInstance_Ctrl; a = KeyCloseInstance_Alt; s = KeyCloseInstance_Shift; k = KeyCloseInstance;
                LoadHotkeyConfig("CloseInstance", ref w, ref c, ref a, ref s, ref k);
                KeyCloseInstance_Win = w; KeyCloseInstance_Ctrl = c; KeyCloseInstance_Alt = a; KeyCloseInstance_Shift = s; KeyCloseInstance = k;

                e2 = KeyCloseInstance_2_Enabled; w2 = KeyCloseInstance_2_Win; c2 = KeyCloseInstance_2_Ctrl; a2 = KeyCloseInstance_2_Alt; s2 = KeyCloseInstance_2_Shift; k2 = KeyCloseInstance_2;
                LoadHotkeyConfig2("CloseInstance", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeyCloseInstance_2_Enabled = e2; KeyCloseInstance_2_Win = w2; KeyCloseInstance_2_Ctrl = c2; KeyCloseInstance_2_Alt = a2; KeyCloseInstance_2_Shift = s2; KeyCloseInstance_2 = k2;

                w = KeyAdjustUp_Win; c = KeyAdjustUp_Ctrl; a = KeyAdjustUp_Alt; s = KeyAdjustUp_Shift; k = KeyAdjustUp;
                LoadHotkeyConfig("AdjustUp", ref w, ref c, ref a, ref s, ref k);
                KeyAdjustUp_Win = w; KeyAdjustUp_Ctrl = c; KeyAdjustUp_Alt = a; KeyAdjustUp_Shift = s; KeyAdjustUp = k;

                e2 = KeyAdjustUp_2_Enabled; w2 = KeyAdjustUp_2_Win; c2 = KeyAdjustUp_2_Ctrl; a2 = KeyAdjustUp_2_Alt; s2 = KeyAdjustUp_2_Shift; k2 = KeyAdjustUp_2;
                LoadHotkeyConfig2("AdjustUp", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeyAdjustUp_2_Enabled = e2; KeyAdjustUp_2_Win = w2; KeyAdjustUp_2_Ctrl = c2; KeyAdjustUp_2_Alt = a2; KeyAdjustUp_2_Shift = s2; KeyAdjustUp_2 = k2;

                w = KeyAdjustDown_Win; c = KeyAdjustDown_Ctrl; a = KeyAdjustDown_Alt; s = KeyAdjustDown_Shift; k = KeyAdjustDown;
                LoadHotkeyConfig("AdjustDown", ref w, ref c, ref a, ref s, ref k);
                KeyAdjustDown_Win = w; KeyAdjustDown_Ctrl = c; KeyAdjustDown_Alt = a; KeyAdjustDown_Shift = s; KeyAdjustDown = k;

                e2 = KeyAdjustDown_2_Enabled; w2 = KeyAdjustDown_2_Win; c2 = KeyAdjustDown_2_Ctrl; a2 = KeyAdjustDown_2_Alt; s2 = KeyAdjustDown_2_Shift; k2 = KeyAdjustDown_2;
                LoadHotkeyConfig2("AdjustDown", ref e2, ref w2, ref c2, ref a2, ref s2, ref k2);
                KeyAdjustDown_2_Enabled = e2; KeyAdjustDown_2_Win = w2; KeyAdjustDown_2_Ctrl = c2; KeyAdjustDown_2_Alt = a2; KeyAdjustDown_2_Shift = s2; KeyAdjustDown_2 = k2;
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

        private void LoadHotkeyConfig2(string name, ref bool enabled, ref bool win, ref bool ctrl, ref bool alt, ref bool shift, ref uint vk)
        {
            if (TryGetValue("Hotkeys", name + "_2_Enabled", out string eStr) && bool.TryParse(eStr, out bool e)) enabled = e;
            if (TryGetValue("Hotkeys", name + "_2_Win", out string wStr) && bool.TryParse(wStr, out bool w)) win = w;
            if (TryGetValue("Hotkeys", name + "_2_Ctrl", out string cStr) && bool.TryParse(cStr, out bool c)) ctrl = c;
            if (TryGetValue("Hotkeys", name + "_2_Alt", out string aStr) && bool.TryParse(aStr, out bool a)) alt = a;
            if (TryGetValue("Hotkeys", name + "_2_Shift", out string sStr) && bool.TryParse(sStr, out bool s)) shift = s;
            if (TryGetValue("Hotkeys", name + "_2", out string kStr) && uint.TryParse(kStr, out uint k)) vk = k;
        }

        // Helper backing fields
        private bool _keyToggleHUD_Win => KeyToggleHUD_Win;
        private bool _keyToggleHUD_Ctrl => KeyToggleHUD_Ctrl;

        public void ResetHotkeysToDefault()
        {
            KeyToggleHUD_Win = true; KeyToggleHUD_Ctrl = false; KeyToggleHUD_Alt = false; KeyToggleHUD_Shift = false; KeyToggleHUD = Win32Api.VK_OEM_3;
            KeyToggleHUD_2_Enabled = false; KeyToggleHUD_2_Win = true; KeyToggleHUD_2_Ctrl = false; KeyToggleHUD_2_Alt = false; KeyToggleHUD_2_Shift = false; KeyToggleHUD_2 = Win32Api.VK_OEM_3;

            KeyIntervalTimer_Win = true; KeyIntervalTimer_Ctrl = true; KeyIntervalTimer_Alt = false; KeyIntervalTimer_Shift = false; KeyIntervalTimer = Win32Api.VK_ESCAPE;
            KeyIntervalTimer_2_Enabled = false; KeyIntervalTimer_2_Win = true; KeyIntervalTimer_2_Ctrl = true; KeyIntervalTimer_2_Alt = false; KeyIntervalTimer_2_Shift = false; KeyIntervalTimer_2 = Win32Api.VK_ESCAPE;

            KeyPauseToggle_Win = true; KeyPauseToggle_Ctrl = false; KeyPauseToggle_Alt = false; KeyPauseToggle_Shift = false; KeyPauseToggle = Win32Api.VK_SPACE;
            KeyPauseToggle_2_Enabled = false; KeyPauseToggle_2_Win = true; KeyPauseToggle_2_Ctrl = false; KeyPauseToggle_2_Alt = false; KeyPauseToggle_2_Shift = false; KeyPauseToggle_2 = Win32Api.VK_RETURN;

            KeyReset_Win = true; KeyReset_Ctrl = false; KeyReset_Alt = false; KeyReset_Shift = false; KeyReset = Win32Api.VK_BACK;
            KeyReset_2_Enabled = false; KeyReset_2_Win = true; KeyReset_2_Ctrl = false; KeyReset_2_Alt = false; KeyReset_2_Shift = false; KeyReset_2 = Win32Api.VK_R;

            KeySaveCountdown_Win = true; KeySaveCountdown_Ctrl = false; KeySaveCountdown_Alt = false; KeySaveCountdown_Shift = false; KeySaveCountdown = Win32Api.VK_S;
            KeySaveCountdown_2_Enabled = false; KeySaveCountdown_2_Win = true; KeySaveCountdown_2_Ctrl = false; KeySaveCountdown_2_Alt = false; KeySaveCountdown_2_Shift = false; KeySaveCountdown_2 = Win32Api.VK_S;

            KeySwitchMode_Win = true; KeySwitchMode_Ctrl = false; KeySwitchMode_Alt = false; KeySwitchMode_Shift = false; KeySwitchMode = Win32Api.VK_ESCAPE;
            KeySwitchMode_2_Enabled = false; KeySwitchMode_2_Win = true; KeySwitchMode_2_Ctrl = false; KeySwitchMode_2_Alt = false; KeySwitchMode_2_Shift = false; KeySwitchMode_2 = Win32Api.VK_ESCAPE;

            KeyNewInstance_Win = true; KeyNewInstance_Ctrl = false; KeyNewInstance_Alt = false; KeyNewInstance_Shift = false; KeyNewInstance = Win32Api.VK_N;
            KeyNewInstance_2_Enabled = false; KeyNewInstance_2_Win = true; KeyNewInstance_2_Ctrl = false; KeyNewInstance_2_Alt = false; KeyNewInstance_2_Shift = false; KeyNewInstance_2 = Win32Api.VK_N;

            KeyCloseInstance_Win = true; KeyCloseInstance_Ctrl = false; KeyCloseInstance_Alt = false; KeyCloseInstance_Shift = false; KeyCloseInstance = Win32Api.VK_W;
            KeyCloseInstance_2_Enabled = false; KeyCloseInstance_2_Win = true; KeyCloseInstance_2_Ctrl = false; KeyCloseInstance_2_Alt = false; KeyCloseInstance_2_Shift = false; KeyCloseInstance_2 = Win32Api.VK_DELETE;

            KeyAdjustUp_Win = true; KeyAdjustUp_Ctrl = false; KeyAdjustUp_Alt = false; KeyAdjustUp_Shift = false; KeyAdjustUp = Win32Api.VK_UP;
            KeyAdjustUp_2_Enabled = false; KeyAdjustUp_2_Win = true; KeyAdjustUp_2_Ctrl = false; KeyAdjustUp_2_Alt = false; KeyAdjustUp_2_Shift = false; KeyAdjustUp_2 = Win32Api.VK_UP;

            KeyAdjustDown_Win = true; KeyAdjustDown_Ctrl = false; KeyAdjustDown_Alt = false; KeyAdjustDown_Shift = false; KeyAdjustDown = Win32Api.VK_DOWN;
            KeyAdjustDown_2_Enabled = false; KeyAdjustDown_2_Win = true; KeyAdjustDown_2_Ctrl = false; KeyAdjustDown_2_Alt = false; KeyAdjustDown_2_Shift = false; KeyAdjustDown_2 = Win32Api.VK_DOWN;
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
                    List<string> rawLines = new List<string>();
                    using (var fs = new FileStream(_iniPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs))
                    {
                        string l;
                        while ((l = reader.ReadLine()) != null)
                        {
                            rawLines.Add(l);
                        }
                    }

                    foreach (string rawLine in rawLines)
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
                    SetValue(sec, "opacity", Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

                    if (InstanceId == 1)
                    {
                        SetValue("Position", "x", WindowX.ToString());
                        SetValue("Position", "y", WindowY.ToString());
                        SetValue("General", "mode", Mode);
                        SetValue("General", "start_with_windows", StartWithWindows.ToString().ToLowerInvariant());
                        SetValue("General", "sound_enabled", SoundEnabled.ToString().ToLowerInvariant());
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
                        SetValue("Interval", "Infinite", IntervalInfinite.ToString().ToLowerInvariant());
                        SetValue("Interval", "WindowX", IntervalWindowX.ToString());
                        SetValue("Interval", "WindowY", IntervalWindowY.ToString());

                        if (IntervalPresets != null)
                        {
                            for (int i = 0; i < IntervalPresets.Count; i++)
                            {
                                var p = IntervalPresets[i];
                                int idx = p.Id > 0 ? p.Id : (i + 1);
                                SetValue("IntervalPresets", $"Preset{idx}_Name", p.Name ?? "");
                                SetValue("IntervalPresets", $"Preset{idx}_Prep", p.Prepare.ToString());
                                SetValue("IntervalPresets", $"Preset{idx}_Work", p.Work.ToString());
                                SetValue("IntervalPresets", $"Preset{idx}_Rest", p.Rest.ToString());
                                SetValue("IntervalPresets", $"Preset{idx}_End", p.End.ToString());
                                SetValue("IntervalPresets", $"Preset{idx}_Loops", p.Loops.ToString());
                                SetValue("IntervalPresets", $"Preset{idx}_Infinite", p.IsInfinite.ToString().ToLowerInvariant());
                            }
                        }

                        SaveHotkeyConfig("ToggleHUD", KeyToggleHUD_Win, KeyToggleHUD_Ctrl, KeyToggleHUD_Alt, KeyToggleHUD_Shift, KeyToggleHUD);
                        SaveHotkeyConfig2("ToggleHUD", KeyToggleHUD_2_Enabled, KeyToggleHUD_2_Win, KeyToggleHUD_2_Ctrl, KeyToggleHUD_2_Alt, KeyToggleHUD_2_Shift, KeyToggleHUD_2);

                        SaveHotkeyConfig("IntervalTimer", KeyIntervalTimer_Win, KeyIntervalTimer_Ctrl, KeyIntervalTimer_Alt, KeyIntervalTimer_Shift, KeyIntervalTimer);
                        SaveHotkeyConfig2("IntervalTimer", KeyIntervalTimer_2_Enabled, KeyIntervalTimer_2_Win, KeyIntervalTimer_2_Ctrl, KeyIntervalTimer_2_Alt, KeyIntervalTimer_2_Shift, KeyIntervalTimer_2);

                        SaveHotkeyConfig("PauseToggle", KeyPauseToggle_Win, KeyPauseToggle_Ctrl, KeyPauseToggle_Alt, KeyPauseToggle_Shift, KeyPauseToggle);
                        SaveHotkeyConfig2("PauseToggle", KeyPauseToggle_2_Enabled, KeyPauseToggle_2_Win, KeyPauseToggle_2_Ctrl, KeyPauseToggle_2_Alt, KeyPauseToggle_2_Shift, KeyPauseToggle_2);

                        SaveHotkeyConfig("Reset", KeyReset_Win, KeyReset_Ctrl, KeyReset_Alt, KeyReset_Shift, KeyReset);
                        SaveHotkeyConfig2("Reset", KeyReset_2_Enabled, KeyReset_2_Win, KeyReset_2_Ctrl, KeyReset_2_Alt, KeyReset_2_Shift, KeyReset_2);

                        SaveHotkeyConfig("SaveCountdown", KeySaveCountdown_Win, KeySaveCountdown_Ctrl, KeySaveCountdown_Alt, KeySaveCountdown_Shift, KeySaveCountdown);
                        SaveHotkeyConfig2("SaveCountdown", KeySaveCountdown_2_Enabled, KeySaveCountdown_2_Win, KeySaveCountdown_2_Ctrl, KeySaveCountdown_2_Alt, KeySaveCountdown_2_Shift, KeySaveCountdown_2);

                        SaveHotkeyConfig("SwitchMode", KeySwitchMode_Win, KeySwitchMode_Ctrl, KeySwitchMode_Alt, KeySwitchMode_Shift, KeySwitchMode);
                        SaveHotkeyConfig2("SwitchMode", KeySwitchMode_2_Enabled, KeySwitchMode_2_Win, KeySwitchMode_2_Ctrl, KeySwitchMode_2_Alt, KeySwitchMode_2_Shift, KeySwitchMode_2);

                        SaveHotkeyConfig("NewInstance", KeyNewInstance_Win, KeyNewInstance_Ctrl, KeyNewInstance_Alt, KeyNewInstance_Shift, KeyNewInstance);
                        SaveHotkeyConfig2("NewInstance", KeyNewInstance_2_Enabled, KeyNewInstance_2_Win, KeyNewInstance_2_Ctrl, KeyNewInstance_2_Alt, KeyNewInstance_2_Shift, KeyNewInstance_2);

                        SaveHotkeyConfig("CloseInstance", KeyCloseInstance_Win, KeyCloseInstance_Ctrl, KeyCloseInstance_Alt, KeyCloseInstance_Shift, KeyCloseInstance);
                        SaveHotkeyConfig2("CloseInstance", KeyCloseInstance_2_Enabled, KeyCloseInstance_2_Win, KeyCloseInstance_2_Ctrl, KeyCloseInstance_2_Alt, KeyCloseInstance_2_Shift, KeyCloseInstance_2);

                        SaveHotkeyConfig("AdjustUp", KeyAdjustUp_Win, KeyAdjustUp_Ctrl, KeyAdjustUp_Alt, KeyAdjustUp_Shift, KeyAdjustUp);
                        SaveHotkeyConfig2("AdjustUp", KeyAdjustUp_2_Enabled, KeyAdjustUp_2_Win, KeyAdjustUp_2_Ctrl, KeyAdjustUp_2_Alt, KeyAdjustUp_2_Shift, KeyAdjustUp_2);

                        SaveHotkeyConfig("AdjustDown", KeyAdjustDown_Win, KeyAdjustDown_Ctrl, KeyAdjustDown_Alt, KeyAdjustDown_Shift, KeyAdjustDown);
                        SaveHotkeyConfig2("AdjustDown", KeyAdjustDown_2_Enabled, KeyAdjustDown_2_Win, KeyAdjustDown_2_Ctrl, KeyAdjustDown_2_Alt, KeyAdjustDown_2_Shift, KeyAdjustDown_2);
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

        private void SaveHotkeyConfig2(string name, bool enabled, bool win, bool ctrl, bool alt, bool shift, uint vk)
        {
            SetValue("Hotkeys", name + "_2_Enabled", enabled.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name + "_2_Win", win.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name + "_2_Ctrl", ctrl.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name + "_2_Alt", alt.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name + "_2_Shift", shift.ToString().ToLowerInvariant());
            SetValue("Hotkeys", name + "_2", vk.ToString());
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

        private System.Threading.Timer _debounceTimer;
        private readonly object _debounceLock = new object();

        public void RequestSaveDebounced(int delayMs = 300)
        {
            lock (_debounceLock)
            {
                if (_debounceTimer == null)
                {
                    _debounceTimer = new System.Threading.Timer(_ =>
                    {
                        try { Save(); } catch { }
                    }, null, delayMs, System.Threading.Timeout.Infinite);
                }
                else
                {
                    _debounceTimer.Change(delayMs, System.Threading.Timeout.Infinite);
                }
            }
        }
    }

    public class IntervalPreset
    {
        public int Id { get; set; } = 1;
        public string Name { get; set; } = "Preset";
        public int Prepare { get; set; } = 5;
        public int Work { get; set; } = 30;
        public int Rest { get; set; } = 10;
        public int End { get; set; } = 5;
        public int Loops { get; set; } = 5;
        public bool IsInfinite { get; set; } = false;

        public IntervalPreset Clone()
        {
            return new IntervalPreset
            {
                Id = this.Id,
                Name = this.Name,
                Prepare = this.Prepare,
                Work = this.Work,
                Rest = this.Rest,
                End = this.End,
                Loops = this.Loops,
                IsInfinite = this.IsInfinite
            };
        }

        public string GetSummary()
        {
            string loopText = IsInfinite ? "∞ Loops" : $"{Loops} Loops";
            return $"Work {Work}s / Rest {Rest}s × {loopText}";
        }
    }
}
