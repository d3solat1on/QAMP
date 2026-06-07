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
using QAMP.Visualization;
using QAMP.Windows;
using static QAMP.Dialogs.NotificationWindow;

namespace QAMP
{
    public partial class MainWindow
    {
        private bool _isClosing = false;
        private static readonly OSDWindow _osd = new();
        private SpectrumFullWindow? _spectrumFullWindow;
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var focusedElement = FocusManager.GetFocusedElement(this);
                bool isTextInput = focusedElement is TextBox || focusedElement is PasswordBox || focusedElement is RichTextBox;
                if (!isTextInput)
                {
                    TogglePlayPause();
                    e.Handled = true;
                }
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Right:
                        if (Player.CurrentTrack != null)
                        {
                            _playService.SeekRelative(5);
                            e.Handled = true;
                        }
                        break;
                    case Key.Left:
                        if (Player.CurrentTrack != null)
                        {
                            _playService.SeekRelative(-5);
                            e.Handled = true;
                        }
                        break;
                    case Key.Up:
                        if (Player.CurrentTrack != null)
                        {
                            _playService.Volume += 0.05;
                            e.Handled = true;
                        }
                        break;
                    case Key.Down:
                        if (Player.CurrentTrack != null)
                        {
                            _playService.Volume -= 0.05;
                            e.Handled = true;
                        }
                        break;
                    case Key.N:
                        if (Player.CurrentTrack != null)
                        {
                            _playService.PlayNextTrack();
                            e.Handled = true;
                        }
                        break;
                    case Key.B:
                        if (Player.CurrentTrack != null)
                        {
                            _playService.PlayPreviousTrack();
                            e.Handled = true;
                        }
                        break;
                    case Key.L:
                        if (Player.CurrentTrack != null)
                        {
                            ViewLyricsButton_Click(null, null);
                            e.Handled = true;
                        }
                        break;
                    case Key.I:
                        if (Player.CurrentTrack != null)
                        {
                            var fullInfo = TagReader.GetFullTrackInfo(Player.CurrentTrack.Path);
                            if (fullInfo != null)
                            {
                                fullInfo.PlayCount = Player.CurrentTrack.PlayCount;
                                var infoWindow = new ShowTrackInfo(fullInfo)
                                {
                                    Owner = this
                                };
                                infoWindow.Show();
                            }
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
                    case Key.W:
                        if (Player.CurrentTrack != null)
                        {
                            OpenSpectrumFullScreen();
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
                        if (Player.CurrentTrack != null)
                        {
                            FavoriteButton_Click(null, null);
                            e.Handled = true;
                        }
                        break;
                }
            }
        }

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

        protected void TracksDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var currentPlaylist = Library.CurrentPlaylist;
                if (currentPlaylist == null) return;

                var tracksToDelete = TracksDataGrid.SelectedItems.Cast<Track>().ToList();
                if (tracksToDelete.Count == 0) return;

                string confirmMessage = Application.Current.FindResource("LngDeleteTracksConfirm") as string
                                        ?? "Вы уверены, что хотите удалить выбранные треки?";

