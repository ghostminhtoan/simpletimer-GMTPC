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
        private SoundPlayer _alarmPlayer;

        public SoundManager()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            _soundsDir = Path.Combine(appDir, "Resources", "Sounds");
        }

        public void Play(string soundFile)
        {
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
                player = new SoundPlayer(fullPath);
                try { player.Load(); } catch { }
                _cache[fileName] = player;
                return player;
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
        }
    }
}
