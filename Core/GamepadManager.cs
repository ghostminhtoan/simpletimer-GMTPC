using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TimeBomb.Core
{
    [Flags]
    public enum GamepadButtons
    {
        None = 0,
        A = 1 << 0,          // Xbox A / PS4 Cross
        B = 1 << 1,          // Xbox B / PS4 Circle
        X = 1 << 2,          // Xbox X / PS4 Square
        Y = 1 << 3,          // Xbox Y / PS4 Triangle
        DPadUp = 1 << 4,
        DPadDown = 1 << 5,
        DPadLeft = 1 << 6,
        DPadRight = 1 << 7,
        LeftBumper = 1 << 8, // LB / L1
        RightBumper = 1 << 9,// RB / R1
        Start = 1 << 10,     // Menu / Options
        Back = 1 << 11,      // View / Share
        L3 = 1 << 12,        // Left stick click
        R3 = 1 << 13,        // Right stick click
        Guide = 1 << 14      // Xbox Guide / PS Button
    }

    public class GamepadManager : IDisposable
    {
        #region XInput Native Structs & Delegates
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_VIBRATION
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int XInputGetStateDelegate(int dwUserIndex, out XINPUT_STATE pState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int XInputSetStateDelegate(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

        private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
        private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
        private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
        private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
        private const ushort XINPUT_GAMEPAD_START = 0x0010;
        private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
        private const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
        private const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
        private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
        private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
        private const ushort XINPUT_GAMEPAD_A = 0x1000;
        private const ushort XINPUT_GAMEPAD_B = 0x2000;
        private const ushort XINPUT_GAMEPAD_X = 0x4000;
        private const ushort XINPUT_GAMEPAD_Y = 0x8000;

        private static XInputGetStateDelegate _xInputGetState;
        private static XInputSetStateDelegate _xInputSetState;
        private static bool _xInputLoaded = false;
        private static IntPtr _xInputModule = IntPtr.Zero;
        #endregion

        #region Events
        public event Action OnToggleRequested;
        public event Action OnPauseToggleRequested;
        public event Action OnResetRequested;
        public event Action OnSaveRequested;
        public event Action OnSwitchModeRequested;
        public event Action OnNewInstanceRequested;
        public event Action OnCloseInstanceRequested;

        public event Action OnAdjustUpStart;
        public event Action OnAdjustUpStop;
        public event Action OnAdjustDownStart;
        public event Action OnAdjustDownStop;

        public event Action OnDismissAlarmRequested;
        public event Action OnActionExecuted;
        #endregion

        private readonly SettingsManager _settings;
        private readonly DispatcherTimer _pollTimer;
        private readonly DispatcherTimer _alarmRumbleTimer;

        private GamepadButtons _prevButtons = GamepadButtons.None;
        private bool _chordExecuted = false;
        private bool _isUpHeld = false;
        private bool _isDownHeld = false;
        private int _activeXInputUserIndex = -1;
        private bool _isAlarmVibrating = false;
        private bool _alarmRumblePhase = false;
        private int _idleCounter = 0;

        public bool IsAlarmActive { get; set; } = false;
        public bool IsConnected { get; private set; } = false;

        public GamepadManager(SettingsManager settings)
        {
            _settings = settings;

            EnsureXInputLoaded();

            _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60Hz
            };
            _pollTimer.Tick += OnPollTick;

            _alarmRumbleTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _alarmRumbleTimer.Tick += OnAlarmRumbleTick;

            if (_settings.GamepadEnabled)
            {
                _pollTimer.Start();
            }
        }

        public void SetEnabled(bool enabled)
        {
            _settings.GamepadEnabled = enabled;
            if (enabled)
            {
                EnsureXInputLoaded();
                if (!_pollTimer.IsEnabled)
                {
                    _pollTimer.Start();
                }
            }
            else
            {
                _pollTimer.Stop();
                StopAlarmVibration();
            }
        }

        public void SetVibrationEnabled(bool enabled)
        {
            _settings.GamepadVibration = enabled;
            if (!enabled)
            {
                StopAlarmVibration();
            }
        }

        private static void EnsureXInputLoaded()
        {
            if (_xInputLoaded) return;

            string[] dllCandidates = { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" };
            foreach (var dll in dllCandidates)
            {
                IntPtr hModule = Win32Api.LoadLibrary(dll);
                if (hModule != IntPtr.Zero)
                {
                    IntPtr pGetState = Win32Api.GetProcAddress(hModule, "XInputGetState");
                    IntPtr pSetState = Win32Api.GetProcAddress(hModule, "XInputSetState");

                    if (pGetState != IntPtr.Zero && pSetState != IntPtr.Zero)
                    {
                        _xInputGetState = (XInputGetStateDelegate)Marshal.GetDelegateForFunctionPointer(pGetState, typeof(XInputGetStateDelegate));
                        _xInputSetState = (XInputSetStateDelegate)Marshal.GetDelegateForFunctionPointer(pSetState, typeof(XInputSetStateDelegate));
                        _xInputModule = hModule;
                        _xInputLoaded = true;
                        break;
                    }
                }
            }
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            if (!_settings.GamepadEnabled) return;

            try
            {
                GamepadButtons current = PollCurrentGamepadButtons();

                bool hasController = (current != GamepadButtons.None || _activeXInputUserIndex >= 0);
                IsConnected = hasController;

                // Adaptive polling rate to minimize background CPU usage and save battery
                if (!hasController)
                {
                    _idleCounter = 0;
                    if (_pollTimer.Interval.TotalMilliseconds < 250)
                    {
                        _pollTimer.Interval = TimeSpan.FromMilliseconds(250);
                    }
                }
                else
                {
                    if (current == GamepadButtons.None)
                    {
                        _idleCounter++;
                        // If idle for ~10 seconds (approx 600 ticks at 16ms), drop rate to 80ms (~12Hz)
                        if (_idleCounter > 600)
                        {
                            if (_pollTimer.Interval.TotalMilliseconds < 80)
                            {
                                _pollTimer.Interval = TimeSpan.FromMilliseconds(80);
                            }
                        }
                    }
                    else
                    {
                        // Instantly restore 16ms (~60Hz) fast responsiveness when user touches any button
                        _idleCounter = 0;
                        if (_pollTimer.Interval.TotalMilliseconds != 16)
                        {
                            _pollTimer.Interval = TimeSpan.FromMilliseconds(16);
                        }
                    }
                }

                ProcessGamepadState(current);
            }
            catch { }
        }

        private GamepadButtons PollCurrentGamepadButtons()
        {
            // 1. Try XInput first (Xbox, Steam Deck, DS4Windows, Steam Virtual Gamepad)
            if (_xInputLoaded && _xInputGetState != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_xInputGetState(i, out XINPUT_STATE state) == 0) // ERROR_SUCCESS
                    {
                        _activeXInputUserIndex = i;
                        return NormalizeXInput(state.Gamepad);
                    }
                }
            }

            _activeXInputUserIndex = -1;

            // 2. DirectInput / WinMM fallback (Direct PS4 DualShock 4 over USB / Bluetooth without XInput wrappers)
            try
            {
                int numDevs = Win32Api.joyGetNumDevs();
                if (numDevs > 0)
                {
                    for (int joyId = 0; joyId < Math.Min(numDevs, 4); joyId++)
                    {
                        var info = new Win32Api.JOYINFOEX
                        {
                            dwSize = Marshal.SizeOf(typeof(Win32Api.JOYINFOEX)),
                            dwFlags = Win32Api.JOY_RETURNALL
                        };

                        if (Win32Api.joyGetPosEx(joyId, ref info) == Win32Api.JOYERR_NOERROR)
                        {
                            return NormalizeWinMM(info);
                        }
                    }
                }
            }
            catch { }

            return GamepadButtons.None;
        }

        private GamepadButtons NormalizeXInput(XINPUT_GAMEPAD pad)
        {
            GamepadButtons b = GamepadButtons.None;
            ushort w = pad.wButtons;

            if ((w & XINPUT_GAMEPAD_A) != 0) b |= GamepadButtons.A;
            if ((w & XINPUT_GAMEPAD_B) != 0) b |= GamepadButtons.B;
            if ((w & XINPUT_GAMEPAD_X) != 0) b |= GamepadButtons.X;
            if ((w & XINPUT_GAMEPAD_Y) != 0) b |= GamepadButtons.Y;

            if ((w & XINPUT_GAMEPAD_DPAD_UP) != 0) b |= GamepadButtons.DPadUp;
            if ((w & XINPUT_GAMEPAD_DPAD_DOWN) != 0) b |= GamepadButtons.DPadDown;
            if ((w & XINPUT_GAMEPAD_DPAD_LEFT) != 0) b |= GamepadButtons.DPadLeft;
            if ((w & XINPUT_GAMEPAD_DPAD_RIGHT) != 0) b |= GamepadButtons.DPadRight;

            if ((w & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0) b |= GamepadButtons.LeftBumper;
            if ((w & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0) b |= GamepadButtons.RightBumper;

            if ((w & XINPUT_GAMEPAD_START) != 0) b |= GamepadButtons.Start;
            if ((w & XINPUT_GAMEPAD_BACK) != 0) b |= GamepadButtons.Back;

            if ((w & XINPUT_GAMEPAD_LEFT_THUMB) != 0) b |= GamepadButtons.L3;
            if ((w & XINPUT_GAMEPAD_RIGHT_THUMB) != 0) b |= GamepadButtons.R3;

            return b;
        }

        private GamepadButtons NormalizeWinMM(Win32Api.JOYINFOEX info)
        {
            GamepadButtons b = GamepadButtons.None;
            int buttons = info.dwButtons;

            // DualShock 4 Standard DirectInput layout:
            // Bit 0: Square, Bit 1: Cross, Bit 2: Circle, Bit 3: Triangle
            // Bit 4: L1, Bit 5: R1, Bit 6: L2, Bit 7: R2
            // Bit 8: Share, Bit 9: Options, Bit 10: L3, Bit 11: R3, Bit 12: PS Button
            if ((buttons & (1 << 1)) != 0) b |= GamepadButtons.A; // Cross
            if ((buttons & (1 << 2)) != 0) b |= GamepadButtons.B; // Circle
            if ((buttons & (1 << 0)) != 0) b |= GamepadButtons.X; // Square
            if ((buttons & (1 << 3)) != 0) b |= GamepadButtons.Y; // Triangle

            if ((buttons & (1 << 4)) != 0) b |= GamepadButtons.LeftBumper;  // L1
            if ((buttons & (1 << 5)) != 0) b |= GamepadButtons.RightBumper; // R1

            if ((buttons & (1 << 9)) != 0) b |= GamepadButtons.Start; // Options
            if ((buttons & (1 << 8)) != 0) b |= GamepadButtons.Back;  // Share
            if ((buttons & (1 << 10)) != 0) b |= GamepadButtons.L3;
            if ((buttons & (1 << 11)) != 0) b |= GamepadButtons.R3;
            if ((buttons & (1 << 12)) != 0) b |= GamepadButtons.Guide; // PS Button

            // POV Hat (D-Pad)
            int pov = info.dwPOV;
            if (pov != 65535 && pov != -1)
            {
                if (pov == 0 || pov == 36000 || pov == 31500 || pov == 4500)
                    b |= GamepadButtons.DPadUp;
                else if (pov >= 4500 && pov <= 13500)
                    b |= GamepadButtons.DPadRight;
                else if (pov >= 13500 && pov <= 22500)
                    b |= GamepadButtons.DPadDown;
                else if (pov >= 22500 && pov <= 31500)
                    b |= GamepadButtons.DPadLeft;
            }

            return b;
        }

        private void ProcessGamepadState(GamepadButtons current)
        {
            GamepadButtons justPressed = current & ~_prevButtons;
            GamepadButtons justReleased = ~current & _prevButtons;

            // 1. Alarm Active: ANY button dismisses the ringing alarm immediately
            if (IsAlarmActive)
            {
                if (justPressed != GamepadButtons.None)
                {
                    StopAlarmVibration();
                    OnDismissAlarmRequested?.Invoke();
                    PulseHaptic(30000, 30000, 80);
                    _prevButtons = current;
                    return;
                }
            }

            // 2. Modifier chord check:
            // View / Back (Xbox), Share (PS4), Guide / PS button, or L3 + R3 combo
            bool isModifierDown = (current & GamepadButtons.Back) != 0 ||
                                  (current & GamepadButtons.Guide) != 0 ||
                                  ((current & (GamepadButtons.L3 | GamepadButtons.R3)) == (GamepadButtons.L3 | GamepadButtons.R3));

            bool wasModifierDown = (_prevButtons & GamepadButtons.Back) != 0 ||
                                   (_prevButtons & GamepadButtons.Guide) != 0 ||
                                   ((_prevButtons & (GamepadButtons.L3 | GamepadButtons.R3)) == (GamepadButtons.L3 | GamepadButtons.R3));

            if (isModifierDown && !wasModifierDown)
            {
                _chordExecuted = false;
            }

            if (!isModifierDown && wasModifierDown)
            {
                // Released modifier
                if (_isUpHeld)
                {
                    _isUpHeld = false;
                    OnAdjustUpStop?.Invoke();
                }
                if (_isDownHeld)
                {
                    _isDownHeld = false;
                    OnAdjustDownStop?.Invoke();
                }

                // If user just tapped View/Share alone without holding any other button -> Toggle Show/Hide HUD
                if (!_chordExecuted)
                {
                    OnToggleRequested?.Invoke();
                    PulseHaptic(0, 25000, 50);
                }

                OnActionExecuted?.Invoke();
            }

            // 3. Chord actions while modifier is held down
            if (isModifierDown)
            {
                if ((justPressed & GamepadButtons.A) != 0)
                {
                    _chordExecuted = true;
                    OnPauseToggleRequested?.Invoke();
                    OnActionExecuted?.Invoke();
                    PulseHaptic(0, 30000, 60);
                }
                else if ((justPressed & GamepadButtons.B) != 0)
                {
                    _chordExecuted = true;
                    OnResetRequested?.Invoke();
                    OnActionExecuted?.Invoke();
                    PulseHaptic(0, 30000, 60);
                }
                else if ((justPressed & GamepadButtons.X) != 0)
                {
                    _chordExecuted = true;
                    OnSwitchModeRequested?.Invoke();
                    OnActionExecuted?.Invoke();
                    PulseHaptic(0, 30000, 60);
                }
                else if ((justPressed & GamepadButtons.Y) != 0)
                {
                    _chordExecuted = true;
                    OnSaveRequested?.Invoke();
                    OnActionExecuted?.Invoke();
                    PulseHaptic(0, 30000, 60);
                }
                else if ((justPressed & GamepadButtons.DPadRight) != 0)
                {
                    _chordExecuted = true;
                    OnNewInstanceRequested?.Invoke();
                    OnActionExecuted?.Invoke();
                    PulseHaptic(0, 35000, 60);
                }
                else if ((justPressed & GamepadButtons.DPadLeft) != 0)
                {
                    _chordExecuted = true;
                    OnCloseInstanceRequested?.Invoke();
                    OnActionExecuted?.Invoke();
                    PulseHaptic(0, 35000, 60);
                }

                // Up (DPad Up or Right Bumper) - supports continuous acceleration
                bool isUpPressedNow = (current & (GamepadButtons.DPadUp | GamepadButtons.RightBumper)) != 0;
                if (isUpPressedNow && !_isUpHeld)
                {
                    _chordExecuted = true;
                    _isUpHeld = true;
                    OnAdjustUpStart?.Invoke();
                    PulseHaptic(0, 20000, 40);
                }
                else if (!isUpPressedNow && _isUpHeld)
                {
                    _isUpHeld = false;
                    OnAdjustUpStop?.Invoke();
                    OnActionExecuted?.Invoke();
                }

                // Down (DPad Down or Left Bumper) - supports continuous acceleration
                bool isDownPressedNow = (current & (GamepadButtons.DPadDown | GamepadButtons.LeftBumper)) != 0;
                if (isDownPressedNow && !_isDownHeld)
                {
                    _chordExecuted = true;
                    _isDownHeld = true;
                    OnAdjustDownStart?.Invoke();
                    PulseHaptic(0, 20000, 40);
                }
                else if (!isDownPressedNow && _isDownHeld)
                {
                    _isDownHeld = false;
                    OnAdjustDownStop?.Invoke();
                    OnActionExecuted?.Invoke();
                }
            }
            else
            {
                // Standalone Start / Options button: Toggle Pause
                if ((justPressed & GamepadButtons.Start) != 0)
                {
                    OnPauseToggleRequested?.Invoke();
                    OnActionExecuted?.Invoke();
                    PulseHaptic(0, 30000, 60);
                }
            }

            _prevButtons = current;
        }

        #region Haptics & Vibration
        public void PulseHaptic(ushort leftMotor, ushort rightMotor, int durationMs)
        {
            if (!_settings.GamepadVibration || _activeXInputUserIndex < 0 || !_xInputLoaded || _xInputSetState == null)
                return;

            try
            {
                var vib = new XINPUT_VIBRATION
                {
                    wLeftMotorSpeed = leftMotor,
                    wRightMotorSpeed = rightMotor
                };
                _xInputSetState(_activeXInputUserIndex, ref vib);

                Task.Delay(durationMs).ContinueWith(_ =>
                {
                    if (!_isAlarmVibrating)
                    {
                        var stopVib = new XINPUT_VIBRATION { wLeftMotorSpeed = 0, wRightMotorSpeed = 0 };
                        if (_activeXInputUserIndex >= 0 && _xInputSetState != null)
                        {
                            _xInputSetState(_activeXInputUserIndex, ref stopVib);
                        }
                    }
                });
            }
            catch { }
        }

        public void StartAlarmVibration()
        {
            IsAlarmActive = true;
            if (!_settings.GamepadVibration || _activeXInputUserIndex < 0 || !_xInputLoaded || _xInputSetState == null)
                return;

            _isAlarmVibrating = true;
            _alarmRumblePhase = false;
            _alarmRumbleTimer.Start();
        }

        public void StopAlarmVibration()
        {
            IsAlarmActive = false;
            _isAlarmVibrating = false;
            _alarmRumbleTimer.Stop();

            if (_activeXInputUserIndex >= 0 && _xInputLoaded && _xInputSetState != null)
            {
                try
                {
                    var stopVib = new XINPUT_VIBRATION { wLeftMotorSpeed = 0, wRightMotorSpeed = 0 };
                    _xInputSetState(_activeXInputUserIndex, ref stopVib);
                }
                catch { }
            }
        }

        private void OnAlarmRumbleTick(object sender, EventArgs e)
        {
            if (!_isAlarmVibrating || _activeXInputUserIndex < 0 || _xInputSetState == null)
                return;

            _alarmRumblePhase = !_alarmRumblePhase;
            var vib = new XINPUT_VIBRATION
            {
                wLeftMotorSpeed = _alarmRumblePhase ? (ushort)50000 : (ushort)0,
                wRightMotorSpeed = _alarmRumblePhase ? (ushort)50000 : (ushort)0
            };
            try
            {
                _xInputSetState(_activeXInputUserIndex, ref vib);
            }
            catch { }
        }
        #endregion

        public void Dispose()
        {
            _pollTimer?.Stop();
            _alarmRumbleTimer?.Stop();
            StopAlarmVibration();

            if (_xInputModule != IntPtr.Zero)
            {
                try
                {
                    Win32Api.FreeLibrary(_xInputModule);
                }
                catch { }
                _xInputModule = IntPtr.Zero;
                _xInputGetState = null;
                _xInputSetState = null;
                _xInputLoaded = false;
            }
        }
    }
}
