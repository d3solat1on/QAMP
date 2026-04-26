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
            UpdatePlayPauseIconState();
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

            // Приоритет 1: Если музыка воспроизводится из ДРУГОГО плейлиста,
            // начинаем воспроизводить выбранный плейлист
            if (Player.CurrentTrack != null && Library.PlayingPlaylist != null &&
                Library.PlayingPlaylist.Id != Library.CurrentPlaylist.Id)
            {
                StartPlayingPlaylist("from different");
                return;
            }

            // Приоритет 2: Если что-то воспроизводится из ТЕКУЩЕГО плейлиста, паузируем
            if (Player.IsPlaying)
            {
                _ = Player.PauseAsync();
                UpdatePlayPauseIconState();
                UpdateOSD();
                return;
            }

            // Приоритет 3: Если текущий трек есть в текущем плейлисте, возобновляем
            if (Player.CurrentTrack != null && Library.CurrentPlaylist.Tracks.Contains(Player.CurrentTrack))
            {
                Player.Resume();
                UpdatePlayPauseIconState();
                UpdateOSD();
                return;
            }

            // Приоритет 4: Начинаем воспроизведение плейлиста с начала
            StartPlayingPlaylist("");
        }

        private void StartPlayingPlaylist(string logSuffix)
        {
            // Устанавливаем текущий плейлист как плейлист для воспроизведения
            Library.PlayingPlaylist = Library.CurrentPlaylist;

            var firstTrack = Library.CurrentPlaylist.Tracks[0];
            var logMsg = string.IsNullOrEmpty(logSuffix)
                ? $"PlayPlaylist: {Library.CurrentPlaylist.Name} | Track: {firstTrack.Executor} - {firstTrack.Name}"
                : $"PlayPlaylist ({logSuffix}): {Library.CurrentPlaylist.Name} | Track: {firstTrack.Executor} - {firstTrack.Name}";
            App.LogInfo(logMsg);
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
            UpdatePlayPauseIconState();
            UpdateNextTrackUI();
            UpdateOSD();
        }

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            // 1. Получаем контекст плейлистов
            var playingPlaylist = MusicLibrary.Instance.PlayingPlaylist;

            // 2. Логика для ГЛОБАЛЬНОЙ кнопки (нижняя панель)
            // Она зависит только от того, играет ли музыка в принципе
            var globalGeometry = isPlaying
                ? (Geometry)Application.Current.Resources["pauseGeometry"]
                : (Geometry)Application.Current.Resources["playGeometry"];

            // 3. Логика для КОНТЕКСТНОЙ кнопки (вверху плейлиста)
            // Она зависит и от состояния, и от того, тот ли это плейлист
            bool isCurrentPlaylistPlaying = isPlaying &&
                                            PlaylistsListBox.SelectedItem is Playlist displayedPlaylist &&
                                            playingPlaylist != null &&
                                            displayedPlaylist.Id == playingPlaylist.Id;

            var contextGeometry = isCurrentPlaylistPlaying
                ? (Geometry)Application.Current.Resources["pauseGeometry"]
                : (Geometry)Application.Current.Resources["playGeometry"];

            // 4. Распределяем иконки
            // Предположим, PlayPauseIcon1 — это нижняя панель, а PlayPauseIcon — верхняя
            PlayPauseIcon1.Data = contextGeometry; // Верхняя (контекстная)
            PlayPauseIcon.Data = globalGeometry; // Нижняя (глобальная)

            // Принудительное обновление
            PlayPauseIcon.InvalidateVisual();
            PlayPauseIcon1.InvalidateVisual();
        }

        /// <summary>
        /// Обновляет иконку Play/Pause в зависимости от текущего состояния
        /// Показывает Pause только если текущий трек воспроизводится ИЗ выбранного плейлиста
        /// </summary>
        private void UpdatePlayPauseIconState()
        {
            bool hasTrack = Player.CurrentTrack != null;
            bool hasPlayingPlaylist = Library.PlayingPlaylist != null;
            // bool playlistMatches = hasPlayingPlaylist && Library.CurrentPlaylist != null && 
            //                       Library.PlayingPlaylist.Id == Library.CurrentPlaylist.Id;
            bool isPlaying = Player.IsPlaying;

            bool shouldShowPlaying = hasTrack && hasPlayingPlaylist && isPlaying;

            // System.Diagnostics.Debug.WriteLine($"[UpdatePlayPauseIconState] HasTrack={hasTrack}, HasPlaylist={hasPlayingPlaylist}, " +
            //     $"Match={playlistMatches} (Playing:{Library.PlayingPlaylist?.Name}={Library.CurrentPlaylist?.Name}), " +
            //     $"IsPlaying={isPlaying}, ShowPause={shouldShowPlaying}");

            UpdatePlayPauseIcon(shouldShowPlaying);
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что у нас есть текущий трек
            if (Player.CurrentTrack == null)
            {
                _ = NotificationWindow.Show("Нет трека для воспроизведения!", this);
                return;
            }
            Player.PlayPreviousTrack();

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
                var sourceQueue = _playService._actualPlayingQueue.ToList(); // Создаем копию, чтобы не изменять оригинальную очередь напрямую

                if (sourceQueue.Count == 0) return;

                Track? currentTrack = Player.CurrentTrack;

                var shuffledList = sourceQueue.OrderBy(x => Guid.NewGuid()).ToList();

                if (currentTrack != null)
                {
                    var existing = shuffledList.FirstOrDefault(t => t.Path == currentTrack.Path);
                    if (existing != null) shuffledList.Remove(existing);
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
            menuItemDate.Click += (s, args) => ApplySort(TrackSortType.AddedDate, true);
            _ = contextMenu.Items.Add(menuItemDate);

            // По альбому 
            var menuItemAlbum = new MenuItem { Header = "По альбому (A-Z)" };
            menuItemAlbum.Click += (s, args) => ApplySort(TrackSortType.AlbumAZ, true);
            _ = contextMenu.Items.Add(menuItemAlbum);

            // По исполнителю 
            var menuItemExecutor = new MenuItem { Header = "По исполнителю (A-Z)" };
            menuItemExecutor.Click += (s, args) => ApplySort(TrackSortType.ExecutorAZ, true);
            _ = contextMenu.Items.Add(menuItemExecutor);

            //По названию 
            var menuItemName = new MenuItem { Header = "По названию (A-Z)" };
            menuItemName.Click += (s, args) => ApplySort(TrackSortType.NameAZ, true);
            _ = contextMenu.Items.Add(menuItemName);

            if (sender is Button button)
            {
                contextMenu.PlacementTarget = button;
                contextMenu.Placement = PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }

        private void ApplySort(TrackSortType sortType, bool showNotification = false)
        {
            if (Library.CurrentPlaylist == null) return;

            Library.CurrentPlaylist.SortType = sortType;

            DatabaseService.UpdatePlaylistSortType(Library.CurrentPlaylist.Id, sortType);

            var sortedTracks = SortTracks([.. Library.CurrentPlaylist.Tracks], sortType);

            Library.CurrentPlaylist.Tracks.Clear();
            foreach (var track in sortedTracks)
            {
                Library.CurrentPlaylist.Tracks.Add(track);
            }

            TracksDataGrid.ItemsSource = null;
            TracksDataGrid.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<Track>(sortedTracks);

            // Player.UpdateQueueOrder(sortedTracks);
            if (showNotification)
            {
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
