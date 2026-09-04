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
        private readonly SolidColorBrush _greenBorder = new SolidColorBrush(Color.FromArgb(0x44, 0x23, 0xFF, 0x23));
        private readonly SolidColorBrush _redBorder = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0x23, 0x23));
        private DropShadowEffect _normalGlow;
        private DropShadowEffect _redGlow;
        private bool _isDragging = false;

        public MainWindow(SettingsManager settings)
        {
            InitializeComponent();
            _settings = settings;

            _normalGlow = (DropShadowEffect)Resources["NormalGlow"];
            _redGlow = (DropShadowEffect)Resources["RedGlow"];

            // Load saved position
            Left = _settings.WindowX;
            Top = _settings.WindowY;

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            Win32Api.SetToolWindowAndNoActivate(handle);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ClampToScreen();
        }

        public void UpdateDisplay(string mainText, string prefixText, string subText, bool isRed, bool isBlinkHidden)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateDisplay(mainText, prefixText, subText, isRed, isBlinkHidden));
                return;
            }

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
                RootBorder.BorderBrush = _greenBorder;
            }

            TxtMainTime.Opacity = isBlinkHidden ? 0.0 : 1.0;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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

                _settings.WindowX = (int)Left;
                _settings.WindowY = (int)Top;
                _settings.Save();
            }
            catch { }
        }
    }
}
