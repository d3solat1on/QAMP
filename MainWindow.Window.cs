using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
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
        private string? _currentLyricsFilePath = null;
        private LrcLine? _lastHighlightedLine = null;
        private bool _hasTimeCodes = false;
        private Point _queueDragStartPoint;
        private Track? _queueDraggedTrack;
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

        private void ExecuteHotkeyAction(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.TogglePlayPause:
                    TogglePlayPause();
                    break;
                case HotkeyAction.SeekForward:
                    _playService.SeekRelative(5);
                    break;
                case HotkeyAction.SeekBackward:
                    _playService.SeekRelative(-5);
                    break;
                case HotkeyAction.VolumeUp:
                    _playService.Volume += 0.05;
                    break;
                case HotkeyAction.VolumeDown:
                    _playService.Volume -= 0.05;
                    break;
                case HotkeyAction.NextTrack:
                    _playService.PlayNextTrack();
                    break;
                case HotkeyAction.PreviousTrack:
                    _playService.PlayPreviousTrack();
                    break;
                case HotkeyAction.ViewLyrics:
                    ViewLyricsButton_Click(null, null);
                    break;
                case HotkeyAction.ShowTrackInfo:
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
                    break;
                case HotkeyAction.ToggleRepeat:
                    RepeatButton_Click(null, null);
                    break;
                case HotkeyAction.ToggleShuffle:
                    ShuffleButton_Click(null, null);
                    break;
                case HotkeyAction.OpenFullScreenSpectrum:
                    OpenSpectrumFullScreen();
                    break;
                case HotkeyAction.ToggleFocusGrid:
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
                    break;
                case HotkeyAction.ToggleFavorite:
                    FavoriteButton_Click(null, null);
                    break;
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
                string executor = Player.CurrentTrack.Executor ?? "Unknown Artist";
                string name = Player.CurrentTrack.Name ?? "Unknown Title";
                _osd.ShowOSD(executor, name);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isClosing) return;

            try
            {
                var config = SettingsManager.Instance.Config;

                if (config != null && config.CloseToTray)
                {
                    e.Cancel = true;
                    Hide();
                    MemoryOptimizer.RunAsync(Dispatcher);
                    return;
                }

                _isClosing = true;

                _playService.Dispose();
                LyricsCache.Clear();
                CoverImageCacheService.ClearMemoryCache();

                var volumeStr = _playService.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture);
                DatabaseService.SaveSettingSync("Volume", volumeStr);

                if (_playService.CurrentTrack != null)
                {
                    DatabaseService.SaveSettingSync("LastTrackPath", _playService.CurrentTrack.Path ?? "");
                    DatabaseService.SaveSettingSync("LastTrackPosition", _playService.Position.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                SettingsManager.Instance.Save();

            }
            catch (Exception ex)
            {
                App.LogException(ex, "OnClosing");
            }
            finally
            {
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
                Player.PositionChanged += OnPositionChangedForLyrics;
                UpdateLyricsView();

                TracksDataGrid.Visibility = Visibility.Collapsed;
                PlaylistsListBox.Visibility = Visibility.Collapsed;
                ControlsPanel.Visibility = Visibility.Collapsed;
                UpperPanel.Visibility = Visibility.Collapsed;
                LibraryTextBlock.Visibility = Visibility.Collapsed;
                SortByButton.Visibility = Visibility.Collapsed;
                CreatePlaylistButton.Visibility = Visibility.Collapsed;
                RemovePlaylistButton.Visibility = Visibility.Collapsed;
                AddMusicButton.Visibility = Visibility.Collapsed;

                LyricsOverlay.Visibility = Visibility.Visible;

                if (_parsedLyrics != null && _parsedLyrics.Count > 0)
                {
                    foreach (var line in _parsedLyrics)
                        line.IsActive = false;

                    _lastHighlightedLine = null;

                    if (_hasTimeCodes)
                    {
                        var currentTime = TimeSpan.FromSeconds(Player.Position);
                        int index = BinarySearchLrc(_parsedLyrics, currentTime);
                        if (index >= 0 && index < _parsedLyrics.Count)
                        {
                            var currentLine = _parsedLyrics[index];
                            currentLine.IsActive = true;
                            _lastHighlightedLine = currentLine;
                            LyricsListBox.ScrollIntoView(currentLine);
                        }
                    }
                    else
                    {
                        _parsedLyrics[0].IsActive = true;
                        _lastHighlightedLine = _parsedLyrics[0];
                    }
                }
            }
            else
            {
                Player.PositionChanged -= OnPositionChangedForLyrics;

                LyricsCache.Clear();

                if (_parsedLyrics != null)
                {
                    foreach (var line in _parsedLyrics)
                        line.IsActive = false;

                    _parsedLyrics.Clear();
                }

                _lastHighlightedLine = null;
                LyricsListBox.ItemsSource = null;

                LibraryTextBlock.Visibility = Visibility.Visible;
                SortByButton.Visibility = Visibility.Visible;
                CreatePlaylistButton.Visibility = Visibility.Visible;
                RemovePlaylistButton.Visibility = Visibility.Visible;
                AddMusicButton.Visibility = Visibility.Visible;
                TracksDataGrid.Visibility = Visibility.Visible;
                PlaylistsListBox.Visibility = Visibility.Visible;
                LeftZona.Visibility = Visibility.Visible;
                ControlsPanel.Visibility = Visibility.Visible;
                UpperPanel.Visibility = Visibility.Visible;
                LyricsOverlay.Visibility = Visibility.Collapsed;
            }
        }
        private void OnPositionChangedForLyrics(double position)
        {
            if (_isLyricsMode && _hasTimeCodes)
            {
                UpdateLyricsHighlight(TimeSpan.FromSeconds(position));
            }
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
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    lines.Add(new LrcLine { Text = trimmed });
                }
            }
            return lines;
        }

        public void UpdateLyricsView()
        {
            Track? track = Player.CurrentTrack;
            if (track == null || string.IsNullOrEmpty(track.Path))
            {
                return;
            }

            if (_currentLyricsFilePath == track.Path && _parsedLyrics != null && _parsedLyrics.Count > 0)
            {
                if (_isLyricsMode && _hasTimeCodes)
                {
                    var currentTime = TimeSpan.FromSeconds(Player.Position);
                    UpdateLyricsHighlight(currentTime);
                }
                return;
            }

            _currentLyricsFilePath = track.Path;

            string lyricsFromFile = GetLyricsFromFile(track.Path);
            string finalLyrics = !string.IsNullOrEmpty(lyricsFromFile) ? lyricsFromFile : track.Lyrics;

            _parsedLyrics = LyricsCache.GetParsedLyrics(track.Path, finalLyrics);

            _hasTimeCodes = _parsedLyrics != null && _parsedLyrics.Any(l => l.Time > TimeSpan.Zero);

            if (!_hasTimeCodes && (_parsedLyrics == null || _parsedLyrics.Count == 0))
            {
                string plainText = LyricsCache.GetPlainText(track.Path, finalLyrics);
                _parsedLyrics = CreatePlainTextLines(plainText);
            }

            if (LyricsListBox != null && LyricsListBox.ItemsSource != _parsedLyrics)
            {
                LyricsListBox.ItemsSource = _parsedLyrics;
            }

            track.Lyrics = finalLyrics;
            _lastHighlightedLine = null;

            if (_parsedLyrics != null)
            {
                foreach (var line in _parsedLyrics)
                    line.IsActive = false;
            }

            if (_isLyricsMode && _parsedLyrics != null && _parsedLyrics.Count > 0)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LyricsListBox?.ScrollIntoView(LyricsListBox.Items[0]);
                        LyricsListBox?.SelectedIndex = 0;
                    }), System.Windows.Threading.DispatcherPriority.Background);
                if (_hasTimeCodes)
                {
                    var currentTime = TimeSpan.FromSeconds(Player.Position);
                    int index = BinarySearchLrc(_parsedLyrics, currentTime);
                    if (index >= 0 && index < _parsedLyrics.Count)
                    {
                        var currentLine = _parsedLyrics[index];
                        currentLine.IsActive = true;
                        _lastHighlightedLine = currentLine;
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            LyricsListBox?.ScrollIntoView(currentLine);
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                else
                {
                    _parsedLyrics[0].IsActive = true;
                    _lastHighlightedLine = _parsedLyrics[0];
                }
            }
        }
        private static string GetLyricsFromFile(string filePath)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                return file.Tag.Lyrics ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        private void UpdateLyricsHighlight(TimeSpan currentTime)
        {
            if (_parsedLyrics == null || _parsedLyrics.Count == 0 || !_hasTimeCodes) return;

            int index = BinarySearchLrc(_parsedLyrics, currentTime);
            if (index < 0 || index >= _parsedLyrics.Count)
            {
                _lastHighlightedLine?.IsActive = false;
                _lastHighlightedLine = null;
                return;
            }

            var currentLine = _parsedLyrics[index];
            if (_lastHighlightedLine == currentLine) return;

            _lastHighlightedLine?.IsActive = false;

            currentLine.IsActive = true;
            _lastHighlightedLine = currentLine;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (LyricsListBox == null) return;

                if (LyricsListBox.ItemContainerGenerator.ContainerFromItem(currentLine) is ListBoxItem container)
                {
                    container.BringIntoView();

                    LyricsListBox.ScrollIntoView(currentLine);

                    container.UpdateLayout();

                    if (!IsLineFullyVisible(currentLine))
                    {
                        var scrollViewer = FindVisualChild<ScrollViewer>(LyricsListBox);
                        if (scrollViewer != null)
                        {
                            var itemPosition = container.TransformToAncestor(LyricsListBox).Transform(new Point(0, 0));
                            double targetOffset = itemPosition.Y - (scrollViewer.ViewportHeight / 2) + (container.ActualHeight / 2);
                            targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.ScrollableHeight));
                            scrollViewer.ScrollToVerticalOffset(targetOffset);
                        }
                    }
                }
                else
                {
                    LyricsListBox.ScrollIntoView(currentLine);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private bool IsLineFullyVisible(LrcLine line)
        {
            if (LyricsListBox == null || LyricsListBox.Items.Count == 0) return false;

            if (LyricsListBox.ItemContainerGenerator.ContainerFromItem(line) is not FrameworkElement container) return false;

            var rect = container.TransformToAncestor(LyricsListBox).TransformBounds(
                new Rect(0, 0, container.ActualWidth, container.ActualHeight));
            var listRect = new Rect(0, 0, LyricsListBox.ActualWidth, LyricsListBox.ActualHeight);

            return rect.Y >= listRect.Y && rect.Y + rect.Height <= listRect.Y + listRect.Height;
        }
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static int BinarySearchLrc(List<LrcLine> lines, TimeSpan time)
        {
            if (lines.Count == 0) return -1;
            if (time < lines[0].Time) return -1;

            int left = 0;
            int right = lines.Count - 1;
            int result = -1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (lines[mid].Time <= time)
                {
                    result = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return result;
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

        private void LrcLine_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && _hasTimeCodes)
            {
                if (item.DataContext is LrcLine clickedLine)
                {
                    double targetSeconds = clickedLine.Time.TotalSeconds;

                    if (targetSeconds >= 0)
                    {
                        Player.Seek(targetSeconds);
                        UpdateLyricsHighlight(clickedLine.Time);
                    }
                }
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

        private void TracksDataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (ViewModels.MusicLibrary.Instance.CurrentPlaylist != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void TracksDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (paths != null && paths.Length > 0)
                {
                    await ProcessDroppedPaths(paths);
                }
            }
        }

        public void RefreshSingleTrackInUI(Track updatedTrack)
        {
            if (updatedTrack == null || TracksDataGrid == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (TracksDataGrid.ItemsSource is System.Collections.IEnumerable items)
                {
                    foreach (var item in items)
                    {
                        if (item is Track t && t.Path == updatedTrack.Path)
                        {
                            t.Lyrics = updatedTrack.Lyrics;
                            t.Name = updatedTrack.Name;
                            t.Executor = updatedTrack.Executor;
                            t.Genre = updatedTrack.Genre;
                            t.Album = updatedTrack.Album;

                            if (TracksDataGrid.ItemContainerGenerator.ContainerFromItem(t) is FrameworkElement itemProperties)
                            {
                                TracksDataGrid.Items.Refresh();
                            }
                            break;
                        }
                    }
                }
            });
        }


        private void OpenQueue_Click(object sender, RoutedEventArgs e)
        {
            if (QueuePanel.Visibility == Visibility.Collapsed)
            {
                QueuePanel.Visibility = Visibility.Visible;

                if (MusicLibrary.Instance != null && _playService != null)
                {
                    MusicLibrary.Instance.PlaybackQueue.Clear();

                    var activeSource = _playService.IsShuffleEnabled
                        ? _playService.ShuffledQueue
                        : _playService._actualPlayingQueue;

                    foreach (var track in activeSource)
                    {
                        MusicLibrary.Instance.PlaybackQueue.Add(track);
                    }

                    QueueListBox.ItemsSource = MusicLibrary.Instance.PlaybackQueue;
                }
            }
            else
            {
                QueuePanel.Visibility = Visibility.Collapsed;
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

        private void RemoveFromQueue_Click(object sender, RoutedEventArgs e)
        {
            if (QueueListBox.SelectedItem is Track selectedTrack)
            {
                MusicLibrary.Instance.PlaybackQueue.Remove(selectedTrack);

                Player._actualPlayingQueue.Remove(selectedTrack);
                UpdateNextTrackUI();
            }
        }
        private void PlayFromQueue_Click(object sender, RoutedEventArgs e)
        {
            if (QueueListBox.SelectedItem is Track selectedTrack)
            {
                var playingPlaylist = MusicLibrary.Instance.PlayingPlaylist;

                if (playingPlaylist != null)
                {
                    var currentOrder = MusicLibrary.Instance.PlaybackQueue.ToList();

                    MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, playingPlaylist, currentOrder, bypassShuffle: true);
                }
                else
                {
                    if (MusicLibrary.Instance.CurrentPlaylist != null)
                    {
                        var currentPlaylist = MusicLibrary.Instance.CurrentPlaylist;
                        MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, currentPlaylist);
                    }
                }
                if (_isLyricsMode) UpdateLyricsView();
            }
        }
        private void QueueListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _queueDragStartPoint = e.GetPosition(null);
            if (ItemsControl.ContainerFromElement(QueueListBox, e.OriginalSource as DependencyObject) is ListBoxItem item)
            {
                _queueDraggedTrack = item.Content as Track;
            }
        }

        private void QueueListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _queueDraggedTrack != null)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _queueDragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(QueueListBox, new DataObject("QueueTrackItem", _queueDraggedTrack), DragDropEffects.Move);
                }
            }
        }

        private void QueueListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("QueueTrackItem"))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void QueueListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("QueueTrackItem"))
            {
                if (e.Data.GetData("QueueTrackItem") is not Track droppedTrack) return;

                if (ItemsControl.ContainerFromElement(QueueListBox, e.OriginalSource as DependencyObject) is not ListBoxItem item) return;

                if (item.Content is not Track targetTrack || droppedTrack.Path == targetTrack.Path) return;

                var uiQueue = MusicLibrary.Instance.PlaybackQueue;
                int oldIndex = uiQueue.IndexOf(droppedTrack);
                int newIndex = uiQueue.IndexOf(targetTrack);

                if (oldIndex >= 0 && newIndex >= 0)
                {
                    uiQueue.Move(oldIndex, newIndex);

                    Player._actualPlayingQueue.RemoveAt(oldIndex);
                    Player._actualPlayingQueue.Insert(newIndex, droppedTrack);
                }
                _queueDraggedTrack = null;
                e.Handled = true;
                UpdateNextTrackUI();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }
}