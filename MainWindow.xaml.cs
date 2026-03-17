using Microsoft.Win32;
using MusicPlayer_by_d3solat1on.Dialogs;
using MusicPlayer_by_d3solat1on.Models;
using MusicPlayer_by_d3solat1on.Services;
using MusicPlayer_by_d3solat1on.ViewModels;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static MusicPlayer_by_d3solat1on.Dialogs.NotificationWindow;
using static MusicPlayer_by_d3solat1on.Services.PlayerService;
using Track = MusicPlayer_by_d3solat1on.Models.Track;

namespace MusicPlayer_by_d3solat1on
{
    public partial class MainWindow : Window
    {

        private readonly DispatcherTimer _memoryCleanupTimer;
        public static MusicLibrary Library => MusicLibrary.Instance;
        private static PlayerService Player => Instance;
        private bool _isSliderDragging = false;
        private double _lastVolume = 0.5;
        private Track _lastTrackWithCover;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Сначала загружаем данные (включая громкость)
            StorageService.Instance.LoadLibrary();

            // 2. Устанавливаем DataContext (теперь UI сам подхватит данные из MusicLibrary)
            DataContext = MusicLibrary.Instance;
            // TracksDataGrid.ItemsSource = Library.CurrentTracks;
            // PlaylistsListBox.ItemsSource = Library.Playlists;
            // Подписки на события плеера
            Player.TrackChanged += OnTrackChanged;
            Player.PositionChanged += OnPositionChanged;
            Player.PlaybackPaused += OnPlaybackPaused;
            Player.VolumeChanged += OnVolumeChanged;
            Player.DurationChanged += OnDurationChanged;

            // 3. Устанавливаем громкость СТРОГО после загрузки библиотеки
            double savedVolume = StorageService.Instance.Volume * 100;
            VolumeSlider.Value = savedVolume;

            if (VolumePercentage != null)
            {
                VolumePercentage.Text = $"{savedVolume:F0}%";
            }

            // Сохранение при закрытии
            Closed += (s, e) => StorageService.Instance.SaveLibrary();

