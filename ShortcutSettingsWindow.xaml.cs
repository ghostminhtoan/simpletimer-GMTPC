using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimeBomb.Core;

namespace TimeBomb
{
    public class ShortcutRowItem
    {
        public string KeyName { get; set; }
        public string Description { get; set; }

        // Primary Shortcut
        public bool Win { get; set; }
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public uint VkCode { get; set; }

        public CheckBox ChkWin { get; set; }
        public CheckBox ChkCtrl { get; set; }
        public CheckBox ChkAlt { get; set; }
        public CheckBox ChkShift { get; set; }
        public TextBox TxtKey { get; set; }

        // Secondary Shortcut
        public bool Enabled2 { get; set; }
        public bool Win2 { get; set; }
        public bool Ctrl2 { get; set; }
        public bool Alt2 { get; set; }
        public bool Shift2 { get; set; }
        public uint VkCode2 { get; set; }

        public CheckBox ChkEnabled2 { get; set; }
        public CheckBox ChkWin2 { get; set; }
        public CheckBox ChkCtrl2 { get; set; }
        public CheckBox ChkAlt2 { get; set; }
        public CheckBox ChkShift2 { get; set; }
        public TextBox TxtKey2 { get; set; }
        public FrameworkElement Panel2 { get; set; }
    }

    public partial class ShortcutSettingsWindow : Window
    {
        private readonly SettingsManager _settings;
        private readonly List<ShortcutRowItem> _items = new List<ShortcutRowItem>();

        public event Action OnShortcutsSaved;

        public ShortcutSettingsWindow(SettingsManager settings)
        {
            InitializeComponent();
            _settings = settings;

            InitItems();
            BuildUI();
        }

