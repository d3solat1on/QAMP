using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QAMP.Dialogs;
using QAMP.Models;

namespace QAMP.Windows
{
    public partial class Settings
    {
        private void LoadCurrentHotkeys()
        {
            var config = SettingsManager.Instance.Config;
            if (config?.Hotkeys == null) return;

            UpdateHotkeyTextBox(TbKeyPlayPause, HotkeyAction.TogglePlayPause);
            UpdateHotkeyTextBox(TbKeyNextTrack, HotkeyAction.NextTrack);
            UpdateHotkeyTextBox(TbKeyPrevTrack, HotkeyAction.PreviousTrack);
            UpdateHotkeyTextBox(TbLyricsMode, HotkeyAction.ViewLyrics);
            UpdateHotkeyTextBox(TbFavoriteRemoveAdd, HotkeyAction.ToggleFavorite);
            UpdateHotkeyTextBox(TbTrackInfo, HotkeyAction.ShowTrackInfo);
            UpdateHotkeyTextBox(TbRepeatTrack, HotkeyAction.ToggleRepeat);
            UpdateHotkeyTextBox(TbShuffleTrack, HotkeyAction.ToggleShuffle);
            UpdateHotkeyTextBox(Tb5secForward, HotkeyAction.SeekForward);
            UpdateHotkeyTextBox(Tb5secback, HotkeyAction.SeekBackward);
            UpdateHotkeyTextBox(TbFullSpectr, HotkeyAction.OpenFullScreenSpectrum);
            UpdateHotkeyTextBox(TbOpenConsole, HotkeyAction.OpenConsole);
        }

        private static void UpdateHotkeyTextBox(TextBox textBox, HotkeyAction action)
        {
            var config = SettingsManager.Instance.Config;
            var hotkey = config.Hotkeys.FirstOrDefault(h => h.Action == action);

            if (hotkey != null)
            {
                string modifiersText = hotkey.Modifiers != ModifierKeys.None ? $"{hotkey.Modifiers} + " : "";
                textBox.Text = $"{modifiersText}{hotkey.Key}";
            }
            else
            {
                string message = (string)Application.Current.FindResource("LngNotAssigned");
                textBox.Text = $"{message}";
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not TextBox currentTextBox) return;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            Key pressedKey = (e.Key == Key.System) ? e.SystemKey : e.Key;
            ModifierKeys modifiers = Keyboard.Modifiers;

            HotkeyAction targetAction;
            if (currentTextBox == TbKeyPlayPause) targetAction = HotkeyAction.TogglePlayPause;
            else if (currentTextBox == TbKeyNextTrack) targetAction = HotkeyAction.NextTrack;
            else if (currentTextBox == TbKeyPrevTrack) targetAction = HotkeyAction.PreviousTrack;
            else if (currentTextBox == TbLyricsMode) targetAction = HotkeyAction.ViewLyrics;
            else if (currentTextBox == TbFavoriteRemoveAdd) targetAction = HotkeyAction.ToggleFavorite;
            else if (currentTextBox == TbTrackInfo) targetAction = HotkeyAction.ShowTrackInfo;
            else if (currentTextBox == TbRepeatTrack) targetAction = HotkeyAction.ToggleRepeat;
            else if (currentTextBox == TbShuffleTrack) targetAction = HotkeyAction.ToggleShuffle;
            else if (currentTextBox == Tb5secForward) targetAction = HotkeyAction.SeekForward;
            else if (currentTextBox == Tb5secback) targetAction = HotkeyAction.SeekBackward;
            else if (currentTextBox == TbFullSpectr) targetAction = HotkeyAction.OpenFullScreenSpectrum;
            else if (currentTextBox == TbOpenConsole) targetAction = HotkeyAction.OpenConsole;
            else return;

            var config = SettingsManager.Instance.Config;
            var duplicate = config.Hotkeys.FirstOrDefault(h =>
                h.Key == pressedKey && h.Modifiers == modifiers && h.Action != targetAction);

            if (duplicate != null)
            {
                string message = (string)Application.Current.FindResource("LngCombinationIsUsing");
                NotificationWindow.Show($"{message} {duplicate.Action}", this);
                e.Handled = true;
                return;
            }

            var hotkeyToUpdate = config.Hotkeys.FirstOrDefault(h => h.Action == targetAction);
            if (hotkeyToUpdate != null)
            {
                hotkeyToUpdate.Key = pressedKey;
                hotkeyToUpdate.Modifiers = modifiers;
            }
            else
            {
                config.Hotkeys.Add(new HotkeyItem(targetAction, pressedKey, modifiers));
            }

            SettingsManager.Instance.Save();
            UpdateHotkeyTextBox(currentTextBox, targetAction);
            e.Handled = true;
        }
    }
}
