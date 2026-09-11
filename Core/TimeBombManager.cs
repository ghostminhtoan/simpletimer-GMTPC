using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace TimeBomb.Core
{
    public enum AppMode
    {
        Timer,
        Stopwatch,
        Clock
    }

    public class TimeBombManager : IDisposable
    {
        private readonly SoundManager _sound;
        private readonly SettingsManager _settings;
        private readonly LowLevelKeyboardHook _hook;
        private readonly DispatcherTimer _tickTimer;
        private readonly DispatcherTimer _blinkTimer;
        private readonly DispatcherTimer _adjustTimer;
        private readonly DispatcherTimer _alarmAutoStopTimer;

        // Current state
        public AppMode Mode { get; private set; } = AppMode.Timer;
        public bool IsVisible { get; private set; } = false;
        public bool IsRunning { get; private set; } = false;
        public bool IsPaused { get; private set; } = false;
        public bool IsAlarmActive { get; private set; } = false;
        private bool IsWinKeyHeld => _hook != null && _hook.IsWinKeyHeld;
        public int TimerMinutes => _timerMinutes;
        public int TimerSeconds => _timerSeconds;
        public int StopwatchMinutes => (int)(_stopwatchElapsedSeconds / 60);
        public int StopwatchSeconds => (int)(_stopwatchElapsedSeconds % 60);

        // Timer mode state
        private int _timerMinutes = 3;
        private int _timerSeconds = 0;
        private int _lastResetMinutes = 3;
        private int _lastResetSeconds = 0;
        private DateTime _timerStartTime;
        private DateTime _targetEndTime = DateTime.Now.AddMinutes(3);
        private double _totalTimerSeconds = 180;
        private double _pausedRemainingSeconds = 180;
        private bool _startedWithWinHeld = false;
        private bool _isBelow10 = false;

        // Stopwatch mode state
        private DateTime _stopwatchStartTime;
        private double _stopwatchElapsedSeconds = 0;
        private string _stopwatchStartedDisplay = "";
        private bool _stopwatchFreshLaunch = true;

        // Blinking
        private bool _blinkVisible = true;

        // Adjust state
        private bool _adjustingUp = false;
        private bool _adjustingDown = false;
        private Stopwatch _adjustStopwatch = new Stopwatch();
        private DateTime _lastAdjustTime = DateTime.MinValue;

        // UI Notification event
        public event Action<string, string, string, bool, bool> OnDisplayChanged;
        // mainText, prefixText, subText, isRed, isBlinkHidden
        public event Action<bool> OnVisibilityToggled;
        public event Action OnAlarmTriggered;
        public event Action OnAlarmDismissed;

        public TimeBombManager(SoundManager sound, SettingsManager settings, LowLevelKeyboardHook hook, bool autoWireHook = false)
        {
            _sound = sound;
            _settings = settings;
            _hook = hook;

            // Load saved settings
            if (Enum.TryParse(settings.Mode, true, out AppMode savedMode))
            {
                Mode = savedMode;
            }
            _lastResetMinutes = settings.LastSetMinutes;
            _lastResetSeconds = settings.LastSetSeconds;
            _timerMinutes = _lastResetMinutes;
            _timerSeconds = _lastResetSeconds;
            _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
            _timerStartTime = DateTime.Now;
            _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
            _pausedRemainingSeconds = _totalTimerSeconds;

            // Main high-precision tick timer (50ms)
            _tickTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _tickTimer.Tick += OnTick;

            // Blink timer (500ms)
            _blinkTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _blinkTimer.Tick += OnBlinkTick;

            // Adjust acceleration timer (30ms)
            _adjustTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _adjustTimer.Tick += OnAdjustTick;

            // Alarm 30-second auto-stop timer
            _alarmAutoStopTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _alarmAutoStopTimer.Tick += (s, ev) => DismissAlarm();

            // Connect hook events if requested
            if (autoWireHook && _hook != null)
            {
                _hook.OnToggleRequested += Toggle;
                _hook.OnPauseToggleRequested += PauseToggle;
                _hook.OnResetRequested += Reset;
                _hook.OnSaveRequested += SaveCountdown;
                _hook.OnSwitchModeRequested += SwitchMode;

                _hook.OnAdjustUpStart += AdjustUpStart;
                _hook.OnAdjustUpStop += AdjustUpStop;
                _hook.OnAdjustDownStart += AdjustDownStart;
                _hook.OnAdjustDownStop += AdjustDownStop;

                _hook.OnWinKeyReleased += OnWinKeyReleased;
            }
        }

        public void Toggle()
        {
            if (IsAlarmActive)
            {
                DismissAlarm();
                return;
            }

            if (IsVisible)
            {
                Stop();
            }
            else
            {
                Start();
            }
        }

        public void Start(bool playSound = true)
        {
            IsVisible = true;
            IsPaused = false;
            _startedWithWinHeld = IsWinKeyHeld;

            if (Mode == AppMode.Timer)
            {
                _timerMinutes = _lastResetMinutes;
                _timerSeconds = _lastResetSeconds;
                _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
                _timerStartTime = DateTime.Now;
                _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                _pausedRemainingSeconds = _totalTimerSeconds;
                _isBelow10 = false;
            }
            else if (Mode == AppMode.Stopwatch)
            {
                _stopwatchElapsedSeconds = 0;
                _stopwatchStartTime = DateTime.Now;
                _stopwatchStartedDisplay = DateTime.Now.ToString("HH:mm:ss");
                _stopwatchFreshLaunch = true;
            }

            IsRunning = true;
            _blinkVisible = true;
            _tickTimer.Start();
            _blinkTimer.Start();

            if (playSound)
            {
                _sound.Play("start.wav");
            }

            UpdateDisplay();
            OnVisibilityToggled?.Invoke(true);
        }

        public void Stop()
        {
            _tickTimer.Stop();
            _blinkTimer.Stop();
            _adjustTimer.Stop();

            IsRunning = false;
            IsVisible = false;
            IsPaused = false;
            _isBelow10 = false;
            _adjustingUp = false;
            _adjustingDown = false;
            _blinkVisible = true;

            if (Mode == AppMode.Timer)
            {
                // Restore to user's saved default countdown mark when timer is closed/hidden
                _lastResetMinutes = _settings.LastSetMinutes;
                _lastResetSeconds = _settings.LastSetSeconds;
                _timerMinutes = _lastResetMinutes;
                _timerSeconds = _lastResetSeconds;
                _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
            }

            OnVisibilityToggled?.Invoke(false);
        }

        public void PauseToggle()
        {
            if (!IsVisible || IsAlarmActive) return;

            if (IsPaused)
            {
                // Unpause
                IsPaused = false;
                IsRunning = true;
                _blinkVisible = true;

                if (Mode == AppMode.Timer)
                {
                    _totalTimerSeconds = _pausedRemainingSeconds > 0 ? _pausedRemainingSeconds : (_timerMinutes * 60 + _timerSeconds);
                    _timerStartTime = DateTime.Now;
                    _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                    _startedWithWinHeld = IsWinKeyHeld;
                }
                else if (Mode == AppMode.Stopwatch)
                {
                    _stopwatchStartTime = DateTime.Now.AddSeconds(-_stopwatchElapsedSeconds);
                    _startedWithWinHeld = IsWinKeyHeld;
                    _stopwatchFreshLaunch = false;
                }

                _sound.Play("play.wav");
            }
            else
            {
                // Pause
                IsPaused = true;
                IsRunning = false;
                if (Mode == AppMode.Timer)
                {
                    _pausedRemainingSeconds = Math.Max(0, (_targetEndTime - DateTime.Now).TotalSeconds);
                }
                _sound.Play("pause.wav");
            }

            UpdateDisplay();
        }

        public void Reset()
        {
            if (IsAlarmActive)
            {
                ResetAlarm();
                return;
            }

            if (!IsVisible) return;

            bool wasRunning = IsRunning && !IsPaused;
            bool wasPaused = IsPaused;

            _adjustTimer.Stop();
            _adjustingUp = false;
            _adjustingDown = false;
            _isBelow10 = false;
            _blinkVisible = true;

            if (Mode == AppMode.Timer)
            {
                _timerMinutes = _lastResetMinutes;
                _timerSeconds = _lastResetSeconds;
                _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
                _timerStartTime = DateTime.Now;
                _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                _pausedRemainingSeconds = _totalTimerSeconds;
            }
            else if (Mode == AppMode.Stopwatch)
            {
                _stopwatchElapsedSeconds = 0;
                _stopwatchStartTime = DateTime.Now;
                _stopwatchStartedDisplay = DateTime.Now.ToString("HH:mm:ss");
                _stopwatchFreshLaunch = true;
            }

            _startedWithWinHeld = IsWinKeyHeld;

            if (wasPaused)
            {
                IsPaused = true;
                IsRunning = false;
            }
            else if (wasRunning)
            {
                IsPaused = false;
                IsRunning = true;
            }

            _sound.Play("reset.wav");
            UpdateDisplay();
        }

        public void SaveCountdown()
        {
            if (IsVisible && Mode == AppMode.Timer)
            {
                int saveMins;
                int saveSecs;
                if (IsRunning && !IsPaused)
                {
                    saveMins = _lastResetMinutes;
                    saveSecs = _lastResetSeconds;
                }
                else
                {
                    saveMins = _timerMinutes;
                    saveSecs = _timerSeconds;
                }

                int total = saveMins * 60 + saveSecs;
                if (total > 0)
                {
                    _lastResetMinutes = saveMins;
                    _lastResetSeconds = saveSecs;
                    _settings.LastSetMinutes = _lastResetMinutes;
                    _settings.LastSetSeconds = _lastResetSeconds;
                    _settings.Save();
                    _sound.Play("adjust.wav");
                }
            }
        }

        public void SetTimer(int minutes, int seconds)
        {
            if (Mode != AppMode.Timer) return;
            int total = minutes * 60 + seconds;
            if (total < 1) total = 1;
            if (total > 999 * 60 + 59) total = 999 * 60 + 59;

            _timerMinutes = total / 60;
            _timerSeconds = total % 60;
            _totalTimerSeconds = total;
            _timerStartTime = DateTime.Now;
            _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
            _pausedRemainingSeconds = _totalTimerSeconds;
            _lastResetMinutes = _timerMinutes;
            _lastResetSeconds = _timerSeconds;

            IsRunning = true;
            IsPaused = false;
            _isBelow10 = (_timerMinutes == 0 && _timerSeconds <= 10);
            _blinkVisible = true;
            _tickTimer.Start();
            _blinkTimer.Start();

            _sound.Play("adjust.wav");
            UpdateDisplay();
        }

        public void SetStopwatchTime(int minutes, int seconds)
        {
            if (Mode != AppMode.Stopwatch) return;
            int total = Math.Max(0, minutes * 60 + seconds);
            if (total > 999 * 60 + 59) total = 999 * 60 + 59;

            _stopwatchElapsedSeconds = total;
            _stopwatchStartTime = DateTime.Now.AddSeconds(-_stopwatchElapsedSeconds);
            _stopwatchFreshLaunch = false;

            _blinkVisible = true;
            if (IsVisible)
            {
                _tickTimer.Start();
                _blinkTimer.Start();
            }

            _sound.Play("adjust.wav");
            UpdateDisplay();
        }

        public void SwitchMode()
        {
            if (IsAlarmActive) return;

            bool wasVisible = IsVisible;
            if (wasVisible)
            {
                _tickTimer.Stop();
                _blinkTimer.Stop();
                _adjustTimer.Stop();
                IsRunning = false;
                _blinkVisible = true;
            }

            if (Mode == AppMode.Timer)
            {
                Mode = AppMode.Stopwatch;
                _sound.Play("switch_stopwatch.wav");
            }
            else if (Mode == AppMode.Stopwatch)
            {
                Mode = AppMode.Clock;
                _sound.Play("switch_timer.wav");
            }
            else
            {
                Mode = AppMode.Timer;
                _timerMinutes = _lastResetMinutes;
                _timerSeconds = _lastResetSeconds;
                _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
                _timerStartTime = DateTime.Now;
                _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                _pausedRemainingSeconds = _totalTimerSeconds;
                _sound.Play("switch_timer.wav");
            }

            _settings.Mode = Mode.ToString().ToLowerInvariant();
            _settings.Save();

            if (wasVisible)
            {
                Start(playSound: false);
            }
        }

        public void OnWinKeyReleased()
        {
            Unfreeze();
        }

        public void Unfreeze()
        {
            // Freezing release handling
            if (Mode == AppMode.Stopwatch)
            {
                if (IsVisible && IsRunning && !IsPaused && _startedWithWinHeld)
                {
                    _stopwatchStartTime = DateTime.Now.AddSeconds(-_stopwatchElapsedSeconds);
                    _startedWithWinHeld = false;
                    _stopwatchFreshLaunch = false;
                }
            }
            else if (Mode == AppMode.Timer)
            {
                if (IsVisible && !IsPaused && _startedWithWinHeld && !_adjustingUp && !_adjustingDown)
                {
                    _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
                    _timerStartTime = DateTime.Now;
                    _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                    _pausedRemainingSeconds = _totalTimerSeconds;
                    IsRunning = true;
                    _startedWithWinHeld = false;
                }
            }
        }

        public void AdjustMinutes(int delta)
        {
            if (!IsVisible || IsPaused || Mode != AppMode.Timer) return;
            int target = _timerMinutes + delta;
            if (target < 1) target = 1;
            if (target > 999) target = 999;
            if (target != _timerMinutes)
            {
                _timerMinutes = target;
                _timerSeconds = 0;
                _totalTimerSeconds = _timerMinutes * 60;
                _timerStartTime = DateTime.Now;
                _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                _pausedRemainingSeconds = _totalTimerSeconds;
                _lastResetMinutes = _timerMinutes;
                _lastResetSeconds = 0;
                _sound.Play("adjust.wav");
                UpdateDisplay();
            }
        }

        #region Adjust Up / Down Acceleration
        public void AdjustUpStart()
        {
            if (!IsVisible || IsPaused || Mode != AppMode.Timer) return;

            _adjustingUp = true;
            _adjustStopwatch.Restart();
            _lastAdjustTime = DateTime.Now;
            _timerSeconds = 0;

            if (_timerMinutes < 999)
            {
                _timerMinutes++;
                _totalTimerSeconds = _timerMinutes * 60;
                _timerStartTime = DateTime.Now;
                _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                _pausedRemainingSeconds = _totalTimerSeconds;
                _sound.Play("adjust.wav");
                UpdateDisplay();
            }

            _adjustTimer.Start();
        }

        public void AdjustUpStop()
        {
            if (!_adjustingUp) return;
            _adjustingUp = false;
            _adjustTimer.Stop();
            _adjustStopwatch.Stop();

            _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
            _timerStartTime = DateTime.Now;
            _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
            _pausedRemainingSeconds = _totalTimerSeconds;
            _lastResetMinutes = _timerMinutes;
            _lastResetSeconds = _timerSeconds;

            if (_hook != null && _hook.IsWinKeyHeld)
            {
                _startedWithWinHeld = true;
            }
            else
            {
                IsRunning = true;
                _startedWithWinHeld = false;
            }
        }

        public void AdjustDownStart()
        {
            if (!IsVisible || IsPaused || Mode != AppMode.Timer) return;

            int totalSec = _timerMinutes * 60 + _timerSeconds;
            if (totalSec <= 60) return; // Minimum 1:00

            _adjustingDown = true;
            _adjustStopwatch.Restart();
            _lastAdjustTime = DateTime.Now;
            _timerSeconds = 0;

            if (_timerMinutes > 1)
            {
                _timerMinutes--;
                _totalTimerSeconds = _timerMinutes * 60;
                _timerStartTime = DateTime.Now;
                _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                _pausedRemainingSeconds = _totalTimerSeconds;
                _sound.Play("adjust.wav");
                UpdateDisplay();
            }

            _adjustTimer.Start();
        }

        public void AdjustDownStop()
        {
            if (!_adjustingDown) return;
            _adjustingDown = false;
            _adjustTimer.Stop();
            _adjustStopwatch.Stop();

            _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
            _timerStartTime = DateTime.Now;
            _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
            _pausedRemainingSeconds = _totalTimerSeconds;
            _lastResetMinutes = _timerMinutes;
            _lastResetSeconds = _timerSeconds;

            if (_hook != null && _hook.IsWinKeyHeld)
            {
                _startedWithWinHeld = true;
            }
            else
            {
                IsRunning = true;
                _startedWithWinHeld = false;
            }
        }

        private void OnAdjustTick(object sender, EventArgs e)
        {
            if (!_adjustingUp && !_adjustingDown)
            {
                _adjustTimer.Stop();
                return;
            }

            double holdSeconds = _adjustStopwatch.Elapsed.TotalSeconds;
            double delayMs = 250;
            if (holdSeconds < 0.35) delayMs = 250;
            else if (holdSeconds < 0.90) delayMs = 120;
            else if (holdSeconds < 1.50) delayMs = 40;
            else if (holdSeconds < 2.00) delayMs = 20;
            else delayMs = 1;

            if ((DateTime.Now - _lastAdjustTime).TotalMilliseconds >= delayMs)
            {
                if (_adjustingUp && _timerMinutes < 999)
                {
                    _timerMinutes++;
                    _timerSeconds = 0;
                    _lastAdjustTime = DateTime.Now;
                    _totalTimerSeconds = _timerMinutes * 60;
                    _timerStartTime = DateTime.Now;
                    _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                    _pausedRemainingSeconds = _totalTimerSeconds;
                    UpdateDisplay();
                }
                else if (_adjustingDown && _timerMinutes > 1)
                {
                    _timerMinutes--;
                    _timerSeconds = 0;
                    _lastAdjustTime = DateTime.Now;
                    _totalTimerSeconds = _timerMinutes * 60;
                    _timerStartTime = DateTime.Now;
                    _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
                    _pausedRemainingSeconds = _totalTimerSeconds;
                    UpdateDisplay();
                }
            }
        }
        #endregion

        #region Ticks & Display
        private void OnBlinkTick(object sender, EventArgs e)
        {
            if (!IsVisible) return;

            if (IsPaused || (_isBelow10 && IsRunning))
            {
                _blinkVisible = !_blinkVisible;
                UpdateDisplay();
            }
            else if (!_blinkVisible)
            {
                _blinkVisible = true;
                UpdateDisplay();
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!IsVisible) return;

            if (Mode == AppMode.Clock)
            {
                UpdateDisplay();
                return;
            }

            if (IsPaused)
            {
                return;
            }

            bool isFrozen = (_adjustingUp || _adjustingDown);
            if (isFrozen)
            {
                if (Mode == AppMode.Stopwatch && _stopwatchFreshLaunch)
                {
                    _stopwatchStartedDisplay = DateTime.Now.ToString("HH:mm:ss");
                }
                UpdateDisplay();
                return;
            }

            if (Mode == AppMode.Timer)
            {
                double remaining = Math.Max(0, (_targetEndTime - DateTime.Now).TotalSeconds);

                if (remaining <= 0)
                {
                    IsRunning = false;
                    _timerMinutes = 0;
                    _timerSeconds = 0;
                    _tickTimer.Stop();
                    _blinkTimer.Stop();
                    TriggerAlarm();
                    return;
                }

                int totalRemainingSec = (int)Math.Ceiling(remaining);
                _timerMinutes = totalRemainingSec / 60;
                _timerSeconds = totalRemainingSec % 60;

                _isBelow10 = (_timerMinutes == 0 && _timerSeconds <= 10);
            }
            else if (Mode == AppMode.Stopwatch)
            {
                _stopwatchElapsedSeconds = (DateTime.Now - _stopwatchStartTime).TotalSeconds;
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            string mainText;
            string prefixText;
            string subText;
            bool isRed = false;
            bool isBlinkHidden = false;

            if (Mode == AppMode.Clock)
            {
                mainText = DateTime.Now.ToString("HH:mm:ss");
                prefixText = "Today:";
                subText = DateTime.Now.ToString("yyyy-MM-dd");
            }
            else if (Mode == AppMode.Stopwatch)
            {
                int mins = (int)(_stopwatchElapsedSeconds / 60);
                int secs = (int)(_stopwatchElapsedSeconds % 60);
                mainText = string.Format(mins >= 100 ? "{0:D3}:{1:D2}" : "{0:D2}:{1:D2}", mins, secs);
                prefixText = "Started:";
                subText = _stopwatchStartedDisplay;

                if (IsPaused && !_blinkVisible)
                {
                    isBlinkHidden = true;
                }
            }
            else // Timer
            {
                mainText = string.Format(_timerMinutes >= 100 ? "{0:D3}:{1:D2}" : "{0:D2}:{1:D2}", _timerMinutes, _timerSeconds);
                prefixText = "Ends at:";
                subText = _targetEndTime.ToString("HH:mm:ss");

                if (IsPaused)
                {
                    if (!_blinkVisible) isBlinkHidden = true;
                }
                else if (_isBelow10)
                {
                    isRed = _blinkVisible; // Alternates red / normal
                }
            }

            OnDisplayChanged?.Invoke(mainText, prefixText, subText, isRed, isBlinkHidden);
        }
        #endregion

        #region Alarm
        private void TriggerAlarm()
        {
            IsAlarmActive = true;
            _sound.StartAlarmLoop();
            _alarmAutoStopTimer.Stop();
            _alarmAutoStopTimer.Start();
            OnAlarmTriggered?.Invoke();
        }

        public void DismissAlarm()
        {
            _alarmAutoStopTimer.Stop();
            IsAlarmActive = false;
            _sound.StopAlarmLoop();
            OnAlarmDismissed?.Invoke();
            Stop();
        }

        public void ResetAlarm()
        {
            _alarmAutoStopTimer.Stop();
            IsAlarmActive = false;
            _sound.StopAlarmLoop();
            OnAlarmDismissed?.Invoke();

            _timerMinutes = _lastResetMinutes;
            _timerSeconds = _lastResetSeconds;
            _totalTimerSeconds = _timerMinutes * 60 + _timerSeconds;
            _timerStartTime = DateTime.Now;
            _targetEndTime = _timerStartTime.AddSeconds(_totalTimerSeconds);
            _pausedRemainingSeconds = _totalTimerSeconds;

            IsRunning = true;
            IsPaused = false;
            _isBelow10 = false;
            _startedWithWinHeld = false;
            _blinkVisible = true;

            _tickTimer.Start();
            _blinkTimer.Start();

            _sound.Play("reset.wav");
            UpdateDisplay();
            OnVisibilityToggled?.Invoke(true);
        }
        #endregion

        public void Dispose()
        {
            Stop();
            _sound?.Dispose();
            _hook?.Dispose();
        }
    }
}
