using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using Track = QAMP.Models.Track;

namespace QAMP
{
    public partial class MainWindow
    {
        private static string FormatTime(double seconds)
        {
            if (seconds <= 0) return "0:00";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss")
                : ts.ToString(@"m\:ss");
        }

        private void TogglePlayPause()
        {
            if (Player.CurrentTrack == null)
            {
                if (MusicLibrary.Instance.PlaybackQueue.Count > 0)
                {
                    var track = MusicLibrary.Instance.PlaybackQueue[0];
                    App.LogInfo($"PlayTrack (Toggle): {track.Executor} - {track.Name}");
                    _ = Player.PlayTrack(track);
                    UpdateNextTrackUI();
                }
            }
            else if (Player.IsPlaying)
            {
                App.LogInfo($"Pause: {Player.CurrentTrack?.Executor} - {Player.CurrentTrack?.Name}");
                _ = Player.PauseAsync();
            }
            else
            {
                App.LogInfo($"Resume: {Player.CurrentTrack?.Executor} - {Player.CurrentTrack?.Name}");
                Player.Resume();
            }
            UpdateOSD();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void PlayPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            if (Library.CurrentPlaylist == null || Library.CurrentPlaylist.Tracks.Count == 0)
            {
                _ = NotificationWindow.Show("Плейлист пуст!", this);
                return;
            }

            if (Player.IsPlaying)
            {
                _ = Player.PauseAsync();
                UpdatePlayPauseIcon(false);
                UpdateOSD();
                return;
            }

            // Проверяем, если музыка воспроизводится из другого плейлиста, 
            // не переключаемся - просто воспроизводим паузированную музыку
            if (Player.CurrentTrack != null && Library.PlayingPlaylist != null &&
                Library.PlayingPlaylist.Id != Library.CurrentPlaylist.Id)
            {
                // Воспроизводим текущий трек из другого плейлиста
                Player.Resume();
                UpdatePlayPauseIcon(true);
                UpdateOSD();
                return;
            }

            if (Player.CurrentTrack != null && Library.CurrentPlaylist.Tracks.Contains(Player.CurrentTrack))
            {
                Player.Resume();
                UpdatePlayPauseIcon(true);
                UpdateOSD();
                return;
            }

            // Устанавливаем текущий плейлист как плейлист для воспроизведения
            Library.PlayingPlaylist = Library.CurrentPlaylist;

            var firstTrack = Library.CurrentPlaylist.Tracks[0];
            App.LogInfo($"PlayPlaylist: {Library.CurrentPlaylist.Name} | Track: {firstTrack.Executor} - {firstTrack.Name}");
            _ = Player.PlayTrack(firstTrack, true);

            Library.PlaybackQueue.Clear();
            foreach (var track in Library.CurrentPlaylist.Tracks)
            {
                Library.PlaybackQueue.Add(track);
            }
            if (_playService.IsShuffleEnabled)
            {
                var shuffledList = Library.PlaybackQueue.ToList();
                if (firstTrack != null && shuffledList.Contains(firstTrack))
                {
                    _ = shuffledList.Remove(firstTrack);
                }
                shuffledList = [.. shuffledList.OrderBy(x => Guid.NewGuid())];
                if (firstTrack != null)
                {
                    shuffledList.Insert(0, firstTrack);
                }
                _playService.ShuffledQueue = shuffledList;
            }
            UpdatePlayPauseIcon(true);
            UpdateNextTrackUI();
            UpdateOSD();
        }

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            var geometry = isPlaying
        ? (Geometry)Application.Current.Resources["pauseGeometry"]
        : (Geometry)Application.Current.Resources["playGeometry"];

            PlayPauseIcon.Data = geometry;

            PlayPauseIcon1.Data = geometry;
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что у нас есть текущий трек
            if (Player.CurrentTrack == null)
            {
                _ = NotificationWindow.Show("Нет текущего трека", this);
                return;
            }