            // Таймер очистки памяти (оставляем, раз он помогает держать 700МБ)
            _memoryCleanupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _memoryCleanupTimer.Tick += (s, e) =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            };
            _memoryCleanupTimer.Start();
        }

        private void OnDurationChanged()
        {
            Dispatcher.Invoke(() =>
            {
                TotalTimeText.Text = FormatTime(Player.Duration);
            });
        }
        private void OnVolumeChanged(double volume)
        {
            Dispatcher.Invoke(() =>
            {
                VolumeSlider.Value = volume * 100;
                VolumePercentage.Text = $"{volume * 100:F0}%";

            });
        }
        private void OnTrackChanged(Track track)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateNowPlayingInfo(track);
                UpdatePlayPauseIcon(true);
                UpdateFavoriteIcon(track);
                CurrentTrackName.Text = track.Name;
                CurrentTrackExecutor.Text = track.Executor;
                CurrentTrackAlbum.Text = track.Album;
                CurrentTrackData.Text = $"{track.Genre} | {track.Duration:mm\\:ss}";
                // Обновляем длительность
                string totalTime = "0:00";
                if (Player.Duration > 0)
                {
                    totalTime = FormatTime(Player.Duration);
                }
                else
                {
                    // Если длительность еще не загружена, показываем "загрузка"
                    totalTime = "загрузка...";

                    // Планируем повторную проверку через небольшие интервалы
                    CheckDurationAsync();
                }
                if (track != null)
                {
                    var favorites = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);
                    bool isFavorite = favorites?.Tracks.Any(t => t.Path == track.Path) ?? false;

                    FavoriteIcon.Source = new BitmapImage(new Uri(isFavorite
                        ? "pack://application:,,,/Resources/remove_favorites.png"
                        : "pack://application:,,,/Resources/add_favorites.png"));
                }
                UpdateCurrentTrackCover(track); // Добавить эту строку
                TotalTimeText.Text = totalTime;
            });
        }
        private async void CheckDurationAsync()
        {
            // Проверяем длительность несколько раз с задержкой
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(100);

                if (Player.Duration > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TotalTimeText.Text = FormatTime(Player.Duration);
                    });
                    break;
                }
            }
        }
        private void OnPositionChanged(double position)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnPositionChanged(position));
                return;
            }

            if (!_isSliderDragging)
            {
                if (Player.Duration > 0)
                {
                    double sliderValue = position / Player.Duration * 100;

                    // Проверка на корректность
                    if (!double.IsNaN(sliderValue) && !double.IsInfinity(sliderValue))
                    {
                        ProgressSlider.Value = sliderValue;
                    }
                }
                CurrentTimeText.Text = FormatTime(position);
            }
        }

        private void OnPlaybackPaused(bool isPaused)
        {
            Dispatcher.Invoke(() =>
            {
                UpdatePlayPauseIcon(!isPaused);
            });
        }

        private static string FormatTime(double seconds)
        {
            if (seconds <= 0) return "0:00";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss")
                : ts.ToString(@"m\:ss");
        }


        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Player.CurrentTrack == null)
            {
                if (Library.CurrentTracks.Count > 0)
                {
                    Player.PlayTrack(Library.CurrentTracks[0]);
                }
            }
            else if (Player.IsPlaying)
            {
                Player.Pause();
            }
            else
            {
                Player.Resume();
            }
        }

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            if (PlayPauseButton.Content is Image image)
            {
                if (isPlaying)
                {
                    image.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/pause.png"));
                }
                else
                {
                    image.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/play.png"));
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Player.Stop();
            PlayPauseButton.Content = "▶";
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "0:00";
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (Library.CurrentTracks.Count == 0) return;

            var currentIndex = Library.CurrentTracks.IndexOf(Player.CurrentTrack);
            if (currentIndex > 0)
            {
                Player.PlayTrack(Library.CurrentTracks[currentIndex - 1]);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {

            if (Library.CurrentTracks.Count == 0) return;

            var currentIndex = Library.CurrentTracks.IndexOf(Player.CurrentTrack);
            if (currentIndex < Library.CurrentTracks.Count - 1)
            {
                Player.PlayTrack(Library.CurrentTracks[currentIndex + 1]);
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


        private void AddMusicButton_Click(object sender, RoutedEventArgs e)
        {
            if (Library.CurrentPlaylist != null)
            {
                // Обновляем заголовки под текущий плейлист
                MenuAddFilesPlaylist.Header = $"Добавить файлы в \"{Library.CurrentPlaylist.Name}\"";
                MenuAddFolderPlaylist.Header = $"Добавить папку в \"{Library.CurrentPlaylist.Name}\"";

                MenuAddFilesPlaylist.Visibility = Visibility.Visible;
                MenuAddFolderPlaylist.Visibility = Visibility.Visible;
            }
            else
            {
                // Если плейлист не выбран, скрываем эти пункты
                MenuAddFilesPlaylist.Visibility = Visibility.Collapsed;
                MenuAddFolderPlaylist.Visibility = Visibility.Collapsed;
            }

            // Открываем меню
            AddMusicMenu.PlacementTarget = sender as Button;
            AddMusicMenu.IsOpen = true;
        }
        private void AddFilesToPlaylist_Click(object sender, RoutedEventArgs e) => AddFilesToCurrentPlaylist();
        private void AddFolderToPlaylist_Click(object sender, RoutedEventArgs e) => AddFolderToCurrentPlaylist();
        private static void AddFilesToCurrentPlaylist()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите музыкальные файлы для плейлиста",
                Filter = "Музыкальные файлы|*.mp3;*.wav;*.flac;*.m4a|Все файлы|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                Library.AddTracksToCurrentPlaylist(openFileDialog.FileNames);
            }
        }

        private static void AddFolderToCurrentPlaylist()
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Выберите папку с музыкой для плейлиста"
            };

            if (folderDialog.ShowDialog() == true)
            {
                Library.AddTracksFromFolderToCurrentPlaylist(folderDialog.FolderName);
            }
        }

        private static void AddFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите музыкальные файлы",
                Filter = "Музыкальные файлы|*.mp3;*.wav;*.flac;*.m4a|Все файлы|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                Library.AddTracks(openFileDialog.FileNames);
            }
        }

        private static void AddFolder()
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Выберите папку с музыкой"
            };

            if (folderDialog.ShowDialog() == true)
            {
                Library.AddTracksFromFolder(folderDialog.FolderName);
            }
        }


        private void CreatePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreatePlaylistDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                Library.CreatePlaylist(dialog.PlaylistName, dialog.PlaylistDescription, dialog.PlaylistCoverImage);
            }
        }


        private void PlaylistsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                Library.CurrentPlaylist = selectedPlaylist;
            }
        }


        private void TracksDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                Player.PlayTrack(selectedTrack);
            }
        }

        private void UpdateNowPlayingInfo(Track track)
        {
            if (_lastTrackWithCover != null && _lastTrackWithCover != track)
            {
                _lastTrackWithCover.UnloadCover();
            }
            LastTrackName.Text = track.Name;
            LastTrackExecutor.Text = track.Executor;
            CurrentTrackName.Text = track.Name;
            CurrentTrackExecutor.Text = track.Executor;
            CurrentTrackAlbum.Text = track.Album;
            CurrentTrackData.Text = $"{track.ExtensionDisplay}, {track.SampleRateDisplay}, {track.BitrateDisplay}, {track.AlbumDisplay}, {track.Genre} JOINT STEREO";

            Library.LastPlayedTrack = track;
            _lastTrackWithCover = track;
            MusicLibrary.Instance.UpdatePlaylistView();
        }

        private void RemoveFromPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (Library.CurrentPlaylist == null)
            {
                MessageBox.Show("Сначала выберите плейлист", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                var result = MessageBox.Show(
                    $"Удалить трек \"{selectedTrack.Name}\" из плейлиста \"{Library.CurrentPlaylist.Name}\"?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Library.CurrentPlaylist.Tracks.Remove(selectedTrack);
                }
            }
        }

        private void RemoveFromLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                var result = MessageBox.Show(
                    $"Удалить трек \"{selectedTrack.Name}\" из библиотеки?\n" +
                    "Это удалит его из всех плейлистов!",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {

                    foreach (var playlist in Library.Playlists)
                    {
                        playlist.Tracks.Remove(selectedTrack);
                    }


                    Library.AllTracks.Remove(selectedTrack);
                }
            }
        }
        private void RemovePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                // Вызываем твое кастомное окно
                string message = $"Удалить плейлист \"{selectedPlaylist.Name}\"?\n" +
                                 "Треки в плейлисте останутся в библиотеке.";

                if (NotificationWindow.Show($"Удалить плейлист \"{selectedPlaylist.Name}\"?", this, NotificationMode.Confirm) == true)
                {
                    Library.Playlists.Remove(selectedPlaylist);
                    // Если нужно, вызываем обновление интерфейса
                    MusicLibrary.Instance.UpdatePlaylistView();

                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Теперь все элементы точно загружены
            if (VolumePercentage != null)
            {
                VolumePercentage.Text = $"{VolumeSlider.Value:F0}%";
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Player != null)
            {
                double volume = VolumeSlider.Value / 100.0;
                Player.Volume = volume;

                // Обновляем иконку в зависимости от громкости
                if (VolumeSlider.Value == 0)
                {
                    VolumeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/volume_off.png"));
                }
                else
                {
                    VolumeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/volume.png"));
                }

                if (VolumePercentage != null)
                {
                    VolumePercentage.Text = $"{VolumeSlider.Value:F0}%";
                }
            }
        }
        private void VolumeButton_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Переключение mute/unmute
            if (VolumeSlider.Value > 0)
            {
                _lastVolume = VolumeSlider.Value;
                VolumeSlider.Value = 0;
            }
            else
            {
                VolumeSlider.Value = _lastVolume;
            }
        }
        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            if (Library.CurrentTracks == null || Library.CurrentTracks.Count < 2) return;

            // Используем алгоритм Тасования Фишера — Йетса
            Random rng = new();
            var list = Library.CurrentTracks.ToList();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }

            // Очищаем текущий список и заливаем перемешанный
            Library.CurrentTracks.Clear();
            foreach (var track in list)
            {
                Library.CurrentTracks.Add(track);
            }

            // Визуальный фидбек — кнопка становится ярче
            // ShuffleButton.Opacity = (ShuffleButton.Opacity == 1.0) ? 0.5 : 1.0;
        }
        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            // Циклическое переключение: NoRepeat -> RepeatAll -> RepeatOne -> NoRepeat
            switch (Player.RepeatMode)
            {
                case RepeatMode.NoRepeat:
                    Player.RepeatMode = RepeatMode.RepeatAll;
                    // RepeatButton.Content = "🔁"; // Можно подсветить кнопку цветом
                    // RepeatButton.Opacity = 1.0;
                    break;
                case RepeatMode.RepeatAll:
                    Player.RepeatMode = RepeatMode.RepeatOne;
                    // RepeatButton.Content = "🔂"; // Иконка повтора одного трека
                    // RepeatButton.Opacity = 1.0;
                    break;
                case RepeatMode.RepeatOne:
                    Player.RepeatMode = RepeatMode.NoRepeat;
                    // RepeatButton.Content = "🔁";
                    // RepeatButton.Opacity = 0.5; // Делаем полупрозрачной, когда выключено
                    break;
            }
        }
        private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                // Вызываем твое кастомное окно
                string message = $"Удалить плейлист \"{selectedPlaylist.Name}\"?\n" +
                                 "Треки в плейлисте останутся в библиотеке.";

                if (NotificationWindow.Show($"Удалить плейлист \"{selectedPlaylist.Name}\"?", this, NotificationMode.Confirm) == true)
                {
                    Library.Playlists.Remove(selectedPlaylist);
                    // Если нужно, вызываем обновление интерфейса
                    MusicLibrary.Instance.UpdatePlaylistView();

                }
            }
        }
        private void EditPlaylist_Click(object sender, RoutedEventArgs e)
        {
            // Получаем плейлист, на котором кликнули
            if (sender is MenuItem menuItem && menuItem.DataContext is Playlist selectedPlaylist)
            {
                // Создаем окно редактирования
                var dialog = new EditPlaylistDialog
                {
                    Owner = this,
                    DataContext = selectedPlaylist // Передаем данные в диалог
                };

                if (dialog.ShowDialog() == true)
                {
                    // Обновляем UI или сохраняем изменения
                    MusicLibrary.Instance.UpdatePlaylistView();
                }
            }
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Player.CurrentTrack == null)
            {
                NotificationWindow.Show("Нет трека для добавления в Избранное", this);
                return;
            }

            var favoritePlaylist = Library.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);

            // Если плейлиста "Избранное" ещё нет, создаём его
            if (favoritePlaylist == null)
            {
                byte[]? favCover = null;
                try
                {
                    // Пытаемся загрузить обложку для плейлиста "Избранное"
                    var uri = new Uri("pack://application:,,,/Resources/favorite_cover.png");
                    var resourceStream = Application.GetResourceStream(uri);
                    if (resourceStream != null)
                    {
                        using var ms = new System.IO.MemoryStream();
                        resourceStream.Stream.CopyTo(ms);
                        favCover = ms.ToArray();
                    }
                }
                catch { /* Если нет обложки - игнорируем */ }

                favoritePlaylist = Library.CreatePlaylist(
                    MusicLibrary.FavoritesName,
                    "Ваши любимые треки",
                    favCover
                );

                // CreatePlaylist уже добавляет плейлист в коллекцию
                // Если нет - раскомментируйте:
                // Library.Playlists.Insert(0, favoritePlaylist);
            }

            // Проверяем, есть ли трек в избранном
            if (!favoritePlaylist.Tracks.Contains(Player.CurrentTrack))
            {
                // Добавляем трек
                favoritePlaylist.Tracks.Add(Player.CurrentTrack);

                // Меняем иконку на "в избранном"
                UpdateFavoriteIcon(Player.CurrentTrack, true);

                NotificationWindow.Show("Трек успешно добавлен в Избранное", this);
            }
            else
            {
                NotificationWindow.Show("Трек успешно удален из Избранного", this);
                favoritePlaylist.Tracks.Remove(Player.CurrentTrack);
                UpdateFavoriteIcon(Player.CurrentTrack, false);
            }
        }
        private void UpdateFavoriteIcon(Track track, bool? forceState = null)
        {
            if (FavoriteIcon == null) return;

            bool isFavorite;

            if (forceState.HasValue)
            {
                isFavorite = forceState.Value;
            }
            else if (track != null)
            {
                var favoritePlaylist = Library.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);
                isFavorite = favoritePlaylist?.Tracks.Contains(track) ?? false;
            }
            else
            {
                isFavorite = false;
            }

            string iconPath = isFavorite
                ? "/Resources/favorites_added.png"   // В избранном
                : "/Resources/add_favorites.png";     // Не в избранном

            FavoriteIcon.Source = new BitmapImage(new Uri(iconPath, UriKind.RelativeOrAbsolute));
            FavoriteButton.ToolTip = isFavorite
                ? "Удалить из избранного"
                : "Добавить в избранное";
        }

        private void UpdateCurrentTrackCover(Track track)
        {
            try
            {
                if (track?.CoverImage != null && track.CoverImage.Length > 0)
                {
                    using var ms = new System.IO.MemoryStream(track.CoverImage);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    CurrentTrackImage.Source = bitmap;
                }
                else
                {
                    // Пытаемся загрузить из ресурсов заглушку
                    var uri = new Uri("pack://application:,,,/Resources/default_cover.png");
                    CurrentTrackImage.Source = new BitmapImage(uri);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки обложки: {ex.Message}");
                CurrentTrackImage.Source = null;
            }
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // Сохраняем всё при закрытии
            StorageService.Instance.SaveLibrary();
        }


    }
}