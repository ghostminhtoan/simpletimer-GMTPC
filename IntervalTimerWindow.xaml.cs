using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TimeBomb.Core;

namespace TimeBomb
{
    public partial class IntervalTimerWindow : Window
    {
        private readonly SettingsManager _settings;
        private bool _isDragging = false;

        private readonly SolidColorBrush _greenBrush = new SolidColorBrush(Color.FromRgb(0x23, 0xFF, 0x23));
        private readonly SolidColorBrush _yellowBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00));
        private readonly SolidColorBrush _orangeBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x00));
        private readonly SolidColorBrush _redBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x23, 0x23));

        private readonly SolidColorBrush _greenBorder = new SolidColorBrush(Color.FromArgb(0xAA, 0x23, 0xFF, 0x23));
        private readonly SolidColorBrush _yellowBorder = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xCC, 0x00));
        private readonly SolidColorBrush _orangeBorder = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0x99, 0x00));
        private readonly SolidColorBrush _redBorder = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0x23, 0x23));

        private DropShadowEffect _normalGlow;
        private DropShadowEffect _yellowGlow;
        private DropShadowEffect _orangeGlow;
        private DropShadowEffect _redGlow;

        public IntervalTimerManager Manager { get; set; }
        public bool IsClickThrough { get; private set; } = false;

        public IntervalTimerWindow(SettingsManager settings, SoundManager sound)
        {
            InitializeComponent();
            _settings = settings;
            Manager = new IntervalTimerManager(sound, settings);

            _normalGlow = (DropShadowEffect)Resources["NormalGlow"];
            _yellowGlow = (DropShadowEffect)Resources["YellowGlow"];
            _orangeGlow = (DropShadowEffect)Resources["OrangeGlow"];
            _redGlow = (DropShadowEffect)Resources["RedGlow"];

            Left = _settings.IntervalWindowX;
            Top = _settings.IntervalWindowY;

            // Load settings into inputs
            TxtPrepare.Text = Manager.PrepareSeconds.ToString();
            TxtWork.Text = Manager.WorkSeconds.ToString();
            TxtRest.Text = Manager.RestSeconds.ToString();
            TxtEnd.Text = Manager.EndSeconds.ToString();
            TxtLoops.Text = Manager.TotalLoops.ToString();

            Manager.OnDisplayChanged += (main, title, loopInfo, subText, isRed, isBlinkHidden) =>
            {
                Dispatcher.Invoke(() => UpdateDisplayUI(main, title, loopInfo, subText, isRed, isBlinkHidden));
            };

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            MouseRightButtonUp += OnMouseRightButtonUp;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            Win32Api.SetToolWindowAndNoActivate(handle);
            if (IsClickThrough)
            {
                Win32Api.SetClickThrough(handle, true);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ClampToScreen();
            Manager.UpdateDisplay();
        }

        public void SetClickThrough(bool enable)
        {
            IsClickThrough = enable;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                Win32Api.SetClickThrough(handle, enable);
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _isDragging = false;
                Manager?.PauseToggle();
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _isDragging = true;
                DragMove();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Released)
            {
                _isDragging = false;
                ClampToScreen();
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ClampToScreen();
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowContextMenu();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Manager?.Stop();
            Hide();
        }

        private void ReadInputsToManager()
        {
            if (int.TryParse(TxtPrepare.Text, out int prep)) Manager.PrepareSeconds = Math.Max(0, prep);
            if (int.TryParse(TxtWork.Text, out int work)) Manager.WorkSeconds = Math.Max(1, work);
            if (int.TryParse(TxtRest.Text, out int rest)) Manager.RestSeconds = Math.Max(0, rest);
            if (int.TryParse(TxtEnd.Text, out int endVal)) Manager.EndSeconds = Math.Max(0, endVal);
            if (int.TryParse(TxtLoops.Text, out int loops)) Manager.TotalLoops = Math.Max(1, loops);
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            ReadInputsToManager();
            Manager.Start();
            BtnStart.Content = "⏸ PAUSE";
        }

        private void BtnEnd_Click(object sender, RoutedEventArgs e)
        {
            Manager.Stop();
            BtnStart.Content = "▶ START";
        }

        private void UpdateDisplayUI(string mainText, string phaseTitle, string loopInfo, string subText, bool isRed, bool isBlinkHidden)
        {
            TxtMainTime.Text = mainText;
            TxtPhaseTitle.Text = phaseTitle;
            TxtLoopInfo.Text = loopInfo;
            TxtSubText.Text = subText;

            // Apply phase-specific color schemes
            switch (Manager.CurrentPhase)
            {
                case IntervalPhase.Prepare:
                    TxtMainTime.Foreground = _yellowBrush;
                    TxtMainTime.Effect = _yellowGlow;
                    BadgePhase.BorderBrush = _yellowBrush;
                    BadgePhase.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xCC, 0x00));
                    TxtPhaseTitle.Foreground = _yellowBrush;
                    RootBorder.BorderBrush = _yellowBorder;
                    break;
                case IntervalPhase.Work:
                    TxtMainTime.Foreground = _greenBrush;
                    TxtMainTime.Effect = _normalGlow;
                    BadgePhase.BorderBrush = _greenBrush;
                    BadgePhase.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x23, 0xFF, 0x23));
                    TxtPhaseTitle.Foreground = _greenBrush;
                    RootBorder.BorderBrush = _greenBorder;
                    break;
                case IntervalPhase.Rest:
                    TxtMainTime.Foreground = _orangeBrush;
                    TxtMainTime.Effect = _orangeGlow;
                    BadgePhase.BorderBrush = _orangeBrush;
                    BadgePhase.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x99, 0x00));
                    TxtPhaseTitle.Foreground = _orangeBrush;
                    RootBorder.BorderBrush = _orangeBorder;
                    break;
                case IntervalPhase.End:
                    TxtMainTime.Foreground = _redBrush;
                    TxtMainTime.Effect = _redGlow;
                    BadgePhase.BorderBrush = _redBrush;
                    BadgePhase.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x23, 0x23));
                    TxtPhaseTitle.Foreground = _redBrush;
                    RootBorder.BorderBrush = _redBorder;
                    break;
                default:
                    TxtMainTime.Foreground = _greenBrush;
                    TxtMainTime.Effect = _normalGlow;
                    BadgePhase.BorderBrush = _greenBrush;
                    BadgePhase.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x23, 0xFF, 0x23));
                    TxtPhaseTitle.Foreground = _greenBrush;
                    RootBorder.BorderBrush = _greenBorder;
                    break;
            }

            TxtMainTime.Opacity = isBlinkHidden ? 0.0 : 1.0;

            if (Manager.IsRunning && !Manager.IsPaused)
            {
                BtnStart.Content = "⏸ PAUSE";
            }
            else if (Manager.IsPaused)
            {
                BtnStart.Content = "▶ RESUME";
            }
            else
            {
                BtnStart.Content = "▶ START";
            }
        }

        private void ShowContextMenu()
        {
            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)),
                BorderBrush = _greenBrush,
                BorderThickness = new Thickness(1),
                Foreground = _greenBrush
            };

            var titleItem = new MenuItem
            {
                Header = "Interval Timer (Ctrl + Win + `)",
                IsEnabled = false,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xFF, 0xA0))
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new Separator());

            var itemStart = new MenuItem { Header = Manager.IsRunning ? "Pause" : "Start", Foreground = _greenBrush };
            itemStart.Click += (s, ev) =>
            {
                if (Manager.IsRunning) Manager.PauseToggle();
                else { ReadInputsToManager(); Manager.Start(); }
            };
            menu.Items.Add(itemStart);

            var itemStop = new MenuItem { Header = "Stop / Reset", Foreground = _greenBrush };
            itemStop.Click += (s, ev) => Manager.Stop();
            menu.Items.Add(itemStop);

            menu.Items.Add(new Separator());

            var itemClose = new MenuItem { Header = "Hide Window", Foreground = _redBrush };
            itemClose.Click += (s, ev) => Hide();
            menu.Items.Add(itemClose);

            menu.IsOpen = true;
        }

        public void ClampToScreen()
        {
            try
            {
                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
                var area = screen.WorkingArea;

                double newLeft = Left;
                double newTop = Top;

                if (newLeft < area.Left) newLeft = area.Left;
                if (newLeft + ActualWidth > area.Right) newLeft = area.Right - ActualWidth;
                if (newTop < area.Top) newTop = area.Top;
                if (newTop + ActualHeight > area.Bottom) newTop = area.Bottom - ActualHeight;

                Left = newLeft;
                Top = newTop;

                _settings.IntervalWindowX = (int)Left;
                _settings.IntervalWindowY = (int)Top;
                _settings.Save();
            }
            catch { }
        }
    }
}
