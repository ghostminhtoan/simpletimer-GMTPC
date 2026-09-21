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
        public bool IsInfiniteLoops { get; set; } = false;

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

        private bool _hasAnnounced2Loops = false;
        private bool _hasAnnounced1Loop = false;

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
            IsInfiniteLoops = settings.IntervalInfinite || settings.IntervalLoops <= 0;

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
            _settings.IntervalInfinite = IsInfiniteLoops;
            _settings.Save();
        }

        public void Start()
        {
            IsVisible = true;
            IsPaused = false;
            CurrentLoop = 1;
            _hasAnnounced2Loops = false;
            _hasAnnounced1Loop = false;
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
            _hasAnnounced2Loops = false;
            _hasAnnounced1Loop = false;
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

        private void AnnounceLoop(int remainingLoops)
        {
            if (_sound != null && !_sound.IsEnabled) return;

            string wavName = remainingLoops == 2 ? "loop_2.wav" : "loop_1.wav";
            try
            {
                _sound?.Play(wavName);
            }
            catch
            {
                try
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            using (var synth = new System.Speech.Synthesis.SpeechSynthesizer())
                            {
                                synth.SetOutputToDefaultAudioDevice();
                                synth.Speak(remainingLoops == 2 ? "Two loops remaining" : "Final loop. One loop remaining");
                            }
                        }
                        catch { }
                    });
                }
                catch { }
            }
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
                    if (!IsInfiniteLoops)
                    {
                        int remainingLoops = TotalLoops - CurrentLoop + 1;
                        if (remainingLoops == 2 && !_hasAnnounced2Loops)
                        {
                            _hasAnnounced2Loops = true;
                            AnnounceLoop(2);
                        }
                        else if (remainingLoops == 1 && !_hasAnnounced1Loop)
                        {
                            _hasAnnounced1Loop = true;
                            AnnounceLoop(1);
                        }
                        else
                        {
                            _sound?.Play("play.wav");
                        }
                    }
                    else
                    {
                        _sound?.Play("play.wav");
                    }
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
                else if (CurrentPhase == IntervalPhase.Work)
                {
                    // Only warn transition to Rest if not the last work loop and Rest > 0
                    bool isLastWork = !IsInfiniteLoops && CurrentLoop >= TotalLoops;
                    if (!isLastWork && RestSeconds > 0)
                    {
                        isNextWorkOrRest = true;
                    }
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
                    // After Work -> Check if this is the final Work loop
                    // If final loop, DO NOT rest (e.g. 5 loops means 5 work and 4 rest)
                    bool isLastWork = !IsInfiniteLoops && CurrentLoop >= TotalLoops;
                    if (isLastWork)
                    {
                        if (EndSeconds > 0) SwitchToPhase(IntervalPhase.End, EndSeconds);
                        else CompleteInterval();
                    }
                    else
                    {
                        if (RestSeconds > 0)
                        {
                            SwitchToPhase(IntervalPhase.Rest, RestSeconds);
                        }
                        else
                        {
                            CurrentLoop++;
                            SwitchToPhase(IntervalPhase.Work, WorkSeconds);
                        }
                    }
                    break;

                case IntervalPhase.Rest:
                    // After Rest -> Always advance to next loop (final loop never enters Rest)
                    if (IsInfiniteLoops || CurrentLoop < TotalLoops)
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
                loopInfoText = IsInfiniteLoops ? "Loops: ∞ (Infinite)" : $"Loops: {TotalLoops}";
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
                        bool isLastWork = !IsInfiniteLoops && CurrentLoop >= TotalLoops;
                        int remainingLoops = !IsInfiniteLoops ? Math.Max(1, TotalLoops - CurrentLoop + 1) : 0;

                        if (IsInfiniteLoops)
                        {
                            loopInfoText = $"Loop {CurrentLoop} / ∞";
                            subText = RestSeconds > 0 ? $"Next: Rest ({RestSeconds}s)" : "Next: Work";
                        }
                        else if (isLastWork)
                        {
                            loopInfoText = $"Loop {CurrentLoop} / {TotalLoops} (Final Loop)";
                            subText = EndSeconds > 0 ? $"Next: End ({EndSeconds}s)" : "Next: Done!";
                        }
                        else if (remainingLoops == 2)
                        {
                            loopInfoText = $"Loop {CurrentLoop} / {TotalLoops} (2 Loops Left)";
                            subText = RestSeconds > 0 ? $"Next: Rest ({RestSeconds}s)" : $"Next: Work ({WorkSeconds}s)";
                        }
                        else
                        {
                            loopInfoText = $"Loop {CurrentLoop} / {TotalLoops}";
                            subText = RestSeconds > 0 ? $"Next: Rest ({RestSeconds}s)" : $"Next: Work ({WorkSeconds}s)";
                        }
                        break;
                    case IntervalPhase.Rest:
                        phaseTitle = "REST";
                        int nextRemaining = !IsInfiniteLoops ? Math.Max(1, TotalLoops - (CurrentLoop + 1) + 1) : 0;
                        bool nextIsLast = !IsInfiniteLoops && (CurrentLoop + 1) >= TotalLoops;
                        int totalRests = Math.Max(1, TotalLoops - 1);

                        if (IsInfiniteLoops)
                        {
                            loopInfoText = $"Rest after Loop {CurrentLoop}";
                            subText = $"Next: Work ({WorkSeconds}s)";
                        }
                        else if (nextIsLast)
                        {
                            loopInfoText = $"Rest {CurrentLoop} / {totalRests}";
                            subText = $"Next: Final Loop ({WorkSeconds}s)";
                        }
                        else if (nextRemaining == 2)
                        {
                            loopInfoText = $"Rest {CurrentLoop} / {totalRests}";
                            subText = $"Next: Work ({WorkSeconds}s) - 2 Loops Left";
                        }
                        else
                        {
                            loopInfoText = $"Rest {CurrentLoop} / {totalRests}";
                            subText = $"Next: Work ({WorkSeconds}s)";
                        }
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
