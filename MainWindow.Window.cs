using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using QAMP.Audio;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.Windows;
using static QAMP.Dialogs.NotificationWindow;

namespace QAMP
{
    public partial class MainWindow
    {
        private bool _isClosing = false;
        private static readonly OSDWindow _osd = new();
        private List<LrcLine> _parsedLyrics = [];
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
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
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
                            _playService.PlayNextTrack();
                            e.Handled = true;
                        break;
                    case Key.B:
                            _playService.PlayPreviousTrack();
                            e.Handled = true;
                        break;
                    case Key.L:
                        ViewLyricsButton_Click(null, null);
                        e.Handled = true;
                        break;
                    case Key.I:
                        if (Player.CurrentTrack != null)
                        {
                            var infoWindow = new ShowTrackInfo(Player.CurrentTrack)
                            {
                                Owner = this
                            };
                            infoWindow.ShowDialog();
                            e.Handled = true;
                        }
                        break;
                    case Key.R:
                        if (Player.CurrentTrack != null)
                        {
                            RepeatButton_Click(null, null);
                            e.Handled = true;
                        }
                        break;
                    case Key.S:
                        if (Player.CurrentTrack != null)
                        {
                            ShuffleButton_Click(null, null);
                            e.Handled = true;
                        }
                        break;
                    case Key.Tab:
                        if (PlaylistsListBox.IsFocused || PlaylistsListBox.IsKeyboardFocusWithin)
                        {
                            TracksDataGrid.Focus();

                            if (TracksDataGrid.SelectedItem == null && TracksDataGrid.Items.Count > 0)
                            {
                                TracksDataGrid.SelectedIndex = 0;
                            }
                        }
                        else
                        {
                            PlaylistsListBox.Focus();

                            if (PlaylistsListBox.SelectedItem == null && PlaylistsListBox.Items.Count > 0)
                            {
                                PlaylistsListBox.SelectedIndex = 0;
                            }
                        }
                        e.Handled = true;
                        break;
                    case Key.F:
                        FavoriteButton_Click(null, null);
                        e.Handled = true;
                        break;
                }
            }
        }

        protected void TracksDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var currentPlaylist = Library.CurrentPlaylist;
                if (currentPlaylist == null) return;

                var tracksToDelete = TracksDataGrid.SelectedItems.Cast<Track>().ToList();

                if (tracksToDelete.Count == 0) return;
                var result = NotificationWindow.Show("Вы уверены, что хотите удалить выбранные треки?", this, NotificationMode.Confirm);
                if (result == true)
                {
                    foreach (var track in tracksToDelete)
                    {
                        DatabaseService.RemoveTrackFromPlaylist(currentPlaylist.Id, track.Id);
                        currentPlaylist.Tracks.Remove(track);
                    }
                }
                e.Handled = true;
            }
        }
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
            // 1. Если мы УЖЕ в процессе полного закрытия (вызванного из меню "Выход"), 
            // не мешаем процессу.
            if (_isClosing) return;

            try
            {
                var config = SettingsManager.Instance.Config;

                // 2. ПРОВЕРКА ТРЕЯ: Если настройка включена — отменяем закрытие окна
                if (config != null && config.CloseToTray)
                {
                    e.Cancel = true; // ГОВОРИМ WINDOWS: НЕ ЗАКРЫВАЙ ОКНО
                    this.Hide();     // Просто скрываем его с глаз
                    App.LogInfo("OnClosing: App hidden to tray instead of closing.");
                    return;          // ВАЖНО: выходим из метода здесь
                }

                // --- ДАЛЕЕ ЛОГИКА ПОЛНОГО ВЫХОДА (если CloseToTray = false) ---
                _isClosing = true;
                App.LogInfo("=== OnClosing FULL EXIT START ===");

                _playService.Dispose();

                // Сохранение данных
                var volumeStr = _playService.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture);
                DatabaseService.SaveSettingSync("Volume", volumeStr);

                if (_playService.CurrentTrack != null)
                {
                    DatabaseService.SaveSettingSync("LastTrackPath", _playService.CurrentTrack.Path ?? "");
                    DatabaseService.SaveSettingSync("LastTrackPosition", _playService.Position.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                SettingsManager.Instance.Save();

                // Чистим иконку, раз уж выходим совсем
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }

                App.LogInfo("=== OnClosing FULL EXIT END ===");
            }
            catch (Exception ex)
            {
                App.LogException(ex, "OnClosing Error");
            }
            finally
            {
                // Если мы не отменили закрытие (CloseToTray был false), 
                // завершаем процесс полностью.
                if (!e.Cancel)
                {
                    base.OnClosing(e);
                    Environment.Exit(0);
                }
            }
        }

        private void SetupTrayIcon()
        {
            var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/icon/QAMP_icon.ico")).Stream;
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(iconStream),
                Visible = true,
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
                _isClosing = true;
                this.Close();
            });
        }

        private void ViewLyricsButton_Click(object? sender, RoutedEventArgs? e)
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

                    string lyricsToDisplay = string.IsNullOrEmpty(track.Lyrics)
                        ? "Текст для этого трека отсутствует или не загружен."
                        : track.Lyrics;

                    _parsedLyrics = ParseLrc(lyricsToDisplay);

                    if (_parsedLyrics.Count > 0)
                    {
                        LyricsListBox.ItemsSource = _parsedLyrics;
                    }
                    else
                    {
                        LyricsListBox.ItemsSource = new List<LrcLine>
                {
                    new() { Text = lyricsToDisplay, IsActive = true }
                };
                    }
                }

                TracksDataGrid.Visibility = Visibility.Collapsed;
                PlaylistsListBox.Visibility = Visibility.Collapsed;
                LeftZona.Visibility = Visibility.Collapsed;
                ControlsPanel.Visibility = Visibility.Collapsed;
                UpperPanel.Visibility = Visibility.Collapsed;
                LyricsOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                TracksDataGrid.Visibility = Visibility.Visible;
                PlaylistsListBox.Visibility = Visibility.Visible;
                LeftZona.Visibility = Visibility.Visible;
                ControlsPanel.Visibility = Visibility.Visible;
                UpperPanel.Visibility = Visibility.Visible;
                LyricsOverlay.Visibility = Visibility.Collapsed;
            }
        }
        private static List<LrcLine> ParseLrc(string lrcText)
        {
            var lines = new List<LrcLine>();
            if (string.IsNullOrEmpty(lrcText)) return lines;

            var regex = MyRegex();

            foreach (var line in lrcText.Split('\n'))
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    if (TimeSpan.TryParse("00:" + match.Groups["time"].Value.Replace(".", ","), out TimeSpan time))
                    {
                        lines.Add(new LrcLine { Time = time, Text = match.Groups["text"].Value.Trim() });
                    }
                }
            }
            return [.. lines.OrderBy(l => l.Time)];
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

            _parsedLyrics = ParseLrc(finalLyrics);

            if (_parsedLyrics.Count > 0)
            {
                LyricsListBox.ItemsSource = _parsedLyrics;
            }
            else
            {
                var fallbackLine = new LrcLine
                {
                    Text = string.IsNullOrEmpty(finalLyrics) ? "Текст не найден." : finalLyrics,
                    IsActive = true
                };
                LyricsListBox.ItemsSource = new List<LrcLine> { fallbackLine };
            }

            track.Lyrics = finalLyrics;
            if (LyricsListBox.Items.Count > 0)
                LyricsListBox.ScrollIntoView(LyricsListBox.Items[0]);
        }
        private void UpdateLyricsHighlight(TimeSpan currentTime)
        {
            if (_parsedLyrics == null || _parsedLyrics.Count == 0) return;

            LrcLine? currentLine = null;
            foreach (var line in _parsedLyrics)
            {
                if (line.Time <= currentTime)
                    currentLine = line;
                else
                    break;
            }

            if (currentLine != null && !currentLine.IsActive)
            {
                foreach (var l in _parsedLyrics) l.IsActive = false;
                currentLine.IsActive = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LyricsListBox.ScrollIntoView(currentLine);
                    LyricsListBox.UpdateLayout();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
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

        [System.Text.RegularExpressions.GeneratedRegex(@"\[(?<time>\d{2}:\d{2}\.\d{2,3})\](?<text>.*)")]
        private static partial System.Text.RegularExpressions.Regex MyRegex();
    }
}