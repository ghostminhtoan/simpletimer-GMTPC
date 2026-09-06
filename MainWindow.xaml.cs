using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TimeBomb.Core;

namespace TimeBomb
{
    public partial class MainWindow : Window
    {
        private readonly SettingsManager _settings;
        private readonly SolidColorBrush _greenBrush = new SolidColorBrush(Color.FromRgb(0x23, 0xFF, 0x23));
        private readonly SolidColorBrush _redBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x23, 0x23));
        private readonly SolidColorBrush _activeBorder = new SolidColorBrush(Color.FromArgb(0xAA, 0x23, 0xFF, 0x23));
        private readonly SolidColorBrush _inactiveBorder = new SolidColorBrush(Color.FromArgb(0x33, 0x23, 0xFF, 0x23));
        private readonly SolidColorBrush _redBorder = new SolidColorBrush(Color.FromArgb(0x77, 0xFF, 0x23, 0x23));
        private readonly SolidColorBrush _activeBadgeBg = new SolidColorBrush(Color.FromArgb(0x55, 0x23, 0xFF, 0x23));
        private readonly SolidColorBrush _inactiveBadgeBg = new SolidColorBrush(Color.FromArgb(0x22, 0x66, 0x66, 0x66));
        private readonly SolidColorBrush _activeBadgeFg = new SolidColorBrush(Color.FromRgb(0x23, 0xFF, 0x23));
        private readonly SolidColorBrush _inactiveBadgeFg = new SolidColorBrush(Color.FromRgb(0x88, 0xAA, 0x88));

        private DropShadowEffect _normalGlow;
        private DropShadowEffect _redGlow;
        private bool _isDragging = false;
        private bool _isCurrentlyRed = false;

        public int InstanceId { get; set; } = 1;
        public bool IsActiveInstance { get; private set; } = false;
        public bool IsClickThrough { get; private set; } = false;
        public TimeBombManager Manager { get; set; }

        public event Action<MainWindow> OnActivatedByInteraction;
        public event Action OnRequestNewInstance;
        public event Action<MainWindow> OnRequestCloseInstance;
        public event Action OnRequestExitAll;
        public event Action<MainWindow, bool> OnClickThroughChanged;
        public event Action<MainWindow, double> OnOpacityChanged;

        public MainWindow(SettingsManager settings, int instanceId = 1)
        {
            InitializeComponent();
            _settings = settings;
            InstanceId = instanceId;

            _normalGlow = (DropShadowEffect)Resources["NormalGlow"];
            _redGlow = (DropShadowEffect)Resources["RedGlow"];

            // Load saved position
            Left = _settings.WindowX;
            Top = _settings.WindowY;

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            MouseEnter += OnMouseEnter;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            MouseWheel += OnMouseWheel;
            MouseRightButtonUp += OnMouseRightButtonUp;
            MouseUp += OnMouseUp;
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
            SetWindowOpacity(_settings.Opacity);
            SetClickThrough(_settings.ClickThrough);
            ClampToScreen();
        }

        public void SetClickThrough(bool enable)
        {
            IsClickThrough = enable;
            _settings.ClickThrough = enable;
            _settings.Save();

            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                Win32Api.SetClickThrough(handle, enable);
            }

            if (GhostBadge != null)
            {
                GhostBadge.Visibility = enable ? Visibility.Visible : Visibility.Collapsed;
            }

            OnClickThroughChanged?.Invoke(this, enable);
        }

        public void SetWindowOpacity(double val)
        {
            val = Math.Max(0.2, Math.Min(1.0, val));
            Opacity = val;
            _settings.Opacity = val;
            _settings.Save();
            OnOpacityChanged?.Invoke(this, val);
        }

        public void SetBadge(int id, bool visible)
        {
            InstanceId = id;
            TxtBadge.Text = "#" + id;
            BadgeBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetActive(bool isActive)
        {
            IsActiveInstance = isActive;

            if (!_isCurrentlyRed)
            {
                RootBorder.BorderBrush = isActive ? _activeBorder : _inactiveBorder;
            }

            if (BadgeBorder.Visibility == Visibility.Visible)
            {
                BadgeBorder.Background = isActive ? _activeBadgeBg : _inactiveBadgeBg;
                TxtBadge.Foreground = isActive ? _activeBadgeFg : _inactiveBadgeFg;
            }
        }

        public bool IsPointInside(int screenX, int screenY)
        {
            return screenX >= Left && screenX <= Left + ActualWidth &&
                   screenY >= Top && screenY <= Top + ActualHeight;
        }

        public void UpdateDisplay(string mainText, string prefixText, string subText, bool isRed, bool isBlinkHidden)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateDisplay(mainText, prefixText, subText, isRed, isBlinkHidden));
                return;
            }

            _isCurrentlyRed = isRed;
            TxtMainTime.Text = mainText;
            TxtPrefix.Text = prefixText;
            TxtSubTime.Text = subText;

            if (isRed)
            {
                TxtMainTime.Foreground = _redBrush;
                TxtMainTime.Effect = _redGlow;
                RootBorder.BorderBrush = _redBorder;
            }
            else
            {
                TxtMainTime.Foreground = _greenBrush;
                TxtMainTime.Effect = _normalGlow;
                RootBorder.BorderBrush = IsActiveInstance ? _activeBorder : _inactiveBorder;
            }

            TxtMainTime.Opacity = isBlinkHidden ? 0.0 : 1.0;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            OnActivatedByInteraction?.Invoke(this);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OnActivatedByInteraction?.Invoke(this);

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

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            OnActivatedByInteraction?.Invoke(this);
            if (e.Delta > 0)
            {
                Manager?.AdjustMinutes(1);
            }
            else if (e.Delta < 0)
            {
                Manager?.AdjustMinutes(-1);
            }
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            OnActivatedByInteraction?.Invoke(this);
            ShowContextMenu();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                OnActivatedByInteraction?.Invoke(this);
                if (Manager != null && Manager.Mode == AppMode.Timer)
                {
                    OpenTimerInputDialog();
                }
            }
        }

        public void OpenTimerInputDialog()
        {
            if (Manager == null || Manager.Mode != AppMode.Timer) return;

            int currentMins = Manager.TimerMinutes;
            int currentSecs = Manager.TimerSeconds;

            var inputWindow = new TimerInputWindow(currentMins, currentSecs);

            double left = Left + (ActualWidth - inputWindow.Width) / 2;
            double top = Top + (ActualHeight - inputWindow.Height) / 2;

            try
            {
                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
                var area = screen.WorkingArea;
                if (left < area.Left) left = area.Left;
                if (left + inputWindow.Width > area.Right) left = area.Right - inputWindow.Width;
                if (top < area.Top) top = area.Top;
                if (top + inputWindow.Height > area.Bottom) top = area.Bottom - inputWindow.Height;
            }
            catch { }

            inputWindow.Left = left;
            inputWindow.Top = top;

            inputWindow.OnTimeConfirmed += (mins, secs) =>
            {
                Manager?.SetTimer(mins, secs);
            };

            inputWindow.Show();
        }

        private void ShowContextMenu()
        {
            var menu = new System.Windows.Controls.ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x23, 0xFF, 0x23)),
                BorderThickness = new Thickness(1),
                Foreground = _greenBrush
            };

            var titleItem = new System.Windows.Controls.MenuItem
            {
                Header = $"TimeBomb #{InstanceId} ({Manager?.Mode})",
                IsEnabled = false,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xFF, 0xA0))
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new System.Windows.Controls.Separator());

            var itemNew = new System.Windows.Controls.MenuItem { Header = "New Timer (Win + N)", Foreground = _greenBrush };
            itemNew.Click += (s, ev) => OnRequestNewInstance?.Invoke();
            menu.Items.Add(itemNew);

            var itemPause = new System.Windows.Controls.MenuItem { Header = "Pause / Resume (Win + Enter)", Foreground = _greenBrush };
            itemPause.Click += (s, ev) => Manager?.PauseToggle();
            menu.Items.Add(itemPause);

            var itemReset = new System.Windows.Controls.MenuItem { Header = "Reset (Win + Backspace)", Foreground = _greenBrush };
            itemReset.Click += (s, ev) => Manager?.Reset();
            menu.Items.Add(itemReset);

            var itemSwitch = new System.Windows.Controls.MenuItem { Header = "Switch Mode (Win + Esc)", Foreground = _greenBrush };
            itemSwitch.Click += (s, ev) => Manager?.SwitchMode();
            menu.Items.Add(itemSwitch);

            var itemSave = new System.Windows.Controls.MenuItem { Header = "Save Countdown (Win + S)", Foreground = _greenBrush };
            itemSave.Click += (s, ev) => Manager?.SaveCountdown();
            menu.Items.Add(itemSave);

            if (Manager?.Mode == AppMode.Timer)
            {
                var itemSet = new System.Windows.Controls.MenuItem { Header = "Set Countdown (Middle Click)", Foreground = _greenBrush };
                itemSet.Click += (s, ev) => OpenTimerInputDialog();
                menu.Items.Add(itemSet);
            }

            var itemEditShortcut = new System.Windows.Controls.MenuItem { Header = "Edit Shortcut (Chỉnh phím tắt)", Foreground = _greenBrush };
            itemEditShortcut.Click += (s, ev) =>
            {
                if (Application.Current is App app)
                {
                    app.OpenShortcutSettingsDialog();
                }
            };
            menu.Items.Add(itemEditShortcut);

            menu.Items.Add(new System.Windows.Controls.Separator());

            // Click Through option
            var itemClickThrough = new System.Windows.Controls.MenuItem
            {
                Header = IsClickThrough ? "✓ Click Through (Bấm xuyên qua: BẬT)" : "Click Through (Bấm xuyên qua)",
                Foreground = _greenBrush,
                ToolTip = "Khi bật, nhấn Ctrl + Chuột giữa vào HUD để tắt"
            };
            itemClickThrough.Click += (s, ev) => SetClickThrough(!IsClickThrough);
            menu.Items.Add(itemClickThrough);

            // Opacity Slider Card
            var opacityCard = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x35, 0x30, 0x30, 0x3A)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x23, 0xFF, 0x23)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(6, 4, 6, 4),
                Padding = new Thickness(8, 6, 8, 6),
                Width = 200
            };

            var cardStack = new System.Windows.Controls.StackPanel();

            var headerDock = new System.Windows.Controls.DockPanel { LastChildFill = false };
            var lblTitle = new System.Windows.Controls.TextBlock
            {
                Text = "Opacity",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xFF, 0xA0))
            };
            var lblValue = new System.Windows.Controls.TextBlock
            {
                Text = $"{(int)(Opacity * 100)}%",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = _greenBrush
            };
            System.Windows.Controls.DockPanel.SetDock(lblTitle, System.Windows.Controls.Dock.Left);
            System.Windows.Controls.DockPanel.SetDock(lblValue, System.Windows.Controls.Dock.Right);
            headerDock.Children.Add(lblTitle);
            headerDock.Children.Add(lblValue);
            cardStack.Children.Add(headerDock);

            var slider = new System.Windows.Controls.Slider
            {
                Minimum = 20,
                Maximum = 100,
                Value = (int)(Opacity * 100),
                SmallChange = 5,
                LargeChange = 10,
                TickFrequency = 5,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 5, 0, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = (Style)Resources["CyberpunkSlider"]
            };

            slider.ValueChanged += (s, ev) =>
            {
                int val = (int)ev.NewValue;
                lblValue.Text = $"{val}%";
                SetWindowOpacity(val / 100.0);
            };

            opacityCard.PreviewMouseLeftButtonDown += (s, ev) => ev.Handled = false;
            opacityCard.PreviewMouseRightButtonDown += (s, ev) => ev.Handled = true;

            cardStack.Children.Add(slider);
            opacityCard.Child = cardStack;

            menu.Items.Add(opacityCard);

            menu.Items.Add(new System.Windows.Controls.Separator());

            var itemClose = new System.Windows.Controls.MenuItem { Header = "Close This Timer (Win + W)", Foreground = _redBrush };
            itemClose.Click += (s, ev) => OnRequestCloseInstance?.Invoke(this);
            menu.Items.Add(itemClose);

            var itemExitAll = new System.Windows.Controls.MenuItem { Header = "Exit All", Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x77, 0x77)) };
            itemExitAll.Click += (s, ev) => OnRequestExitAll?.Invoke();
            menu.Items.Add(itemExitAll);

            menu.IsOpen = true;
        }

        public void ClampToScreen()
        {
            try
            {
                double currentLeft = double.IsNaN(Left) || double.IsInfinity(Left) ? 150 : Left;
                double currentTop = double.IsNaN(Top) || double.IsInfinity(Top) ? 150 : Top;

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
                    _settings.WindowX = (int)Left;
                    _settings.WindowY = (int)Top;
                    _settings.Save();
                }
            }
            catch { }
        }
    }
}
