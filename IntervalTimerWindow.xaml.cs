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
            if (Manager.IsInfiniteLoops)
            {
                ChkInfinite.IsChecked = true;
                TxtLoops.Text = "∞";
                TxtLoops.IsEnabled = false;
            }
            else
            {
                ChkInfinite.IsChecked = false;
                TxtLoops.Text = Manager.TotalLoops.ToString();
                TxtLoops.IsEnabled = true;
            }

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
            if (IsClickThrough)
            {
                Win32Api.SetClickThrough(handle, true);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ClampToScreen();
            Manager.UpdateDisplay();
            CheckMatchingPreset();
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
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<TextBox>(dep) != null ||
                    FindVisualParent<Button>(dep) != null ||
                    FindVisualParent<CheckBox>(dep) != null)
                {
                    return;
                }
            }

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

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
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
            if (Manager == null) return;
            if (TxtPrepare != null && int.TryParse(TxtPrepare.Text, out int prep)) Manager.PrepareSeconds = Math.Max(0, prep);
            if (TxtWork != null && int.TryParse(TxtWork.Text, out int work)) Manager.WorkSeconds = Math.Max(1, work);
            if (TxtRest != null && int.TryParse(TxtRest.Text, out int rest)) Manager.RestSeconds = Math.Max(0, rest);
            if (TxtEnd != null && int.TryParse(TxtEnd.Text, out int endVal)) Manager.EndSeconds = Math.Max(0, endVal);

            if (ChkInfinite != null && ChkInfinite.IsChecked == true)
            {
                Manager.IsInfiniteLoops = true;
            }
            else if (TxtLoops != null)
            {
                string loopText = (TxtLoops.Text ?? "").Trim().ToLowerInvariant();
                if (loopText == "0" || loopText == "inf" || loopText == "infinite" || loopText == "∞")
                {
                    Manager.IsInfiniteLoops = true;
                }
                else if (int.TryParse(TxtLoops.Text, out int loops))
                {
                    Manager.IsInfiniteLoops = false;
                    Manager.TotalLoops = Math.Max(1, loops);
                }
            }
        }

        private void Input_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
            }
        }

        private void Input_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                tb.Focus();
                tb.SelectAll();
            }
        }

        private void Input_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (tb == TxtLoops && ChkInfinite != null && ChkInfinite.IsChecked == true) return;

                if (int.TryParse(tb.Text, out int val))
                {
                    int delta = e.Delta > 0 ? 1 : -1;
                    int min = (tb == TxtWork || tb == TxtLoops) ? 1 : 0;
                    int newVal = Math.Max(min, val + delta);
                    tb.Text = newVal.ToString();
                    tb.SelectAll();
                    ReadInputsToManager();
                    if (Manager != null && Manager.CurrentPhase == IntervalPhase.Idle)
                    {
                        Manager.UpdateDisplay();
                    }
                    e.Handled = true;
                }
            }
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Manager == null) return;

            if (sender == TxtLoops && ChkInfinite != null)
            {
                string t = TxtLoops.Text.Trim().ToLowerInvariant();
                if (t == "0" || t == "inf" || t == "infinite" || t == "∞")
                {
                    if (ChkInfinite.IsChecked != true)
                    {
                        ChkInfinite.IsChecked = true;
                        return;
                    }
                }
                else if (int.TryParse(t, out int lp) && lp > 0)
                {
                    if (ChkInfinite.IsChecked == true)
                    {
                        ChkInfinite.IsChecked = false;
                        return;
                    }
                }
            }

            ReadInputsToManager();
            CheckMatchingPreset();
            if (Manager != null && Manager.CurrentPhase == IntervalPhase.Idle)
            {
                Manager.UpdateDisplay();
            }
        }

        private void BtnPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int presetId))
            {
                ApplyPreset(presetId);
            }
        }

        public void ApplyPreset(int presetId)
        {
            int prep = 5, work = 30, rest = 10, end = 5, loops = 5;
            bool infinite = false;

            switch (presetId)
            {
                case 1: // Tabata (Work 20s / Rest 10s × 8 Loops)
                    prep = 5; work = 20; rest = 10; end = 5; loops = 8;
                    break;
                case 2: // HIIT 30/15 (Work 30s / Rest 15s × 5 Loops)
                    prep = 5; work = 30; rest = 15; end = 5; loops = 5;
                    break;
                case 3: // Endurance 45/15 (Work 45s / Rest 15s × 5 Loops)
                    prep = 5; work = 45; rest = 15; end = 5; loops = 5;
                    break;
                case 4: // Boxing 3m/1m (Work 180s / Rest 60s × 5 Loops)
                    prep = 5; work = 180; rest = 60; end = 5; loops = 5;
                    break;
                case 5: // Pomodoro 25m/5m (Work 1500s / Rest 300s × 4 Loops)
                    prep = 5; work = 1500; rest = 300; end = 5; loops = 4;
                    break;
            }

            TxtPrepare.Text = prep.ToString();
            TxtWork.Text = work.ToString();
            TxtRest.Text = rest.ToString();
            TxtEnd.Text = end.ToString();
            TxtLoops.Text = loops.ToString();
            if (ChkInfinite != null) ChkInfinite.IsChecked = infinite;

            ReadInputsToManager();
            HighlightPresetButton(presetId);

            if (Manager != null && Manager.CurrentPhase == IntervalPhase.Idle)
            {
                Manager.UpdateDisplay();
            }
        }

        private void HighlightPresetButton(int presetId)
        {
            var activeBg = new SolidColorBrush(Color.FromArgb(0x55, 0x23, 0xFF, 0x23));
            var normalBg = new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x1E, 0x28));
            var activeBorder = new SolidColorBrush(Color.FromRgb(0x23, 0xFF, 0x23));
            var normalBorder = new SolidColorBrush(Color.FromArgb(0x44, 0x23, 0xFF, 0x23));
            var activeFg = new SolidColorBrush(Color.FromRgb(0x23, 0xFF, 0x23));
            var normalFg = new SolidColorBrush(Color.FromRgb(0xA0, 0xFF, 0xA0));

            Button[] buttons = { BtnPreset1, BtnPreset2, BtnPreset3, BtnPreset4, BtnPreset5 };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                bool isActive = (i + 1) == presetId;
                buttons[i].Background = isActive ? activeBg : normalBg;
                buttons[i].BorderBrush = isActive ? activeBorder : normalBorder;
                buttons[i].Foreground = isActive ? activeFg : normalFg;
                buttons[i].FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold;
            }
        }

        private void CheckMatchingPreset()
        {
            if (TxtPrepare == null || TxtWork == null || TxtRest == null || TxtEnd == null || TxtLoops == null) return;

            if (int.TryParse(TxtPrepare.Text, out int prep) &&
                int.TryParse(TxtWork.Text, out int work) &&
                int.TryParse(TxtRest.Text, out int rest) &&
                int.TryParse(TxtEnd.Text, out int end) &&
                int.TryParse(TxtLoops.Text, out int loops) &&
                ChkInfinite?.IsChecked != true)
            {
                if (prep == 5 && work == 20 && rest == 10 && end == 5 && loops == 8) { HighlightPresetButton(1); return; }
                if (prep == 5 && work == 30 && rest == 15 && end == 5 && loops == 5) { HighlightPresetButton(2); return; }
                if (prep == 5 && work == 45 && rest == 15 && end == 5 && loops == 5) { HighlightPresetButton(3); return; }
                if (prep == 5 && work == 180 && rest == 60 && end == 5 && loops == 5) { HighlightPresetButton(4); return; }
                if (prep == 5 && work == 1500 && rest == 300 && end == 5 && loops == 4) { HighlightPresetButton(5); return; }
            }

            HighlightPresetButton(0);
        }

        private void ChkInfinite_Checked(object sender, RoutedEventArgs e)
        {
            if (Manager != null) Manager.IsInfiniteLoops = true;
            TxtLoops.Text = "∞";
            TxtLoops.IsEnabled = false;
            ReadInputsToManager();
            if (Manager != null && Manager.CurrentPhase == IntervalPhase.Idle)
            {
                Manager.UpdateDisplay();
            }
        }

        private void ChkInfinite_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Manager != null) Manager.IsInfiniteLoops = false;
            TxtLoops.IsEnabled = true;
            TxtLoops.Text = (Manager != null && Manager.TotalLoops > 0) ? Manager.TotalLoops.ToString() : "3";
            ReadInputsToManager();
            if (Manager != null && Manager.CurrentPhase == IntervalPhase.Idle)
            {
                Manager.UpdateDisplay();
            }
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
                Header = "Interval Timer (Ctrl + Win + Esc)",
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

            var presetsMenu = new MenuItem
            {
                Header = "⚡ Presets (5 Chế độ chọn nhanh)",
                Foreground = _greenBrush
            };

            var p1 = new MenuItem { Header = "1. Tabata (Work 20s / Rest 10s × 8)", Foreground = _greenBrush };
            p1.Click += (s, ev) => ApplyPreset(1);
            presetsMenu.Items.Add(p1);

            var p2 = new MenuItem { Header = "2. HIIT 30/15 (Work 30s / Rest 15s × 5)", Foreground = _greenBrush };
            p2.Click += (s, ev) => ApplyPreset(2);
            presetsMenu.Items.Add(p2);

            var p3 = new MenuItem { Header = "3. Endurance (Work 45s / Rest 15s × 5)", Foreground = _greenBrush };
            p3.Click += (s, ev) => ApplyPreset(3);
            presetsMenu.Items.Add(p3);

            var p4 = new MenuItem { Header = "4. Boxing (Work 3m / Rest 1m × 5)", Foreground = _greenBrush };
            p4.Click += (s, ev) => ApplyPreset(4);
            presetsMenu.Items.Add(p4);

            var p5 = new MenuItem { Header = "5. Pomodoro (Focus 25m / Rest 5m × 4)", Foreground = _greenBrush };
            p5.Click += (s, ev) => ApplyPreset(5);
            presetsMenu.Items.Add(p5);

            menu.Items.Add(presetsMenu);

            menu.Items.Add(new Separator());

            bool soundOn = _settings?.SoundEnabled ?? true;
            var itemSound = new MenuItem
            {
                Header = soundOn ? "🔊 Sound: ON (Bật âm thanh)" : "🔈 Sound: OFF (Tắt âm thanh)",
                Foreground = _greenBrush
            };
            itemSound.Click += (s, ev) =>
            {
                bool newState = !(_settings?.SoundEnabled ?? true);
                if (_settings != null)
                {
                    _settings.SoundEnabled = newState;
                    _settings.Save();
                }
                if (Application.Current is App app)
                {
                    app.SyncSoundSetting(newState);
                }
            };
            menu.Items.Add(itemSound);

            var itemClose = new MenuItem { Header = "Hide Window", Foreground = _redBrush };
            itemClose.Click += (s, ev) => Hide();
            menu.Items.Add(itemClose);

            menu.IsOpen = true;
        }

        public void ClampToScreen()
        {
            try
            {
                double currentLeft = double.IsNaN(Left) || double.IsInfinity(Left) ? 250 : Left;
                double currentTop = double.IsNaN(Top) || double.IsInfinity(Top) ? 250 : Top;

                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)currentLeft, (int)currentTop));
                var area = screen.WorkingArea;

                double newLeft = currentLeft;
                double newTop = currentTop;

                double width = ActualWidth > 0 ? ActualWidth : Width;
                double height = ActualHeight > 0 ? ActualHeight : Height;

                if (newLeft < area.Left) newLeft = area.Left;
                if (newLeft + width > area.Right) newLeft = area.Right - width;
                if (newTop < area.Top) newTop = area.Top;
                if (newTop + height > area.Bottom) newTop = area.Bottom - height;

                Left = newLeft;
                Top = newTop;

                if (_settings != null)
                {
                    _settings.IntervalWindowX = (int)Left;
                    _settings.IntervalWindowY = (int)Top;
                    _settings.Save();
                }
            }
            catch { }
        }
    }
}
