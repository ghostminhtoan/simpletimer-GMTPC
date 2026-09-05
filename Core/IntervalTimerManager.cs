using System;
using System.Windows.Threading;

namespace TimeBomb.Core
{
    public enum IntervalPhase
    {
        Idle,
        Prepare,
        Work,
        Rest,
        End,
        Completed
    }

    public class IntervalTimerManager : IDisposable
    {
        private readonly SoundManager _sound;
        private readonly SettingsManager _settings;
        private readonly DispatcherTimer _tickTimer;
        private readonly DispatcherTimer _blinkTimer;

        public IntervalPhase CurrentPhase { get; private set; } = IntervalPhase.Idle;
        public int CurrentLoop { get; private set; } = 1;
        public int TotalLoops { get; set; } = 3;

        public int PrepareSeconds { get; set; } = 5;
        public int WorkSeconds { get; set; } = 30;
        public int RestSeconds { get; set; } = 10;
        public int EndSeconds { get; set; } = 5;

        public bool IsRunning { get; private set; } = false;
        public bool IsPaused { get; private set; } = false;
        public bool IsVisible { get; private set; } = false;

        private DateTime _phaseStartTime;
        private DateTime _phaseEndTime;
        private double _pausedRemainingSeconds = 0;
        private bool _blinkVisible = true;
        private int _lastWarningSecond = -1;

        // Display update: mainTime, phaseBadge, loopInfo, totalProgress, isRed, isBlinkHidden
        public event Action<string, string, string, string, bool, bool> OnDisplayChanged;
        public event Action<IntervalPhase> OnPhaseChanged;
        public event Action<bool> OnVisibilityToggled;

        public IntervalTimerManager(SoundManager sound, SettingsManager settings)
        {
            _sound = sound;
            _settings = settings;

            PrepareSeconds = settings.IntervalPrepare;
            WorkSeconds = settings.IntervalWork;
            RestSeconds = settings.IntervalRest;
            EndSeconds = settings.IntervalEnd;
            TotalLoops = settings.IntervalLoops;

            _tickTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _tickTimer.Tick += OnTick;

            _blinkTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _blinkTimer.Tick += (s, e) =>
            {
                if (IsPaused)
                {
                    _blinkVisible = !_blinkVisible;
                    UpdateDisplay();
                }
                else if (!_blinkVisible)
                {
                    _blinkVisible = true;
                    UpdateDisplay();
                }
            };
        }

        public void SaveSettings()
        {
            _settings.IntervalPrepare = PrepareSeconds;
            _settings.IntervalWork = WorkSeconds;
            _settings.IntervalRest = RestSeconds;
            _settings.IntervalEnd = EndSeconds;
            _settings.IntervalLoops = TotalLoops;
            _settings.Save();
        }

        public void Start()
        {
            IsVisible = true;
            IsPaused = false;
            CurrentLoop = 1;
            SaveSettings();

            if (PrepareSeconds > 0)
            {
                SwitchToPhase(IntervalPhase.Prepare, PrepareSeconds);
            }
            else
            {
                SwitchToPhase(IntervalPhase.Work, WorkSeconds);
            }

            IsRunning = true;
            _blinkVisible = true;
            _tickTimer.Start();
            _blinkTimer.Start();

            _sound?.Play("start.wav");
            UpdateDisplay();
            OnVisibilityToggled?.Invoke(true);
        }

        public void Stop()
        {
            _tickTimer.Stop();
            _blinkTimer.Stop();
            IsRunning = false;
            IsPaused = false;
            CurrentPhase = IntervalPhase.Idle;
            CurrentLoop = 1;
            _blinkVisible = true;
            UpdateDisplay();
        }

        public void PauseToggle()
        {
            if (!IsRunning && CurrentPhase == IntervalPhase.Idle) return;

            if (IsPaused)
            {
                // Unpause
                IsPaused = false;
                IsRunning = true;
                _blinkVisible = true;
                _phaseStartTime = DateTime.Now;
                _phaseEndTime = _phaseStartTime.AddSeconds(_pausedRemainingSeconds);
                _sound?.Play("play.wav");
            }
            else
            {
                // Pause
                IsPaused = true;
                IsRunning = false;
                _pausedRemainingSeconds = Math.Max(0, (_phaseEndTime - DateTime.Now).TotalSeconds);
                _sound?.Play("pause.wav");
            }

            UpdateDisplay();
        }

        private void SwitchToPhase(IntervalPhase nextPhase, int durationSeconds)
        {
            CurrentPhase = nextPhase;
            int totalSec = Math.Max(1, durationSeconds);
            _phaseStartTime = DateTime.Now;
            _phaseEndTime = _phaseStartTime.AddSeconds(totalSec);
            _pausedRemainingSeconds = totalSec;
            _lastWarningSecond = -1;

            OnPhaseChanged?.Invoke(nextPhase);

            switch (nextPhase)
            {
                case IntervalPhase.Prepare:
                    _sound?.Play("adjust.wav");
                    break;
                case IntervalPhase.Work:
                    _sound?.Play("play.wav");
                    break;
                case IntervalPhase.Rest:
                    _sound?.Play("pause.wav");
                    break;
                case IntervalPhase.End:
                    _sound?.Play("alarm.wav");
                    break;
                case IntervalPhase.Completed:
                    _sound?.Play("reset.wav");
                    break;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!IsRunning || IsPaused || CurrentPhase == IntervalPhase.Idle)
            {
                UpdateDisplay();
                return;
            }

            double remaining = Math.Max(0, (_phaseEndTime - DateTime.Now).TotalSeconds);

            if (remaining <= 0)
            {
                AdvancePhase();
                return;
            }

            // 3-second warning sound before Work and Rest phases
            int remainingSecInt = (int)Math.Ceiling(remaining);
            if (remainingSecInt >= 1 && remainingSecInt <= 3 && _lastWarningSecond != remainingSecInt)
            {
                bool isNextWorkOrRest = false;
                if (CurrentPhase == IntervalPhase.Prepare || CurrentPhase == IntervalPhase.Rest)
                {
                    // Transitioning to Work
                    isNextWorkOrRest = true;
                }
                else if (CurrentPhase == IntervalPhase.Work && RestSeconds > 0)
                {
                    // Transitioning to Rest
                    isNextWorkOrRest = true;
                }

                if (isNextWorkOrRest)
                {
                    _lastWarningSecond = remainingSecInt;
                    _sound?.Play("adjust.wav");
                }
            }

            UpdateDisplay();
        }

