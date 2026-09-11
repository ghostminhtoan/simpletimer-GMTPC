using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TimeBomb.Core;

namespace TimeBomb
{
    public partial class ToastNotificationWindow : Window
    {
        private static ToastNotificationWindow _activeToast = null;
        private readonly DispatcherTimer _dismissTimer;
        private bool _isFadingOut = false;

        public ToastNotificationWindow(string message, double durationSeconds = 4.0)
        {
            InitializeComponent();

            TxtMessage.Text = message;
            Opacity = 0;

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;

            double fadeOutDuration = 0.5;
            double displayDuration = Math.Max(0.5, durationSeconds - fadeOutDuration);

            _dismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(displayDuration)
            };
            _dismissTimer.Tick += (s, e) =>
            {
                _dismissTimer.Stop();
                BeginFadeOut(fadeOutDuration);
            };
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                // Ensure the toast never steals focus
                Win32Api.SetToolWindowAndNoActivate(handle);
                // Make the toast click-through so user interactions are never blocked
                Win32Api.SetClickThrough(handle, true);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Position at bottom right of the primary work area
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 24;

            // Slide & Fade-In Animation
            var translate = new TranslateTransform(0, 15);
            RootBorder.RenderTransform = translate;

            var slideAnim = new DoubleAnimation(15, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeInAnim = new DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            translate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            BeginAnimation(OpacityProperty, fadeInAnim);

            _dismissTimer.Start();
        }

        private void BeginFadeOut(double fadeDuration)
        {
            if (_isFadingOut) return;
            _isFadingOut = true;

            var fadeOutAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(fadeDuration))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOutAnim.Completed += (s, e) =>
            {
                try
                {
                    Close();
                }
                catch { }
                if (_activeToast == this)
                {
                    _activeToast = null;
                }
            };

            BeginAnimation(OpacityProperty, fadeOutAnim);
        }

        /// <summary>
        /// Displays a non-intrusive, auto-dismissing Cyberpunk notification toast.
        /// </summary>
        public static void ShowToast(string message, double durationSeconds = 4.0)
        {
            var app = Application.Current;
            if (app == null) return;

            if (!app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(new Action(() => ShowToast(message, durationSeconds)));
                return;
            }

            try
            {
                if (_activeToast != null)
                {
                    try { _activeToast.Close(); } catch { }
                    _activeToast = null;
                }

                _activeToast = new ToastNotificationWindow(message, durationSeconds);
                _activeToast.Show();
            }
            catch { }
        }
    }
}
