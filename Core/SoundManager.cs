using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace TimeBomb.Core
{
    public class SoundManager : IDisposable
    {
        private readonly string _soundsDir;
        private readonly Dictionary<string, SoundPlayer> _cache = new Dictionary<string, SoundPlayer>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MemoryStream> _activeStreams = new List<MemoryStream>();
        private SoundPlayer _alarmPlayer;

        public SoundManager()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            _soundsDir = Path.Combine(appDir, "Resources", "Sounds");
            PreloadAllSounds();
        }

        private void PreloadAllSounds()
        {
            try
            {
                if (Directory.Exists(_soundsDir))
                {
                    string[] wavFiles = Directory.GetFiles(_soundsDir, "*.wav");
                    foreach (string filePath in wavFiles)
                    {
                        string fileName = Path.GetFileName(filePath);
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(filePath);
                            var ms = new MemoryStream(bytes);
                            _activeStreams.Add(ms);
                            var player = new SoundPlayer(ms);
                            player.Load();
                            _cache[fileName] = player;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (!_isEnabled)
                {
                    StopAlarmLoop();
                }
            }
        }

        public void Play(string soundFile)
        {
            if (!_isEnabled) return;
            try
            {
                SoundPlayer player = GetPlayer(soundFile);
                if (player != null)
                {
                    player.Play();
                }
            }
            catch
            {
                // Fallback gracefully if sound device is busy or unavailable
            }
        }

        public void StartAlarmLoop()
        {
            if (!_isEnabled) return;
            try
            {
                StopAlarmLoop();
                _alarmPlayer = GetPlayer("alarm.wav");
                if (_alarmPlayer != null)
                {
                    _alarmPlayer.PlayLooping();
                }
            }
            catch { }
        }

        public void StopAlarmLoop()
        {
            try
            {
                if (_alarmPlayer != null)
                {
                    _alarmPlayer.Stop();
                    _alarmPlayer = null;
                }
            }
            catch { }
        }

        private SoundPlayer GetPlayer(string fileName)
        {
            if (_cache.TryGetValue(fileName, out var player))
                return player;

            string fullPath = Path.Combine(_soundsDir, fileName);
            if (File.Exists(fullPath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    var ms = new MemoryStream(bytes);
                    _activeStreams.Add(ms);
                    player = new SoundPlayer(ms);
                    player.Load();
                    _cache[fileName] = player;
                    return player;
                }
                catch
                {
                    player = new SoundPlayer(fullPath);
                    try { player.Load(); } catch { }
                    _cache[fileName] = player;
                    return player;
                }
            }

            return null;
        }

        public void Dispose()
        {
            StopAlarmLoop();
            foreach (var p in _cache.Values)
            {
                try { p.Dispose(); } catch { }
            }
            _cache.Clear();

            foreach (var ms in _activeStreams)
            {
                try { ms.Dispose(); } catch { }
            }
            _activeStreams.Clear();
        }
    }
}