        private void InitItems()
        {
            _items.Clear();
            _items.Add(new ShortcutRowItem
            {
                KeyName = "ToggleHUD", Description = "Show / Hide Timer",
                Win = _settings.KeyToggleHUD_Win, Ctrl = _settings.KeyToggleHUD_Ctrl, Alt = _settings.KeyToggleHUD_Alt, Shift = _settings.KeyToggleHUD_Shift, VkCode = _settings.KeyToggleHUD,
                Enabled2 = _settings.KeyToggleHUD_2_Enabled, Win2 = _settings.KeyToggleHUD_2_Win, Ctrl2 = _settings.KeyToggleHUD_2_Ctrl, Alt2 = _settings.KeyToggleHUD_2_Alt, Shift2 = _settings.KeyToggleHUD_2_Shift, VkCode2 = _settings.KeyToggleHUD_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "IntervalTimer", Description = "Interval Timer",
                Win = _settings.KeyIntervalTimer_Win, Ctrl = _settings.KeyIntervalTimer_Ctrl, Alt = _settings.KeyIntervalTimer_Alt, Shift = _settings.KeyIntervalTimer_Shift, VkCode = _settings.KeyIntervalTimer,
                Enabled2 = _settings.KeyIntervalTimer_2_Enabled, Win2 = _settings.KeyIntervalTimer_2_Win, Ctrl2 = _settings.KeyIntervalTimer_2_Ctrl, Alt2 = _settings.KeyIntervalTimer_2_Alt, Shift2 = _settings.KeyIntervalTimer_2_Shift, VkCode2 = _settings.KeyIntervalTimer_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "PauseToggle", Description = "Pause / Resume",
                Win = _settings.KeyPauseToggle_Win, Ctrl = _settings.KeyPauseToggle_Ctrl, Alt = _settings.KeyPauseToggle_Alt, Shift = _settings.KeyPauseToggle_Shift, VkCode = _settings.KeyPauseToggle,
                Enabled2 = _settings.KeyPauseToggle_2_Enabled, Win2 = _settings.KeyPauseToggle_2_Win, Ctrl2 = _settings.KeyPauseToggle_2_Ctrl, Alt2 = _settings.KeyPauseToggle_2_Alt, Shift2 = _settings.KeyPauseToggle_2_Shift, VkCode2 = _settings.KeyPauseToggle_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "Reset", Description = "Reset Timer / Alarm",
                Win = _settings.KeyReset_Win, Ctrl = _settings.KeyReset_Ctrl, Alt = _settings.KeyReset_Alt, Shift = _settings.KeyReset_Shift, VkCode = _settings.KeyReset,
                Enabled2 = _settings.KeyReset_2_Enabled, Win2 = _settings.KeyReset_2_Win, Ctrl2 = _settings.KeyReset_2_Ctrl, Alt2 = _settings.KeyReset_2_Alt, Shift2 = _settings.KeyReset_2_Shift, VkCode2 = _settings.KeyReset_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "SaveCountdown", Description = "Save Default Timer",
                Win = _settings.KeySaveCountdown_Win, Ctrl = _settings.KeySaveCountdown_Ctrl, Alt = _settings.KeySaveCountdown_Alt, Shift = _settings.KeySaveCountdown_Shift, VkCode = _settings.KeySaveCountdown,
                Enabled2 = _settings.KeySaveCountdown_2_Enabled, Win2 = _settings.KeySaveCountdown_2_Win, Ctrl2 = _settings.KeySaveCountdown_2_Ctrl, Alt2 = _settings.KeySaveCountdown_2_Alt, Shift2 = _settings.KeySaveCountdown_2_Shift, VkCode2 = _settings.KeySaveCountdown_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "SwitchMode", Description = "Switch Mode",
                Win = _settings.KeySwitchMode_Win, Ctrl = _settings.KeySwitchMode_Ctrl, Alt = _settings.KeySwitchMode_Alt, Shift = _settings.KeySwitchMode_Shift, VkCode = _settings.KeySwitchMode,
                Enabled2 = _settings.KeySwitchMode_2_Enabled, Win2 = _settings.KeySwitchMode_2_Win, Ctrl2 = _settings.KeySwitchMode_2_Ctrl, Alt2 = _settings.KeySwitchMode_2_Alt, Shift2 = _settings.KeySwitchMode_2_Shift, VkCode2 = _settings.KeySwitchMode_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "NewInstance", Description = "New Timer Instance",
                Win = _settings.KeyNewInstance_Win, Ctrl = _settings.KeyNewInstance_Ctrl, Alt = _settings.KeyNewInstance_Alt, Shift = _settings.KeyNewInstance_Shift, VkCode = _settings.KeyNewInstance,
                Enabled2 = _settings.KeyNewInstance_2_Enabled, Win2 = _settings.KeyNewInstance_2_Win, Ctrl2 = _settings.KeyNewInstance_2_Ctrl, Alt2 = _settings.KeyNewInstance_2_Alt, Shift2 = _settings.KeyNewInstance_2_Shift, VkCode2 = _settings.KeyNewInstance_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "CloseInstance", Description = "Close Instance",
                Win = _settings.KeyCloseInstance_Win, Ctrl = _settings.KeyCloseInstance_Ctrl, Alt = _settings.KeyCloseInstance_Alt, Shift = _settings.KeyCloseInstance_Shift, VkCode = _settings.KeyCloseInstance,
                Enabled2 = _settings.KeyCloseInstance_2_Enabled, Win2 = _settings.KeyCloseInstance_2_Win, Ctrl2 = _settings.KeyCloseInstance_2_Ctrl, Alt2 = _settings.KeyCloseInstance_2_Alt, Shift2 = _settings.KeyCloseInstance_2_Shift, VkCode2 = _settings.KeyCloseInstance_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "AdjustUp", Description = "Increase Timer (+1m)",
                Win = _settings.KeyAdjustUp_Win, Ctrl = _settings.KeyAdjustUp_Ctrl, Alt = _settings.KeyAdjustUp_Alt, Shift = _settings.KeyAdjustUp_Shift, VkCode = _settings.KeyAdjustUp,
                Enabled2 = _settings.KeyAdjustUp_2_Enabled, Win2 = _settings.KeyAdjustUp_2_Win, Ctrl2 = _settings.KeyAdjustUp_2_Ctrl, Alt2 = _settings.KeyAdjustUp_2_Alt, Shift2 = _settings.KeyAdjustUp_2_Shift, VkCode2 = _settings.KeyAdjustUp_2
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "AdjustDown", Description = "Decrease Timer (-1m)",
                Win = _settings.KeyAdjustDown_Win, Ctrl = _settings.KeyAdjustDown_Ctrl, Alt = _settings.KeyAdjustDown_Alt, Shift = _settings.KeyAdjustDown_Shift, VkCode = _settings.KeyAdjustDown,
                Enabled2 = _settings.KeyAdjustDown_2_Enabled, Win2 = _settings.KeyAdjustDown_2_Win, Ctrl2 = _settings.KeyAdjustDown_2_Ctrl, Alt2 = _settings.KeyAdjustDown_2_Alt, Shift2 = _settings.KeyAdjustDown_2_Shift, VkCode2 = _settings.KeyAdjustDown_2
            });
        }

