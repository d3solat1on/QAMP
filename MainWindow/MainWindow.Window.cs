using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QAMP.Models;
using QAMP.Visualization;
using QAMP.Windows;

namespace QAMP
{
    public partial class MainWindow
    {
        private static readonly OSDWindow _osd = new();
        private SpectrumFullWindow? _spectrumFullWindow;

        private void FullSpectrumWindowButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSpectrumFullScreen();
        }
        
        private void OpenSpectrumFullScreen()
        {
            if (_spectrumFullWindow != null)
            {
                _spectrumFullWindow.Activate();
                return;
            }

            _spectrumFullWindow = new SpectrumFullWindow
            {
                Owner = this
            };
            _spectrumFullWindow.Closed += (sender, args) =>
            {
                _spectrumFullWindow = null;
            };
            _spectrumFullWindow.Show();
        }

        public static void UpdateOSD()
        {
            if (Player.CurrentTrack != null)
            {
                string executor = Player.CurrentTrack.Executor ?? "Unknown Artist";
                string name = Player.CurrentTrack.Name ?? "Unknown Title";
                _osd.ShowOSD(executor, name);
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (QueuePanel.Visibility == Visibility.Visible)
            {
                Point mousePos = e.GetPosition(QueuePanel);
                bool isOverQueue = mousePos.X >= 0 && mousePos.X <= QueuePanel.ActualWidth &&
                                   mousePos.Y >= 0 && mousePos.Y <= QueuePanel.ActualHeight;

                if (!isOverQueue)
                {
                    QueuePanel.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                }
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var config = SettingsManager.Instance.Config;
            if (config?.Hotkeys == null) return;

            Key pressedKey = (e.Key == Key.System) ? e.SystemKey : e.Key;

            var matchedHotkey = config.Hotkeys.FirstOrDefault(h => h.Key == pressedKey && h.Modifiers == Keyboard.Modifiers);

            if (matchedHotkey == null) return;

            if (matchedHotkey.Action == HotkeyAction.TogglePlayPause)
            {
                var focusedElement = FocusManager.GetFocusedElement(this);
                if (focusedElement is TextBox || focusedElement is PasswordBox || focusedElement is RichTextBox)
                {
                    return;
                }
            }

            if (Player.CurrentTrack != null || matchedHotkey.Action == HotkeyAction.ToggleFocusGrid)
            {
                ExecuteHotkeyAction(matchedHotkey.Action);
                e.Handled = true;
            }
        }
        
    }
}