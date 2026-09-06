using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimeBomb.Core;

namespace TimeBomb
{
    public class ShortcutRowItem
    {
        public string KeyName { get; set; }
        public string Description { get; set; }

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
                Win = _settings.KeyToggleHUD_Win, Ctrl = _settings.KeyToggleHUD_Ctrl, Alt = _settings.KeyToggleHUD_Alt, Shift = _settings.KeyToggleHUD_Shift, VkCode = _settings.KeyToggleHUD
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "PauseToggle", Description = "Pause / Resume",
                Win = _settings.KeyPauseToggle_Win, Ctrl = _settings.KeyPauseToggle_Ctrl, Alt = _settings.KeyPauseToggle_Alt, Shift = _settings.KeyPauseToggle_Shift, VkCode = _settings.KeyPauseToggle
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "Reset", Description = "Reset Timer / Alarm",
                Win = _settings.KeyReset_Win, Ctrl = _settings.KeyReset_Ctrl, Alt = _settings.KeyReset_Alt, Shift = _settings.KeyReset_Shift, VkCode = _settings.KeyReset
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "SaveCountdown", Description = "Save Default Timer",
                Win = _settings.KeySaveCountdown_Win, Ctrl = _settings.KeySaveCountdown_Ctrl, Alt = _settings.KeySaveCountdown_Alt, Shift = _settings.KeySaveCountdown_Shift, VkCode = _settings.KeySaveCountdown
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "SwitchMode", Description = "Switch Mode",
                Win = _settings.KeySwitchMode_Win, Ctrl = _settings.KeySwitchMode_Ctrl, Alt = _settings.KeySwitchMode_Alt, Shift = _settings.KeySwitchMode_Shift, VkCode = _settings.KeySwitchMode
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "NewInstance", Description = "New Timer Instance",
                Win = _settings.KeyNewInstance_Win, Ctrl = _settings.KeyNewInstance_Ctrl, Alt = _settings.KeyNewInstance_Alt, Shift = _settings.KeyNewInstance_Shift, VkCode = _settings.KeyNewInstance
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "CloseInstance", Description = "Close Instance",
                Win = _settings.KeyCloseInstance_Win, Ctrl = _settings.KeyCloseInstance_Ctrl, Alt = _settings.KeyCloseInstance_Alt, Shift = _settings.KeyCloseInstance_Shift, VkCode = _settings.KeyCloseInstance
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "AdjustUp", Description = "Increase Timer (+1m)",
                Win = _settings.KeyAdjustUp_Win, Ctrl = _settings.KeyAdjustUp_Ctrl, Alt = _settings.KeyAdjustUp_Alt, Shift = _settings.KeyAdjustUp_Shift, VkCode = _settings.KeyAdjustUp
            });
            _items.Add(new ShortcutRowItem
            {
                KeyName = "AdjustDown", Description = "Decrease Timer (-1m)",
                Win = _settings.KeyAdjustDown_Win, Ctrl = _settings.KeyAdjustDown_Ctrl, Alt = _settings.KeyAdjustDown_Alt, Shift = _settings.KeyAdjustDown_Shift, VkCode = _settings.KeyAdjustDown
            });
        }

        private void BuildUI()
        {
            PnlShortcuts.Children.Clear();

            foreach (var item in _items)
            {
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

                // Description
                var txtDesc = new TextBlock
                {
                    Text = item.Description,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA0, 0xFF, 0xA0)),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txtDesc, 0);

                // Modifiers Checkboxes Panel
                var pnlChk = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                
                var chkCtrl = new CheckBox { Content = "Ctrl", IsChecked = item.Ctrl, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkWin = new CheckBox { Content = "Win", IsChecked = item.Win, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkAlt = new CheckBox { Content = "Alt", IsChecked = item.Alt, Style = (Style)Resources["CyberpunkCheckBox"] };
                var chkShift = new CheckBox { Content = "Shift", IsChecked = item.Shift, Style = (Style)Resources["CyberpunkCheckBox"] };

                item.ChkCtrl = chkCtrl;
                item.ChkWin = chkWin;
                item.ChkAlt = chkAlt;
                item.ChkShift = chkShift;

                pnlChk.Children.Add(chkCtrl);
                pnlChk.Children.Add(chkWin);
                pnlChk.Children.Add(chkAlt);
                pnlChk.Children.Add(chkShift);

                Grid.SetColumn(pnlChk, 1);

                // Key TextBox Input
                var txtKey = new TextBox
                {
                    Text = GetKeyName(item.VkCode),
                    Style = (Style)Resources["CyberpunkTextBox"],
                    IsReadOnly = true,
                    Cursor = Cursors.Hand,
                    Tag = item
                };
                item.TxtKey = txtKey;

                txtKey.PreviewKeyDown += (s, e) =>
                {
                    Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
                    if (key == Key.LWin || key == Key.RWin || key == Key.LeftCtrl || key == Key.RightCtrl ||
                        key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift)
                    {
                        return; // Ignore modifier standalone key presses
                    }

                    uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    item.VkCode = vk;
                    txtKey.Text = GetKeyName(vk);
                    e.Handled = true;
                };

                Grid.SetColumn(txtKey, 2);

                grid.Children.Add(txtDesc);
                grid.Children.Add(pnlChk);
                grid.Children.Add(txtKey);

                PnlShortcuts.Children.Add(grid);
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
                bool w = item.ChkWin.IsChecked == true;
                bool c = item.ChkCtrl.IsChecked == true;
                bool a = item.ChkAlt.IsChecked == true;
                bool s = item.ChkShift.IsChecked == true;
                uint vk = item.VkCode;

                switch (item.KeyName)
                {
                    case "ToggleHUD":
                        _settings.KeyToggleHUD_Win = w; _settings.KeyToggleHUD_Ctrl = c; _settings.KeyToggleHUD_Alt = a; _settings.KeyToggleHUD_Shift = s; _settings.KeyToggleHUD = vk;
                        break;
                    case "PauseToggle":
                        _settings.KeyPauseToggle_Win = w; _settings.KeyPauseToggle_Ctrl = c; _settings.KeyPauseToggle_Alt = a; _settings.KeyPauseToggle_Shift = s; _settings.KeyPauseToggle = vk;
                        break;
                    case "Reset":
                        _settings.KeyReset_Win = w; _settings.KeyReset_Ctrl = c; _settings.KeyReset_Alt = a; _settings.KeyReset_Shift = s; _settings.KeyReset = vk;
                        break;
                    case "SaveCountdown":
                        _settings.KeySaveCountdown_Win = w; _settings.KeySaveCountdown_Ctrl = c; _settings.KeySaveCountdown_Alt = a; _settings.KeySaveCountdown_Shift = s; _settings.KeySaveCountdown = vk;
                        break;
                    case "SwitchMode":
                        _settings.KeySwitchMode_Win = w; _settings.KeySwitchMode_Ctrl = c; _settings.KeySwitchMode_Alt = a; _settings.KeySwitchMode_Shift = s; _settings.KeySwitchMode = vk;
                        break;
                    case "NewInstance":
                        _settings.KeyNewInstance_Win = w; _settings.KeyNewInstance_Ctrl = c; _settings.KeyNewInstance_Alt = a; _settings.KeyNewInstance_Shift = s; _settings.KeyNewInstance = vk;
                        break;
                    case "CloseInstance":
                        _settings.KeyCloseInstance_Win = w; _settings.KeyCloseInstance_Ctrl = c; _settings.KeyCloseInstance_Alt = a; _settings.KeyCloseInstance_Shift = s; _settings.KeyCloseInstance = vk;
                        break;
                    case "AdjustUp":
                        _settings.KeyAdjustUp_Win = w; _settings.KeyAdjustUp_Ctrl = c; _settings.KeyAdjustUp_Alt = a; _settings.KeyAdjustUp_Shift = s; _settings.KeyAdjustUp = vk;
                        break;
                    case "AdjustDown":
                        _settings.KeyAdjustDown_Win = w; _settings.KeyAdjustDown_Ctrl = c; _settings.KeyAdjustDown_Alt = a; _settings.KeyAdjustDown_Shift = s; _settings.KeyAdjustDown = vk;
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