            if (_playService.IsShuffleEnabled)
            {
                int currentIndex = _playService.ShuffledQueue.IndexOf(Player.CurrentTrack);

                if (currentIndex > 0)
                {
                    var prevTrack = _playService.ShuffledQueue[currentIndex - 1];
                    App.LogInfo($"PrevTrack (Shuffle): {prevTrack.Executor} - {prevTrack.Name}");
                    _ = Player.PlayTrack(prevTrack);
                }
                else if (currentIndex == 0 && _playService.RepeatMode == RepeatMode.RepeatAll)
                {
                    var prevTrack = _playService.ShuffledQueue[_playService.ShuffledQueue.Count - 1];
                    App.LogInfo($"PrevTrack (Shuffle, wrap): {prevTrack.Executor} - {prevTrack.Name}");
                    _ = Player.PlayTrack(prevTrack);
                }
                else if (currentIndex == -1)
                {
                    var remainingTracks = MusicLibrary.Instance.PlaybackQueue
                        .Where(t => t != Player.CurrentTrack)
                        .OrderBy(x => Guid.NewGuid())
                        .ToList();

                    _playService.ShuffledQueue = [Player.CurrentTrack, .. remainingTracks];

                    if (_playService.RepeatMode == RepeatMode.RepeatAll && _playService.ShuffledQueue.Count > 1)
                    {
                        var prevTrack = _playService.ShuffledQueue[_playService.ShuffledQueue.Count - 1];
                        App.LogInfo($"PrevTrack (Shuffle, not in queue): {prevTrack.Executor} - {prevTrack.Name}");
                        _ = Player.PlayTrack(prevTrack);
                    }
                }

                UpdateNextTrackUI();
            }
            else
            {
                // Обычный режим (без shuffle)
                if (MusicLibrary.Instance.PlaybackQueue.Count == 0)
                {
                    _ = NotificationWindow.Show("Плейлист пуст", this);
                    return;
                }

                var currentIndex = MusicLibrary.Instance.PlaybackQueue.IndexOf(Player.CurrentTrack);

                // КРИТИЧНАЯ ПРОВЕРКА: currentIndex должен быть валидным
                if (currentIndex > 0)
                {
                    var track = MusicLibrary.Instance.PlaybackQueue[currentIndex - 1];
                    App.LogInfo($"PrevTrack: {track.Executor} - {track.Name}");
                    _ = Player.PlayTrack(track);
                    UpdateNextTrackUI();
                }
                else if (currentIndex == -1)
                {
                    // Трека нет в очереди
                    if (_playService.RepeatMode == RepeatMode.RepeatAll && MusicLibrary.Instance.PlaybackQueue.Count > 0)
                    {
                        var track = MusicLibrary.Instance.PlaybackQueue[MusicLibrary.Instance.PlaybackQueue.Count - 1];
                        App.LogInfo($"PrevTrack (wrap): {track.Executor} - {track.Name}");
                        _ = Player.PlayTrack(track);
                        UpdateNextTrackUI();
                    }
                    else
                    {
                        _ = NotificationWindow.Show("Это первый трек в плейлисте", this);
                    }
                }
                else if (currentIndex == 0)
                {
                    // Это первый трек
                    if (_playService.RepeatMode == RepeatMode.RepeatAll && MusicLibrary.Instance.PlaybackQueue.Count > 0)
                    {
                        var track = MusicLibrary.Instance.PlaybackQueue[MusicLibrary.Instance.PlaybackQueue.Count - 1];
                        App.LogInfo($"PrevTrack (wrap, at first): {track.Executor} - {track.Name}");
                        _ = Player.PlayTrack(track);
                        UpdateNextTrackUI();
                    }
                    else
                    {
                        _ = NotificationWindow.Show("Это первый трек в плейлисте", this);
                    }
                }
            }

            if (_isLyricsMode)
            {
                UpdateLyricsView();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            Player.PlayNextTrack();

            UpdateNextTrackUI();
            if (_isLyricsMode) UpdateLyricsView();
        }

        public void UpdateNextTrackUI(Track? currentTrack = null)
        {
            if (Player.RepeatMode == RepeatMode.RepeatOne)
            {
                var current = Player.CurrentTrack;
                if (current != null)
                {
                    NextTrackName.Text = "Повтор текущего трека";
                }
            }
            else
            {
                var next = _playService.GetNextTrack();

                if (next != null)
                {
                    NextTrackName.Text = $"{next.Executor} - {next.Name}";
                }
                else
                {
                    NextTrackName.Text = "Плейлист закончился";
                }
            }
        }

        private void ShuffleButton_Click(object? sender, RoutedEventArgs? e)
        {
            _playService.IsShuffleEnabled = !_playService.IsShuffleEnabled;

            if (_playService.IsShuffleEnabled)
            {
                var shuffledList = MusicLibrary.Instance.PlaybackQueue.ToList();

                Track? currentTrack = Player.CurrentTrack;
                if (currentTrack != null && shuffledList.Contains(currentTrack))
                {
                    _ = shuffledList.Remove(currentTrack);
                }

                shuffledList = [.. shuffledList.OrderBy(x => Guid.NewGuid())];

                if (currentTrack != null)
                {
                    shuffledList.Insert(0, currentTrack);
                }

                _playService.ShuffledQueue = shuffledList;

                // Обновляем обе иконки (верхняя и нижняя панель)
                if (ShuffleImage != null)
                {
                    ShuffleImage.Data = (Geometry)Application.Current.Resources["shuffle_OnGeometry"];
                    ShuffleImage.Fill = (Brush)Application.Current.Resources["AccentBrush"];
                }
                if (ShuffleImage1 != null)
                {
                    ShuffleImage1.Data = (Geometry)Application.Current.Resources["shuffle_OnGeometry"];
                    ShuffleImage1.Fill = (Brush)Application.Current.Resources["AccentBrush"];
                }
                UpdateNextTrackUI();
            }
            else
            {
                _playService.ShuffledQueue.Clear();
                // Обновляем обе иконки (верхняя и нижняя панель)
                if (ShuffleImage != null)
                {
                    ShuffleImage.Data = (Geometry)Application.Current.Resources["shuffleGeometry"];
                    ShuffleImage.Fill = (Brush)Application.Current.Resources["AccentBrush"];
                }
                if (ShuffleImage1 != null)
                {
                    ShuffleImage1.Data = (Geometry)Application.Current.Resources["shuffleGeometry"];
                    ShuffleImage1.Fill = (Brush)Application.Current.Resources["AccentBrush"];
                }
                UpdateNextTrackUI();
            }
        }

