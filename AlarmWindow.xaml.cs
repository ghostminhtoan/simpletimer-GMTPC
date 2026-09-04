using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TimeBomb.Core;

namespace TimeBomb
{
    public partial class AlarmWindow : Window
    {
        private readonly TimeBombManager _manager;

        public AlarmWindow(TimeBombManager manager)
        {
            InitializeComponent();
            _manager = manager;

            SourceInitialized += OnSourceInitialized;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            Win32Api.SetToolWindowAndNoActivate(handle);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Block spacebar from activating buttons inadvertently
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void OnBorderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            _manager.DismissAlarm();
            Hide();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _manager.ResetAlarm();
            Hide();
        }
    }
}