        private void AdvancePhase()
        {
            switch (CurrentPhase)
            {
                case IntervalPhase.Prepare:
                    // After Prepare -> Start Work Loop 1
                    CurrentLoop = 1;
                    SwitchToPhase(IntervalPhase.Work, WorkSeconds);
                    break;

                case IntervalPhase.Work:
                    // After Work -> Go to Rest if Rest > 0, or check next loop
                    if (RestSeconds > 0)
                    {
                        SwitchToPhase(IntervalPhase.Rest, RestSeconds);
                    }
                    else
                    {
                        if (CurrentLoop < TotalLoops)
                        {
                            CurrentLoop++;
                            SwitchToPhase(IntervalPhase.Work, WorkSeconds);
                        }
                        else
                        {
                            if (EndSeconds > 0) SwitchToPhase(IntervalPhase.End, EndSeconds);
                            else CompleteInterval();
                        }
                    }
                    break;

                case IntervalPhase.Rest:
                    // After Rest -> Next Loop or End
                    if (CurrentLoop < TotalLoops)
                    {
                        CurrentLoop++;
                        SwitchToPhase(IntervalPhase.Work, WorkSeconds);
                    }
                    else
                    {
                        if (EndSeconds > 0) SwitchToPhase(IntervalPhase.End, EndSeconds);
                        else CompleteInterval();
                    }
                    break;

                case IntervalPhase.End:
                    CompleteInterval();
                    break;

                default:
                    Stop();
                    break;
            }
        }

        private void CompleteInterval()
        {
            CurrentPhase = IntervalPhase.Completed;
            _tickTimer.Stop();
            IsRunning = false;
            _sound?.Play("reset.wav");
            UpdateDisplay();
            OnPhaseChanged?.Invoke(IntervalPhase.Completed);
        }

        public void UpdateDisplay()
        {
            string mainText;
            string phaseTitle;
            string loopInfoText;
            string subText;
            bool isRed = false;
            bool isBlinkHidden = (IsPaused && !_blinkVisible);

            if (CurrentPhase == IntervalPhase.Idle)
            {
                mainText = "00:00";
                phaseTitle = "INTERVAL TIMER";
                loopInfoText = $"Loops: {TotalLoops}";
                subText = $"P:{PrepareSeconds}s W:{WorkSeconds}s R:{RestSeconds}s E:{EndSeconds}s";
            }
            else if (CurrentPhase == IntervalPhase.Completed)
            {
                mainText = "DONE!";
                phaseTitle = "COMPLETED";
                loopInfoText = $"Finished {TotalLoops}/{TotalLoops} Loops";
                subText = "Press Start to repeat";
            }
            else
            {
                double rem = IsPaused ? _pausedRemainingSeconds : Math.Max(0, (_phaseEndTime - DateTime.Now).TotalSeconds);
                int totalSec = (int)Math.Ceiling(rem);
                int mins = totalSec / 60;
                int secs = totalSec % 60;
                mainText = string.Format(mins >= 100 ? "{0:D3}:{1:D2}" : "{0:D2}:{1:D2}", mins, secs);

                switch (CurrentPhase)
                {
                    case IntervalPhase.Prepare:
                        phaseTitle = "PREPARE";
                        loopInfoText = "Get Ready!";
                        subText = $"Next: Work ({WorkSeconds}s)";
                        break;
                    case IntervalPhase.Work:
                        phaseTitle = "WORK";
                        loopInfoText = $"Loop {CurrentLoop} / {TotalLoops}";
                        subText = RestSeconds > 0 ? $"Next: Rest ({RestSeconds}s)" : (CurrentLoop < TotalLoops ? $"Next: Work" : $"Next: End");
                        break;
                    case IntervalPhase.Rest:
                        phaseTitle = "REST";
                        loopInfoText = $"Loop {CurrentLoop} / {TotalLoops}";
                        subText = CurrentLoop < TotalLoops ? $"Next: Work ({WorkSeconds}s)" : $"Next: End ({EndSeconds}s)";
                        break;
                    case IntervalPhase.End:
                        phaseTitle = "END";
                        loopInfoText = "Final Phase";
                        subText = "Almost done!";
                        isRed = true;
                        break;
                    default:
                        phaseTitle = "INTERVAL";
                        loopInfoText = "";
                        subText = "";
                        break;
                }
            }

            OnDisplayChanged?.Invoke(mainText, phaseTitle, loopInfoText, subText, isRed, isBlinkHidden);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