                var result = NotificationWindow.Show(confirmMessage, this, NotificationMode.Confirm);
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
                        // Текст с таймкодами (LRC формат)
                        LyricsListBox.ItemsSource = _parsedLyrics;
                    }
                    else
                    {
                        // Текст без таймкодов - разбиваем на отдельные строки для навигации
                        _parsedLyrics = CreatePlainTextLines(lyricsToDisplay);
                        LyricsListBox.ItemsSource = _parsedLyrics;
                    }
                }

                TracksDataGrid.Visibility = Visibility.Collapsed;
                PlaylistsListBox.Visibility = Visibility.Collapsed;
                LeftZona.Visibility = Visibility.Collapsed;
                ControlsPanel.Visibility = Visibility.Collapsed;
                UpperPanel.Visibility = Visibility.Collapsed;
                LyricsOverlay.Visibility = Visibility.Visible;

                // Установляем фокус на ListBox и выбираем первый элемент для навигации
                LyricsListBox.SelectedIndex = 0;
                LyricsListBox.Focus();
                LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                // Cursor = Cursors.None; // Скрываем курсор в режиме отображения текста
            }
            else
            {
                TracksDataGrid.Visibility = Visibility.Visible;
                PlaylistsListBox.Visibility = Visibility.Visible;
                LeftZona.Visibility = Visibility.Visible;
                ControlsPanel.Visibility = Visibility.Visible;
                UpperPanel.Visibility = Visibility.Visible;
                LyricsOverlay.Visibility = Visibility.Collapsed;
                // Cursor = Cursors.Arrow; // Восстанавливаем курсор
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

        /// <summary>
        /// Создает список LrcLine для текста без таймкодов, разбивая по строкам
        /// </summary>
        private static List<LrcLine> CreatePlainTextLines(string text)
        {
            var lines = new List<LrcLine>();
            if (string.IsNullOrEmpty(text)) return lines;

            foreach (var line in text.Split('\n'))
            {
                var trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    lines.Add(new LrcLine { Text = trimmedLine, IsActive = false });
                }
            }

            // Помечаем первую строку как активную
            if (lines.Count > 0)
                lines[0].IsActive = true;

            return lines;
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
                // Текст с таймкодами (LRC формат)
                LyricsListBox.ItemsSource = _parsedLyrics;
            }
            else
            {
                // Текст без таймкодов - разбиваем на отдельные строки для навигации
                string textToDisplay = string.IsNullOrEmpty(finalLyrics) ? "Текст не найден." : finalLyrics;
                _parsedLyrics = CreatePlainTextLines(textToDisplay);
                LyricsListBox.ItemsSource = _parsedLyrics;
            }

            track.Lyrics = finalLyrics;
            if (LyricsListBox.Items.Count > 0)
                LyricsListBox.ScrollIntoView(LyricsListBox.Items[0]);
        }
        private void UpdateLyricsHighlight(TimeSpan currentTime)
        {
            // Проверяем как _parsedLyrics, так и LyricsListBox.Items для поддержки обоих режимов
            if ((LyricsListBox.Items.Count == 0) || _parsedLyrics == null || _parsedLyrics.Count == 0)
                return;

            // Проверяем, есть ли таймкоды (если все строки имеют Time == 0, это обычный текст без таймкодов)
            bool hasTimeCodes = _parsedLyrics.Any(l => l.Time > TimeSpan.Zero);

            // Если текст без таймкодов, не обновляем IsActive - пользователь сам навигирует по клавиатуре
            if (!hasTimeCodes)
                return;

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

        private void LyricsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Обработка скролла по клавиатуре в режиме LyricsOverlay
            if (LyricsOverlay.Visibility != Visibility.Visible)
                return;

            int currentIndex = LyricsListBox.SelectedIndex;
            int itemCount = LyricsListBox.Items.Count;

            switch (e.Key)
            {
                case Key.Up:
                    if (currentIndex > 0)
                    {
                        LyricsListBox.SelectedIndex = currentIndex - 1;
                        LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    if (currentIndex < itemCount - 1)
                    {
                        LyricsListBox.SelectedIndex = currentIndex + 1;
                        LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.PageUp:
                    // Скролл на 5 строк вверх
                    int newIndexUp = Math.Max(0, currentIndex - 5);
                    LyricsListBox.SelectedIndex = newIndexUp;
                    LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                    e.Handled = true;
                    break;

                case Key.PageDown:
                    // Скролл на 5 строк вниз
                    int newIndexDown = Math.Min(itemCount - 1, currentIndex + 5);
                    LyricsListBox.SelectedIndex = newIndexDown;
                    LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                    e.Handled = true;
                    break;

                case Key.Home:
                    // В начало текста
                    LyricsListBox.SelectedIndex = 0;
                    LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                    e.Handled = true;
                    break;

                case Key.End:
                    // В конец текста
                    LyricsListBox.SelectedIndex = itemCount - 1;
                    LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                    e.Handled = true;
                    break;
            }
        }

        private void LyricsListBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Обработка скролла колесом мыши в режиме LyricsOverlay
            if (LyricsOverlay.Visibility != Visibility.Visible)
                return;

            int currentIndex = LyricsListBox.SelectedIndex;
            int itemCount = LyricsListBox.Items.Count;

            // Колесо вверх = отрицательное значение Delta
            if (e.Delta > 0)
            {
                // Скролл вверх
                if (currentIndex > 0)
                {
                    LyricsListBox.SelectedIndex = currentIndex - 1;
                    LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                }
            }
            else
            {
                // Скролл вниз
                if (currentIndex < itemCount - 1)
                {
                    LyricsListBox.SelectedIndex = currentIndex + 1;
                    LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                }
            }

            e.Handled = true;
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