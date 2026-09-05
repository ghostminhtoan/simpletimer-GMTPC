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
        public bool GamepadEnabled { get; set; } = true;
        public bool GamepadVibration { get; set; } = true;

        public SettingsManager(int instanceId = 1)
        {
            InstanceId = instanceId;

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string stateDir = Path.Combine(appDir, "gui_state");
            try
            {
                if (!Directory.Exists(stateDir))
                {
                    Directory.CreateDirectory(stateDir);
                }
            }
            catch { }

            _iniPath = Path.Combine(stateDir, "config.ini");
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
                        LastSetMinutes = Math.Max(1, mins);
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
                        LastSetMinutes = Math.Max(1, mins);

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

                if (InstanceId == 1)
                {
                    SetValue("Position", "x", WindowX.ToString());
                    SetValue("Position", "y", WindowY.ToString());
                    SetValue("General", "mode", Mode);
                    SetValue("Timer", "LastSetMinutes", LastSetMinutes.ToString());
                    SetValue("Gamepad", "enabled", GamepadEnabled.ToString().ToLowerInvariant());
                    SetValue("Gamepad", "vibration", GamepadVibration.ToString().ToLowerInvariant());
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