        private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isSliderDragging = true;
        }

        private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isSliderDragging = false;
            if (Player.CurrentTrack != null)
            {
                var newPosition = ProgressSlider.Value / 100 * Player.Duration;
                Player.Seek(newPosition);
            }
        }

        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Slider slider) return;

            Point point = e.GetPosition(slider);
            double relativePosition = point.X / slider.ActualWidth;
            double newValue = slider.Minimum + (relativePosition * (slider.Maximum - slider.Minimum));
            slider.Value = newValue;
        }

        private void SortButton_Click(object sender, RoutedEventArgs e) // Обработчик кнопки сортировки
        {
            if (Library.CurrentPlaylist == null)
            {
                _ = NotificationWindow.Show("Сначала выберите плейлист!", this);
                return;
            }
            var contextMenu = new ContextMenu();

            // По дате добавления
            var menuItemDate = new MenuItem { Header = "По дате добавления" };
            menuItemDate.Click += (s, args) => ApplySort(TrackSortType.AddedDate);
            _ = contextMenu.Items.Add(menuItemDate);

            // По альбому 
            var menuItemAlbum = new MenuItem { Header = "По альбому (A-Z)" };
            menuItemAlbum.Click += (s, args) => ApplySort(TrackSortType.AlbumAZ);
            _ = contextMenu.Items.Add(menuItemAlbum);

            // По исполнителю 
            var menuItemExecutor = new MenuItem { Header = "По исполнителю (A-Z)" };
            menuItemExecutor.Click += (s, args) => ApplySort(TrackSortType.ExecutorAZ);
            _ = contextMenu.Items.Add(menuItemExecutor);

            //По названию 
            var menuItemName = new MenuItem { Header = "По названию (A-Z)" };
            menuItemName.Click += (s, args) => ApplySort(TrackSortType.NameAZ);
            _ = contextMenu.Items.Add(menuItemName);

            if (sender is Button button)
            {
                contextMenu.PlacementTarget = button;
                contextMenu.Placement = PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }

        private void ApplySort(TrackSortType sortType)
        {
            if (Library.CurrentPlaylist == null) return;

            // Сохраняем тип сортировки в плейлисте
            Library.CurrentPlaylist.SortType = sortType;

            // Сохраняем в БД
            DatabaseService.UpdatePlaylistSortType(Library.CurrentPlaylist.Id, sortType);

            // Применяем сортировку к трекам
            var sortedTracks = SortTracks([.. Library.CurrentPlaylist.Tracks], sortType);

            // ВАЖНО: НЕ изменяем саму коллекцию Tracks!
            // Вместо этого переустанавливаем ItemsSource на отсортированные треки для отображения в DataGrid
            TracksDataGrid.ItemsSource = null;
            TracksDataGrid.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<Track>(sortedTracks);

            var brushColor = sortType == TrackSortType.AddedDate
                ? ((Brush)Application.Current.Resources["DisabledBrush"] ?? Brushes.Gray)
                : (Brush)Application.Current.Resources["AccentBrush"];

            // if (sortImage != null)
            //     sortImage.Fill = brushColor;
            // if (sortImage1 != null)
            //     sortImage1.Fill = brushColor;

            string sortName = sortType switch
            {
                TrackSortType.AddedDate => "по дате добавления",
                TrackSortType.AlbumAZ => "по альбому (A-Z)",
                TrackSortType.ExecutorAZ => "по исполнителю (A-Z)",
                TrackSortType.NameAZ => "по названию (A-Z)",
                _ => "неизвестно"
            };
            _ = NotificationWindow.Show($"Плейлист отсортирован {sortName}", this);
        }

        /// <summary>
        /// Сортирует список треков по выбранному критерию
        /// </summary>
        private static List<Track> SortTracks(List<Track> tracks, TrackSortType sortType)
        {
            return sortType switch
            {
                TrackSortType.AddedDate => [.. tracks.OrderBy(t => t.AddedDate)],
                // Сначала по альбому, потом по номеру трека в альбоме
                TrackSortType.AlbumAZ => [.. tracks
                    .OrderBy(t => t.Album ?? "")
                    .ThenBy(t => t.TrackNumber)],
                // Сначала по исполнителю, потом по альбому, потом по номеру трека
                TrackSortType.ExecutorAZ => [.. tracks
                    .OrderBy(t => t.Executor ?? "")
                    .ThenBy(t => t.Album ?? "")
                    .ThenBy(t => t.TrackNumber)],
                TrackSortType.NameAZ => [.. tracks.OrderBy(t => t.Name ?? "")],
                _ => tracks
            };
        }
    }
}
