using System;
using System.Collections.Generic;
using System.IO;

namespace TimeBomb.Core
{
    public class SettingsManager
    {
        private readonly string _iniPath;
        private readonly Dictionary<string, Dictionary<string, string>> _data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public int InstanceId { get; set; } = 1;
        public int WindowX { get; set; } = 150;
        public int WindowY { get; set; } = 150;
        public string Mode { get; set; } = "timer";
        public int LastSetMinutes { get; set; } = 3;
        public int LastSetSeconds { get; set; } = 0;
        public bool ClickThrough { get; set; } = false;
        public double Opacity { get; set; } = 1.0;
        public bool GamepadEnabled { get; set; } = true;
        public bool GamepadVibration { get; set; } = true;

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
            }
            catch
            {
                // Fallback to default values if parse fails
            }
        }

        private void ParseIniFile()
        {
            if (!File.Exists(_iniPath)) return;

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

        public void Save()
        {
            try
            {
                ParseIniFile();

                string sec = "Instance_" + InstanceId;
                SetValue(sec, "x", WindowX.ToString());
                SetValue(sec, "y", WindowY.ToString());
                SetValue(sec, "mode", Mode);
                SetValue(sec, "LastSetMinutes", LastSetMinutes.ToString());
                SetValue(sec, "LastSetSeconds", LastSetSeconds.ToString());
                SetValue(sec, "click_through", ClickThrough.ToString().ToLowerInvariant());
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
            catch
            {
                // Ignore IO issues on exit
            }
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
