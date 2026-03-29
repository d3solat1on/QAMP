using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using QAMP.Audio;
using QAMP.Models;
using QAMP.Services;
using QAMP.Windows;

namespace QAMP
{
    public partial class MainWindow
    {
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(new HwndSourceHook(HwndHook));
            HotKeyManager.RegisterMediaKeys(this);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                switch (id)
                {
                    case 9000:
                        TogglePlayPause();
                        break;
                    case 9001:
                        _playService.PlayNextTrack();
                        break;
                    case 9002:
                        _playService.PlayPreviousTrack();
                        break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var focused = FocusManager.GetFocusedElement(this);
            bool isTextInput = focused is TextBox || focused is PasswordBox || focused is RichTextBox;

            switch (e.Key)
            {
                case Key.Space when !isTextInput:
                    TogglePlayPause();
                    e.Handled = true;
                    break;

                case Key.Right:
                    _playService.SeekRelative(5);
                    e.Handled = true;
                    break;
                case Key.Left:
                    _playService.SeekRelative(-5);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _playService.Volume += 0.05;
                    e.Handled = true;
                    break;
                case Key.Down:
                    _playService.Volume -= 0.05;
                    e.Handled = true;
                    break;
                case Key.N:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        _playService.PlayNextTrack();
                        e.Handled = true;
                    }
                    break;
                case Key.B:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        _playService.PlayPreviousTrack();
                        e.Handled = true;
                    }
                    break;    
            }
        }

        private static readonly OSDWindow _osd = new();

        public static void UpdateOSD()
        {
            if (Player.CurrentTrack != null)
            {
                string executor = Player.CurrentTrack.Executor ?? "Неизвестный исполнитель";
                string name = Player.CurrentTrack.Name ?? "Без названия";
                _osd.ShowOSD(executor, name);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
#pragma warning disable CA1416
            var config = SettingsManager.Instance.Config;
            string volumeStr = _playService.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture);
            DatabaseService.SaveSetting("Volume", volumeStr);
            if (config.CloseToTray)
            {
                e.Cancel = true;
                Hide();

                SettingsManager.Instance.Save();

                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = true;
                }
            }
            else
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                SettingsManager.Instance.Save();
                Application.Current.Shutdown();
            }
#pragma warning restore CA1416

            base.OnClosing(e);
        }

        private void SetupTrayIcon()
        {
#pragma warning disable CA1416
            var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/icon/QAMP_icon.ico")).Stream;
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(iconStream),
                Visible = false,
                Text = "QAMP Player"
            };

            _notifyIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };

            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Развернуть", null, (s, e) =>
            {
                Show();
                WindowState = WindowState.Maximized;
                Activate();
            });
            _notifyIcon.ContextMenuStrip.Items.Add("Играть/Пауза", null, (s, e) => TogglePlayPause());
            _notifyIcon.ContextMenuStrip.Items.Add("-");
            _notifyIcon.ContextMenuStrip.Items.Add("Выход", null, (s, e) =>
            {
                SettingsManager.Instance.Save();
                Application.Current.Shutdown();
            });
#pragma warning restore CA1416
        }

        private void ViewLyricsButton_Click(object sender, RoutedEventArgs e)
        {
            _isLyricsMode = !_isLyricsMode;
            UpdateInterfaceMode();
        }

        private void UpdateInterfaceMode()
        {
            if (_isLyricsMode)
            {
                var track = Player.CurrentTrack;
                if (track != null)
                {
                    if (string.IsNullOrEmpty(track.Lyrics))
                    {
                        try
                        {
                            using var file = TagLib.File.Create(track.Path);
                            track.Lyrics = file.Tag.Lyrics;
                        }
                        catch { }
                    }

                    BigLyricsText.Text = string.IsNullOrEmpty(track.Lyrics)
                        ? "Текст для этого трека отсутствует или не загружен."
                        : track.Lyrics;
                }

                TracksDataGrid.Visibility = Visibility.Collapsed;
                PlaylistsListBox.Visibility = Visibility.Collapsed;
                LeftZona.Visibility = Visibility.Collapsed;
                LyricsOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                TracksDataGrid.Visibility = Visibility.Visible;
                PlaylistsListBox.Visibility = Visibility.Visible;
                LeftZona.Visibility = Visibility.Visible;
                LyricsOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateLyricsView()
        {
            var track = Player.CurrentTrack;
            if (track == null) return;

            string lyricsFromFile = string.Empty;
            try
            {
                using var stream = new FileStream(track.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var fileAbstraction = new StreamFileAbstraction(track.Path, stream, stream);
                var tFile = TagLib.File.Create(fileAbstraction);
                lyricsFromFile = tFile.Tag.Lyrics;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка доступа к файлу: " + ex.Message);
            }

            string finalLyrics = !string.IsNullOrEmpty(lyricsFromFile) ? lyricsFromFile : track.Lyrics;

            BigLyricsText.Text = string.IsNullOrEmpty(finalLyrics)
                ? "Текст не найден в тегах файла."
                : finalLyrics;

            track.Lyrics = finalLyrics;

            LyricsScrollViewer?.ScrollToHome();
            UpdateNextTrackUI();
        }

        private void MainGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }
}