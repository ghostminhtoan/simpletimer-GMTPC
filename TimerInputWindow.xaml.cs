using System;
using System.Windows;
using System.Windows.Input;

namespace TimeBomb
{
    public partial class TimerInputWindow : Window
    {
        public event Action<int, int> OnTimeConfirmed;

        private bool _isClosing = false;

        public TimerInputWindow(int initialMinutes, int initialSeconds)
        {
            InitializeComponent();

            TxtInput.Text = string.Format("{0:D2}:{1:D2}", Math.Max(0, initialMinutes), Math.Max(0, Math.Min(59, initialSeconds)));

            Loaded += (s, e) =>
            {
                TxtInput.Focus();
                TxtInput.SelectAll();
            };

            Deactivated += (s, e) =>
            {
                SafeClose();
            };
        }

        private void SafeClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            try { Close(); } catch { }
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmAndClose();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                SafeClose();
            }
        }

        private void BtnSet_Click(object sender, RoutedEventArgs e)
        {
            ConfirmAndClose();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
        }

        private void ConfirmAndClose()
        {
            if (TryParseTime(TxtInput.Text, out int minutes, out int seconds))
            {
                OnTimeConfirmed?.Invoke(minutes, seconds);
                SafeClose();
            }
            else
            {
                // Highlight invalid input
                TxtInput.SelectAll();
                TxtInput.Focus();
            }
        }

        public static bool TryParseTime(string rawInput, out int minutes, out int seconds)
        {
            minutes = 0;
            seconds = 0;

            if (string.IsNullOrWhiteSpace(rawInput))
                return false;

            string s = rawInput.Trim().ToLowerInvariant();

            // Suffix 's' e.g. "90s", "45s"
            if (s.EndsWith("s") && int.TryParse(s.Substring(0, s.Length - 1), out int totalSecOnly))
            {
                if (totalSecOnly < 1) return false;
                minutes = totalSecOnly / 60;
                seconds = totalSecOnly % 60;
                return true;
            }

            // Colon notation mm:ss or hh:mm:ss or :ss
            if (s.Contains(":"))
            {
                string[] parts = s.Split(':');
                if (parts.Length == 2)
                {
                    int m = 0;
                    if (!string.IsNullOrEmpty(parts[0]) && !int.TryParse(parts[0], out m))
                        return false;

                    if (!int.TryParse(parts[1], out int sec) || sec < 0)
                        return false;

                    int total = m * 60 + sec;
                    if (total < 1) return false;

                    minutes = total / 60;
                    seconds = total % 60;
                    return true;
                }
                else if (parts.Length == 3)
                {
                    if (!int.TryParse(parts[0], out int h) ||
                        !int.TryParse(parts[1], out int m) ||
                        !int.TryParse(parts[2], out int sec))
                        return false;

                    int total = h * 3600 + m * 60 + sec;
                    if (total < 1) return false;

                    minutes = total / 60;
                    seconds = total % 60;
                    return true;
                }
                return false;
            }

            // Pure number
            if (int.TryParse(s, out int val))
            {
                if (val < 1) return false;

                // Treat pure numbers as minutes (e.g. 5 -> 5 mins, 15 -> 15 mins)
                minutes = Math.Min(999, val);
                seconds = 0;
                return true;
            }

            return false;
        }
    }
}
