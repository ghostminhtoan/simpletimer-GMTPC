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
            RefreshPresetButtonsUI();
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

        public void RefreshPresetButtonsUI()
        {
            if (_settings?.IntervalPresets == null || _settings.IntervalPresets.Count == 0) return;

            Button[] buttons = { BtnPreset1, BtnPreset2, BtnPreset3, BtnPreset4, BtnPreset5 };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                int id = i + 1;
                var p = _settings.IntervalPresets.Find(x => x.Id == id);
                if (p != null)
                {
                    buttons[i].Content = p.Name;
                    buttons[i].ToolTip = $"{p.Name}: Work {p.Work}s / Rest {p.Rest}s × {(p.IsInfinite ? "∞" : p.Loops.ToString())} Loops\n(Chuột trái: Chọn • Chuột phải: Sửa / Đổi tên)";
                }
            }
        }

        private void BtnPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int presetId))
            {
                ApplyPreset(presetId);
            }
        }

        private void BtnPreset_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int presetId))
            {
                e.Handled = true;
                ShowPresetContextMenu(btn, presetId);
            }
        }

        public void ApplyPreset(int presetId)
        {
            var p = _settings?.IntervalPresets?.Find(x => x.Id == presetId);
            if (p == null)
            {
                var defs = SettingsManager.GetDefaultPresets();
                int idx = presetId - 1;
                if (idx >= 0 && idx < defs.Count) p = defs[idx];
            }
            if (p == null) return;

            if (TxtPrepare != null) TxtPrepare.Text = p.Prepare.ToString();
            if (TxtWork != null) TxtWork.Text = p.Work.ToString();
            if (TxtRest != null) TxtRest.Text = p.Rest.ToString();
            if (TxtEnd != null) TxtEnd.Text = p.End.ToString();

            if (p.IsInfinite)
            {
                if (ChkInfinite != null) ChkInfinite.IsChecked = true;
                if (TxtLoops != null)
                {
                    TxtLoops.Text = "∞";
                    TxtLoops.IsEnabled = false;
                }
            }
            else
            {
                if (ChkInfinite != null) ChkInfinite.IsChecked = false;
                if (TxtLoops != null)
                {
                    TxtLoops.Text = p.Loops.ToString();
                    TxtLoops.IsEnabled = true;
                }
            }

            ReadInputsToManager();
            HighlightPresetButton(presetId);

            if (Manager != null && Manager.CurrentPhase == IntervalPhase.Idle)
            {
                Manager.UpdateDisplay();
            }
        }

        private void ShowPresetContextMenu(Button btn, int presetId)
        {
            var p = _settings?.IntervalPresets?.Find(x => x.Id == presetId);
            string presetName = p != null ? p.Name : $"Preset {presetId}";

            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x22)),
                BorderBrush = _greenBorder,
                BorderThickness = new Thickness(1),
                PlacementTarget = btn,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
            };

            var titleItem = new MenuItem
            {
                Header = $"⚡ Preset #{presetId}: {presetName}",
                IsEnabled = false,
                Foreground = _greenBrush,
                FontWeight = FontWeights.Bold
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new Separator());

            var itemApply = new MenuItem { Header = "▶ Áp dụng Preset này", Foreground = _greenBrush };
            itemApply.Click += (s, ev) => ApplyPreset(presetId);
            menu.Items.Add(itemApply);

            var itemEdit = new MenuItem { Header = "⚙️ Chỉnh sửa thông số & Đổi tên...", Foreground = _yellowBrush, FontWeight = FontWeights.SemiBold };
            itemEdit.Click += (s, ev) => OpenEditPresetDialog(presetId);
            menu.Items.Add(itemEdit);

            var itemSaveCurrent = new MenuItem { Header = "💾 Lưu thời gian đang nhập vào Preset này", Foreground = _greenBrush };
            itemSaveCurrent.Click += (s, ev) => SaveCurrentInputsToPreset(presetId);
            menu.Items.Add(itemSaveCurrent);

            menu.Items.Add(new Separator());

            var itemResetSingle = new MenuItem { Header = $"🔄 Khôi phục mặc định Preset #{presetId}", Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xDD, 0xAA)) };
            itemResetSingle.Click += (s, ev) =>
            {
                _settings?.ResetIntervalPreset(presetId);
                RefreshPresetButtonsUI();
                ApplyPreset(presetId);
            };
            menu.Items.Add(itemResetSingle);

            var itemResetAll = new MenuItem { Header = "🔄 Khôi phục tất cả 5 Presets về mặc định", Foreground = _redBrush };
            itemResetAll.Click += (s, ev) =>
            {
                if (MessageBox.Show("Bạn có chắc chắn muốn khôi phục toàn bộ 5 Presets về thông số mặc định ban đầu?", "Xác nhận khôi phục", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _settings?.ResetAllIntervalPresets();
                    RefreshPresetButtonsUI();
                    ApplyPreset(presetId);
                }
            };
            menu.Items.Add(itemResetAll);

            menu.IsOpen = true;
        }

        public void OpenEditPresetDialog(int presetId)
        {
            var p = _settings?.IntervalPresets?.Find(x => x.Id == presetId);
            if (p == null)
            {
                var defs = SettingsManager.GetDefaultPresets();
                int idx = presetId - 1;
                if (idx >= 0 && idx < defs.Count) p = defs[idx];
            }
            if (p == null) return;

            var dlg = new IntervalPresetEditWindow(p)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dlg.ShowDialog() == true && dlg.Preset != null)
            {
                if (_settings != null)
                {
                    int index = _settings.IntervalPresets.FindIndex(x => x.Id == presetId);
                    if (index >= 0)
                    {
                        _settings.IntervalPresets[index] = dlg.Preset;
                    }
                    else
                    {
                        _settings.IntervalPresets.Add(dlg.Preset);
                    }
                    _settings.Save();
                }

                RefreshPresetButtonsUI();
                ApplyPreset(presetId);
            }
        }

        public void SaveCurrentInputsToPreset(int presetId)
        {
            if (TxtPrepare == null || TxtWork == null || TxtRest == null || TxtEnd == null || TxtLoops == null) return;

            if (!int.TryParse(TxtWork.Text, out int work) || work < 1)
            {
                MessageBox.Show("Thời gian Work phải tối thiểu 1 giây.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int.TryParse(TxtPrepare.Text, out int prep);
            int.TryParse(TxtRest.Text, out int rest);
            int.TryParse(TxtEnd.Text, out int end);
            bool isInf = ChkInfinite?.IsChecked == true;
            int loops = 5;
            if (!isInf)
            {
                int.TryParse(TxtLoops.Text, out loops);
                if (loops < 1) loops = 1;
            }

            var p = _settings?.IntervalPresets?.Find(x => x.Id == presetId);
            string name = p != null ? p.Name : $"Preset {presetId}";

            var updatedPreset = new IntervalPreset
            {
                Id = presetId,
                Name = name,
                Prepare = prep >= 0 ? prep : 0,
                Work = work,
                Rest = rest >= 0 ? rest : 0,
                End = end >= 0 ? end : 0,
                Loops = loops,
                IsInfinite = isInf
            };

            if (_settings != null)
            {
                int index = _settings.IntervalPresets.FindIndex(x => x.Id == presetId);
                if (index >= 0)
                {
                    _settings.IntervalPresets[index] = updatedPreset;
                }
                else
                {
                    _settings.IntervalPresets.Add(updatedPreset);
                }
                _settings.Save();
            }

            RefreshPresetButtonsUI();
            HighlightPresetButton(presetId);
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
            if (_settings?.IntervalPresets == null) return;

            if (int.TryParse(TxtPrepare.Text, out int prep) &&
                int.TryParse(TxtWork.Text, out int work) &&
                int.TryParse(TxtRest.Text, out int rest) &&
                int.TryParse(TxtEnd.Text, out int end))
            {
                bool isInf = ChkInfinite?.IsChecked == true;
                int loops = 0;
                if (!isInf) int.TryParse(TxtLoops.Text, out loops);

                foreach (var p in _settings.IntervalPresets)
                {
                    if (p.Prepare == prep && p.Work == work && p.Rest == rest && p.End == end)
                    {
                        if (p.IsInfinite && isInf)
                        {
                            HighlightPresetButton(p.Id);
                            return;
                        }
                        if (!p.IsInfinite && !isInf && p.Loops == loops)
                        {
                            HighlightPresetButton(p.Id);
                            return;
                        }
                    }
                }
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

            if (_settings?.IntervalPresets != null && _settings.IntervalPresets.Count > 0)
            {
                foreach (var pr in _settings.IntervalPresets)
                {
                    int localId = pr.Id;
                    var pItem = new MenuItem 
                    { 
                        Header = $"{pr.Id}. {pr.Name} ({pr.GetSummary()})", 
                        Foreground = _greenBrush 
                    };
                    pItem.Click += (s, ev) => ApplyPreset(localId);
                    presetsMenu.Items.Add(pItem);
                }
            }
            else
            {
                for (int i = 1; i <= 5; i++)
                {
                    int localId = i;
                    var pItem = new MenuItem { Header = $"Preset #{i}", Foreground = _greenBrush };
                    pItem.Click += (s, ev) => ApplyPreset(localId);
                    presetsMenu.Items.Add(pItem);
                }
            }

            presetsMenu.Items.Add(new Separator());
            var itemEditPresets = new MenuItem
            {
                Header = "⚙️ Quản lý & Tùy chỉnh Preset...",
                Foreground = _yellowBrush,
                FontWeight = FontWeights.SemiBold
            };
            itemEditPresets.Click += (s, ev) => OpenEditPresetDialog(1);
            presetsMenu.Items.Add(itemEditPresets);

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