        private void BuildUI()
        {
            PnlShortcuts.Children.Clear();

            foreach (var item in _items)
            {
                var cardBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x40, 0x24, 0x24, 0x30)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x35, 0x23, 0xFF, 0x23)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(10, 8, 10, 8)
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Row 0: Primary Shortcut
                var row0 = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

                // Description
                var txtDesc = new TextBlock
                {
                    Text = item.Description,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xFF, 0xA0)),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txtDesc, 0);

                // Primary Modifiers
                var pnlChk1 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var chkCtrl1 = new CheckBox { Content = "Ctrl", IsChecked = item.Ctrl, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkWin1 = new CheckBox { Content = "Win", IsChecked = item.Win, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkAlt1 = new CheckBox { Content = "Alt", IsChecked = item.Alt, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkShift1 = new CheckBox { Content = "Shift", IsChecked = item.Shift, Style = (Style)Resources["CyberpunkCheckBox"] };

                item.ChkCtrl = chkCtrl1;
                item.ChkWin = chkWin1;
                item.ChkAlt = chkAlt1;
                item.ChkShift = chkShift1;

                pnlChk1.Children.Add(chkCtrl1);
                pnlChk1.Children.Add(chkWin1);
                pnlChk1.Children.Add(chkAlt1);
                pnlChk1.Children.Add(chkShift1);
                Grid.SetColumn(pnlChk1, 1);

                // Primary Key TextBox
                var txtKey1 = new TextBox
                {
                    Text = GetKeyName(item.VkCode),
                    Style = (Style)Resources["CyberpunkTextBox"],
                    IsReadOnly = true,
                    Cursor = Cursors.Hand,
                    Tag = item
                };
                item.TxtKey = txtKey1;

                txtKey1.PreviewKeyDown += (s, e) =>
                {
                    Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
                    if (key == Key.LWin || key == Key.RWin || key == Key.LeftCtrl || key == Key.RightCtrl ||
                        key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift)
                    {
                        return;
                    }

                    uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    item.VkCode = vk;
                    txtKey1.Text = GetKeyName(vk);
                    e.Handled = true;
                };
                Grid.SetColumn(txtKey1, 2);

                row0.Children.Add(txtDesc);
                row0.Children.Add(pnlChk1);
                row0.Children.Add(txtKey1);
                Grid.SetRow(row0, 0);

                // Row 1: Secondary Shortcut (Checkbox to enable + modifiers & key)
                var row1 = new Grid { Margin = new Thickness(0, 2, 0, 0) };
                row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

                var chkEnabled2 = new CheckBox
                {
                    Content = "Phím tắt 2 (Phụ)",
                    IsChecked = item.Enabled2,
                    Style = (Style)Resources["CyberpunkCheckBox"],
                    Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0xDF, 0x80)),
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center
                };
                item.ChkEnabled2 = chkEnabled2;
                Grid.SetColumn(chkEnabled2, 0);

                // Container for Secondary Modifiers + Key
                var pnlChk2 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var chkCtrl2 = new CheckBox { Content = "Ctrl", IsChecked = item.Ctrl2, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkWin2 = new CheckBox { Content = "Win", IsChecked = item.Win2, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkAlt2 = new CheckBox { Content = "Alt", IsChecked = item.Alt2, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkShift2 = new CheckBox { Content = "Shift", IsChecked = item.Shift2, Style = (Style)Resources["CyberpunkCheckBox"] };

                item.ChkCtrl2 = chkCtrl2;
                item.ChkWin2 = chkWin2;
                item.ChkAlt2 = chkAlt2;
                item.ChkShift2 = chkShift2;

                pnlChk2.Children.Add(chkCtrl2);
                pnlChk2.Children.Add(chkWin2);
                pnlChk2.Children.Add(chkAlt2);
                pnlChk2.Children.Add(chkShift2);
                Grid.SetColumn(pnlChk2, 1);

                var txtKey2 = new TextBox
                {
                    Text = GetKeyName(item.VkCode2),
                    Style = (Style)Resources["CyberpunkTextBox"],
                    IsReadOnly = true,
                    Cursor = Cursors.Hand,
                    Tag = item
                };
                item.TxtKey2 = txtKey2;

                txtKey2.PreviewKeyDown += (s, e) =>
                {
                    Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
                    if (key == Key.LWin || key == Key.RWin || key == Key.LeftCtrl || key == Key.RightCtrl ||
                        key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift)
                    {
                        return;
                    }

                    uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    item.VkCode2 = vk;
                    txtKey2.Text = GetKeyName(vk);
                    e.Handled = true;
                };
                Grid.SetColumn(txtKey2, 2);

                // Group secondary controls in sub-grid for unified enable/disable & opacity
                var subGrid2 = new Grid();
                subGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                subGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                subGrid2.Children.Add(pnlChk2);
                subGrid2.Children.Add(txtKey2);
                Grid.SetColumn(subGrid2, 1);
                Grid.SetColumnSpan(subGrid2, 2);

                item.Panel2 = subGrid2;

                Action updateState2 = () =>
                {
                    bool isEn = chkEnabled2.IsChecked == true;
                    subGrid2.IsEnabled = isEn;
                    subGrid2.Opacity = isEn ? 1.0 : 0.35;
                };

                chkEnabled2.Checked += (s, e) => updateState2();
                chkEnabled2.Unchecked += (s, e) => updateState2();
                updateState2();

                row1.Children.Add(chkEnabled2);
                row1.Children.Add(subGrid2);
                Grid.SetRow(row1, 1);

                mainGrid.Children.Add(row0);
                mainGrid.Children.Add(row1);

                cardBorder.Child = mainGrid;
                PnlShortcuts.Children.Add(cardBorder);
            }
        }

        private string GetKeyName(uint vk)
        {
            switch (vk)
            {
                case Win32Api.VK_OEM_3: return "`";
                case Win32Api.VK_SPACE: return "Space";
                case Win32Api.VK_BACK: return "Backspace";
                case Win32Api.VK_ESCAPE: return "Esc";
                case Win32Api.VK_DELETE: return "Delete";
                case Win32Api.VK_UP: return "Up";
                case Win32Api.VK_DOWN: return "Down";
                default:
                    Key key = KeyInterop.KeyFromVirtualKey((int)vk);
                    return key.ToString();
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            _settings.ResetHotkeysToDefault();
            InitItems();
            BuildUI();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items)
            {
                // Primary
                bool w1 = item.ChkWin.IsChecked == true;
                bool c1 = item.ChkCtrl.IsChecked == true;
                bool a1 = item.ChkAlt.IsChecked == true;
                bool s1 = item.ChkShift.IsChecked == true;
                uint vk1 = item.VkCode;

                // Secondary
                bool en2 = item.ChkEnabled2.IsChecked == true;
                bool w2 = item.ChkWin2.IsChecked == true;
                bool c2 = item.ChkCtrl2.IsChecked == true;
                bool a2 = item.ChkAlt2.IsChecked == true;
                bool s2 = item.ChkShift2.IsChecked == true;
                uint vk2 = item.VkCode2;

                switch (item.KeyName)
                {
                    case "ToggleHUD":
                        _settings.KeyToggleHUD_Win = w1; _settings.KeyToggleHUD_Ctrl = c1; _settings.KeyToggleHUD_Alt = a1; _settings.KeyToggleHUD_Shift = s1; _settings.KeyToggleHUD = vk1;
                        _settings.KeyToggleHUD_2_Enabled = en2; _settings.KeyToggleHUD_2_Win = w2; _settings.KeyToggleHUD_2_Ctrl = c2; _settings.KeyToggleHUD_2_Alt = a2; _settings.KeyToggleHUD_2_Shift = s2; _settings.KeyToggleHUD_2 = vk2;
                        break;
                    case "IntervalTimer":
                        _settings.KeyIntervalTimer_Win = w1; _settings.KeyIntervalTimer_Ctrl = c1; _settings.KeyIntervalTimer_Alt = a1; _settings.KeyIntervalTimer_Shift = s1; _settings.KeyIntervalTimer = vk1;
                        _settings.KeyIntervalTimer_2_Enabled = en2; _settings.KeyIntervalTimer_2_Win = w2; _settings.KeyIntervalTimer_2_Ctrl = c2; _settings.KeyIntervalTimer_2_Alt = a2; _settings.KeyIntervalTimer_2_Shift = s2; _settings.KeyIntervalTimer_2 = vk2;
                        break;
                    case "PauseToggle":
                        _settings.KeyPauseToggle_Win = w1; _settings.KeyPauseToggle_Ctrl = c1; _settings.KeyPauseToggle_Alt = a1; _settings.KeyPauseToggle_Shift = s1; _settings.KeyPauseToggle = vk1;
                        _settings.KeyPauseToggle_2_Enabled = en2; _settings.KeyPauseToggle_2_Win = w2; _settings.KeyPauseToggle_2_Ctrl = c2; _settings.KeyPauseToggle_2_Alt = a2; _settings.KeyPauseToggle_2_Shift = s2; _settings.KeyPauseToggle_2 = vk2;
                        break;
                    case "Reset":
                        _settings.KeyReset_Win = w1; _settings.KeyReset_Ctrl = c1; _settings.KeyReset_Alt = a1; _settings.KeyReset_Shift = s1; _settings.KeyReset = vk1;
                        _settings.KeyReset_2_Enabled = en2; _settings.KeyReset_2_Win = w2; _settings.KeyReset_2_Ctrl = c2; _settings.KeyReset_2_Alt = a2; _settings.KeyReset_2_Shift = s2; _settings.KeyReset_2 = vk2;
                        break;
                    case "SaveCountdown":
                        _settings.KeySaveCountdown_Win = w1; _settings.KeySaveCountdown_Ctrl = c1; _settings.KeySaveCountdown_Alt = a1; _settings.KeySaveCountdown_Shift = s1; _settings.KeySaveCountdown = vk1;
                        _settings.KeySaveCountdown_2_Enabled = en2; _settings.KeySaveCountdown_2_Win = w2; _settings.KeySaveCountdown_2_Ctrl = c2; _settings.KeySaveCountdown_2_Alt = a2; _settings.KeySaveCountdown_2_Shift = s2; _settings.KeySaveCountdown_2 = vk2;
                        break;
                    case "SwitchMode":
                        _settings.KeySwitchMode_Win = w1; _settings.KeySwitchMode_Ctrl = c1; _settings.KeySwitchMode_Alt = a1; _settings.KeySwitchMode_Shift = s1; _settings.KeySwitchMode = vk1;
                        _settings.KeySwitchMode_2_Enabled = en2; _settings.KeySwitchMode_2_Win = w2; _settings.KeySwitchMode_2_Ctrl = c2; _settings.KeySwitchMode_2_Alt = a2; _settings.KeySwitchMode_2_Shift = s2; _settings.KeySwitchMode_2 = vk2;
                        break;
                    case "NewInstance":
                        _settings.KeyNewInstance_Win = w1; _settings.KeyNewInstance_Ctrl = c1; _settings.KeyNewInstance_Alt = a1; _settings.KeyNewInstance_Shift = s1; _settings.KeyNewInstance = vk1;
                        _settings.KeyNewInstance_2_Enabled = en2; _settings.KeyNewInstance_2_Win = w2; _settings.KeyNewInstance_2_Ctrl = c2; _settings.KeyNewInstance_2_Alt = a2; _settings.KeyNewInstance_2_Shift = s2; _settings.KeyNewInstance_2 = vk2;
                        break;
                    case "CloseInstance":
                        _settings.KeyCloseInstance_Win = w1; _settings.KeyCloseInstance_Ctrl = c1; _settings.KeyCloseInstance_Alt = a1; _settings.KeyCloseInstance_Shift = s1; _settings.KeyCloseInstance = vk1;
                        _settings.KeyCloseInstance_2_Enabled = en2; _settings.KeyCloseInstance_2_Win = w2; _settings.KeyCloseInstance_2_Ctrl = c2; _settings.KeyCloseInstance_2_Alt = a2; _settings.KeyCloseInstance_2_Shift = s2; _settings.KeyCloseInstance_2 = vk2;
                        break;
                    case "AdjustUp":
                        _settings.KeyAdjustUp_Win = w1; _settings.KeyAdjustUp_Ctrl = c1; _settings.KeyAdjustUp_Alt = a1; _settings.KeyAdjustUp_Shift = s1; _settings.KeyAdjustUp = vk1;
                        _settings.KeyAdjustUp_2_Enabled = en2; _settings.KeyAdjustUp_2_Win = w2; _settings.KeyAdjustUp_2_Ctrl = c2; _settings.KeyAdjustUp_2_Alt = a2; _settings.KeyAdjustUp_2_Shift = s2; _settings.KeyAdjustUp_2 = vk2;
                        break;
                    case "AdjustDown":
                        _settings.KeyAdjustDown_Win = w1; _settings.KeyAdjustDown_Ctrl = c1; _settings.KeyAdjustDown_Alt = a1; _settings.KeyAdjustDown_Shift = s1; _settings.KeyAdjustDown = vk1;
                        _settings.KeyAdjustDown_2_Enabled = en2; _settings.KeyAdjustDown_2_Win = w2; _settings.KeyAdjustDown_2_Ctrl = c2; _settings.KeyAdjustDown_2_Alt = a2; _settings.KeyAdjustDown_2_Shift = s2; _settings.KeyAdjustDown_2 = vk2;
                        break;
                }
            }

            _settings.Save();
            OnShortcutsSaved?.Invoke();
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
