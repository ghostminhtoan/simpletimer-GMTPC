using System;
using System.Windows;
using System.Windows.Input;
using TimeBomb.Core;

namespace TimeBomb
{
    public partial class IntervalPresetEditWindow : Window
    {
        public IntervalPreset Preset { get; private set; }

        public IntervalPresetEditWindow(IntervalPreset preset)
        {
            InitializeComponent();
            Preset = preset?.Clone() ?? new IntervalPreset();

            TxtHeaderTitle.Text = $"EDIT PRESET #{Preset.Id}: {Preset.Name}";
            TxtName.Text = Preset.Name;
            TxtWork.Text = Preset.Work.ToString();
            TxtRest.Text = Preset.Rest.ToString();
            TxtPrepare.Text = Preset.Prepare.ToString();
            TxtEnd.Text = Preset.End.ToString();
            
            if (Preset.IsInfinite)
            {
                ChkInfinite.IsChecked = true;
                TxtLoops.Text = "∞";
                TxtLoops.IsEnabled = false;
            }
            else
            {
                ChkInfinite.IsChecked = false;
                TxtLoops.Text = Preset.Loops.ToString();
                TxtLoops.IsEnabled = true;
            }

            Loaded += (s, e) =>
            {
                TxtName.Focus();
                TxtName.SelectAll();
            };

            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };
        }

        private void ChkInfinite_Checked(object sender, RoutedEventArgs e)
        {
            TxtLoops.Text = "∞";
            TxtLoops.IsEnabled = false;
        }

        private void ChkInfinite_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtLoops.IsEnabled = true;
            TxtLoops.Text = Preset.Loops > 0 ? Preset.Loops.ToString() : "5";
            TxtLoops.SelectAll();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveAndClose();
        }

        private void SaveAndClose()
        {
            string name = TxtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Preset {Preset.Id}";
            }

            if (!int.TryParse(TxtWork.Text, out int work) || work < 1)
            {
                MessageBox.Show("Work time must be at least 1 second.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtWork.Focus();
                return;
            }

            if (!int.TryParse(TxtRest.Text, out int rest) || rest < 0)
            {
                rest = 0;
            }

            if (!int.TryParse(TxtPrepare.Text, out int prep) || prep < 0)
            {
                prep = 0;
            }

            if (!int.TryParse(TxtEnd.Text, out int endVal) || endVal < 0)
            {
                endVal = 0;
            }

            bool infinite = ChkInfinite.IsChecked == true;
            int loops = 5;
            if (!infinite)
            {
                if (!int.TryParse(TxtLoops.Text, out loops) || loops < 1)
                {
                    loops = 1;
                }
            }

            Preset.Name = name;
            Preset.Work = work;
            Preset.Rest = rest;
            Preset.Prepare = prep;
            Preset.End = endVal;
            Preset.Loops = loops;
            Preset.IsInfinite = infinite;

            DialogResult = true;
            Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var defs = SettingsManager.GetDefaultPresets();
            int idx = Preset.Id - 1;
            if (idx >= 0 && idx < defs.Count)
            {
                var def = defs[idx];
                TxtName.Text = def.Name;
                TxtWork.Text = def.Work.ToString();
                TxtRest.Text = def.Rest.ToString();
                TxtPrepare.Text = def.Prepare.ToString();
                TxtEnd.Text = def.End.ToString();
                ChkInfinite.IsChecked = def.IsInfinite;
                TxtLoops.Text = def.IsInfinite ? "∞" : def.Loops.ToString();
                TxtLoops.IsEnabled = !def.IsInfinite;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveAndClose();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }
    }
}
