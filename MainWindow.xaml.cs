using Microsoft.Win32;
using Newtonsoft.Json;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static QAMP.Dialogs.NotificationWindow;
using static QAMP.Services.PlayerService;
using Track = QAMP.Models.Track;

namespace QAMP
{
    public partial class MainWindow : Window
    {
        private readonly PlayerService _playService = Instance;
        private readonly DispatcherTimer _memoryCleanupTimer;
        public static MusicLibrary Library => MusicLibrary.Instance;
        private static PlayerService Player => Instance;
        private bool _isSliderDragging = false;
        private double _lastVolume = 0.5;
        private Track? _lastTrackWithCover; // Исправлено: допускает null

        public MainWindow()
        {
            InitializeComponent();

            // Отладочная информация о пути к БД
            System.Diagnostics.Debug.WriteLine("=== ПУТЬ К БАЗЕ ДАННЫХ ===");
            System.Diagnostics.Debug.WriteLine($"Путь: {DatabaseService.DatabasePath}");
            System.Diagnostics.Debug.WriteLine($"Папка существует: {Directory.Exists(Path.GetDirectoryName(DatabaseService.DatabasePath))}");

            // Создаем БД если нужно
            DatabaseService.EnsureDatabaseCreated();

            // Проверяем, создался ли файл
            System.Diagnostics.Debug.WriteLine($"Файл БД существует: {File.Exists(DatabaseService.DatabasePath)}");

            LoadApplicationSettings();
            LoadPlaylists();
            DataContext = MusicLibrary.Instance;

            Player.TrackChanged += OnTrackChanged;
            Player.PositionChanged += OnPositionChanged;
            Player.PlaybackPaused += OnPlaybackPaused;
            Player.VolumeChanged += OnVolumeChanged;
            Player.DurationChanged += OnDurationChanged;
            _playService.TrackChanged += UpdateNextTrackUI;
            PlaylistsListBox.MouseDoubleClick += PlaylistsListBox_MouseDoubleClick;

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

            Closed += (s, e) => OnClosing((CancelEventArgs)e);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (VolumePercentage != null)
            {
                VolumePercentage.Text = $"{VolumeSlider.Value:F0}%";
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            DatabaseService.SaveSetting("Volume", _playService.Volume.ToString());
            base.OnClosing(e);
        }

        private void LoadApplicationSettings()
        {
            string savedVolume = DatabaseService.GetSetting("Volume", "0.5");
            if (double.TryParse(savedVolume, out double vol))
            {
                _playService.Volume = vol;
                VolumeSlider.Value = vol;
            }

            string lastPlaylistId = DatabaseService.GetSetting("LastPlaylistId", "-1");
            if (int.TryParse(lastPlaylistId, out int id) && id != -1)
            {
                var playlist = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == id);
                if (playlist != null)
                {
                    PlaylistsListBox.SelectedItem = playlist;
                }
            }
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

        private void OnTrackChanged(Track? track) // Исправлено: допускает null
        {
            if (track == null) return;

            Dispatcher.Invoke(() =>
            {
                UpdateNowPlayingInfo(track);
                UpdatePlayPauseIcon(true);
                UpdateFavoriteIcon(track);
                CurrentTrackName.Text = track.Name;
                CurrentTrackExecutor.Text = track.Executor;
                CurrentTrackAlbum.Text = track.Album;
                CurrentTrackData.Text = $"{track.Genre} | {track.Duration} | {track.SampleRate} Hz | {track.Bitrate} kbps";
                CurrentTrackExtension.Text = track.Extension;
                CurrentTrackYear.Text = track.Year > 0 ? track.Year.ToString() : "Неизвестно";

                string totalTime = "0:00";
                if (Player.Duration > 0)
                {
                    totalTime = FormatTime(Player.Duration);
                }
                else
                {
                    totalTime = "загрузка...";
                    CheckDurationAsync();
                }

                var favorites = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);
                bool isFavorite = favorites?.Tracks.Any(t => t.Path == track.Path) ?? false;

                FavoriteIcon.Source = new BitmapImage(new Uri(isFavorite
                    ? "pack://application:,,,/Resources/remove_favorites.png"
                    : "pack://application:,,,/Resources/add_favorites.png"));

                UpdateCurrentTrackCover(track);
                TotalTimeText.Text = totalTime;
            });
        }

        private async void CheckDurationAsync()
        {
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
                if (MusicLibrary.Instance.PlaybackQueue.Count > 0)
                {
                    Player.PlayTrack(MusicLibrary.Instance.PlaybackQueue[0]); // Исправлено: PlaybackQueue вместо CurrentTracks
                    UpdateNextTrackUI();
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

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playService.IsShuffleEnabled)
            {
                // Находим индекс текущего трека в ShuffledQueue
                int currentIndex = _playService.ShuffledQueue.IndexOf(Player.CurrentTrack);

                if (currentIndex > 0)
                {
                    // Есть предыдущий трек
                    var prevTrack = _playService.ShuffledQueue[currentIndex - 1];
                    Player.PlayTrack(prevTrack);
                }
                else if (currentIndex == 0 && _playService.RepeatMode == RepeatMode.RepeatAll)
                {
                    // Если включен повтор всех и мы в начале - переходим в конец
                    var prevTrack = _playService.ShuffledQueue[_playService.ShuffledQueue.Count - 1];
                    Player.PlayTrack(prevTrack);
                }
                else if (currentIndex == -1)
                {
                    // Трек не найден в ShuffledQueue, пересоздаем очередь
                    var remainingTracks = MusicLibrary.Instance.PlaybackQueue
                        .Where(t => t != Player.CurrentTrack)
                        .OrderBy(x => Guid.NewGuid())
                        .ToList();

                    _playService.ShuffledQueue = new List<Track> { Player.CurrentTrack };
                    _playService.ShuffledQueue.AddRange(remainingTracks);

                    // Нет предыдущего трека, так как текущий в начале
                    if (_playService.RepeatMode == RepeatMode.RepeatAll && _playService.ShuffledQueue.Count > 1)
                    {
                        var prevTrack = _playService.ShuffledQueue[_playService.ShuffledQueue.Count - 1];
                        Player.PlayTrack(prevTrack);
                    }
                }

                UpdateNextTrackUI();
            }
            else
            {
                // Обычный режим (без Shuffle)
                var currentIndex = MusicLibrary.Instance.PlaybackQueue.IndexOf(Player.CurrentTrack);
                if (currentIndex > 0)
                {
                    Player.PlayTrack(MusicLibrary.Instance.PlaybackQueue[currentIndex - 1]);
                    UpdateNextTrackUI();
                }
                else if (_playService.RepeatMode == RepeatMode.RepeatAll && MusicLibrary.Instance.PlaybackQueue.Count > 0)
                {
                    Player.PlayTrack(MusicLibrary.Instance.PlaybackQueue[MusicLibrary.Instance.PlaybackQueue.Count - 1]);
                    UpdateNextTrackUI();
                }
                else
                {
                    NotificationWindow.Show("Это первый трек в плейлисте", this);
                }
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playService.IsShuffleEnabled)
            {
                // Находим индекс текущего трека в ShuffledQueue
                int currentIndex = _playService.ShuffledQueue.IndexOf(Player.CurrentTrack);

                if (currentIndex != -1 && currentIndex < _playService.ShuffledQueue.Count - 1)
                {
                    // Есть следующий трек в очереди
                    var nextTrack = _playService.ShuffledQueue[currentIndex + 1];
                    Player.PlayTrack(nextTrack);
                }
                else if (currentIndex == _playService.ShuffledQueue.Count - 1)
                {
                    // Мы в конце очереди
                    if (_playService.RepeatMode == RepeatMode.RepeatAll)
                    {
                        // Если включен повтор всех - начинаем сначала
                        var nextTrack = _playService.ShuffledQueue[0];
                        Player.PlayTrack(nextTrack);
                    }
                    else
                    {
                        // Нет следующего трека
                        NotificationWindow.Show("Плейлист закончился", this);
                    }
                }
                else
                {
                    // Трек не найден в ShuffledQueue (например, после ручного выбора трека)
                    // Пересоздаем очередь с текущим треком
                    var remainingTracks = MusicLibrary.Instance.PlaybackQueue
                        .Where(t => t != Player.CurrentTrack)
                        .OrderBy(x => Guid.NewGuid())
                        .ToList();

                    _playService.ShuffledQueue = new List<Track> { Player.CurrentTrack };
                    _playService.ShuffledQueue.AddRange(remainingTracks);

                    // Если есть следующий трек
                    if (_playService.ShuffledQueue.Count > 1)
                    {
                        var nextTrack = _playService.ShuffledQueue[1];
                        Player.PlayTrack(nextTrack);
                    }
                }

                UpdateNextTrackUI();
            }
            else
            {
                // Обычный режим (без Shuffle)
                var currentIndex = MusicLibrary.Instance.PlaybackQueue.IndexOf(Player.CurrentTrack);
                if (currentIndex < MusicLibrary.Instance.PlaybackQueue.Count - 1)
                {
                    Player.PlayTrack(MusicLibrary.Instance.PlaybackQueue[currentIndex + 1]);
                    UpdateNextTrackUI();
                }
                else if (_playService.RepeatMode == RepeatMode.RepeatAll && MusicLibrary.Instance.PlaybackQueue.Count > 0)
                {
                    Player.PlayTrack(MusicLibrary.Instance.PlaybackQueue[0]);
                    UpdateNextTrackUI();
                }
                else
                {
                    NotificationWindow.Show("Плейлист закончился", this);
                }
            }
        }
        private void UpdateNextTrackUI(Track? currentTrack = null)
        {
            var next = _playService.GetNextTrack();

            if (next != null)
            {
                NextTrackName.Text = $"{next.Executor} - {next.Name}";
                NextTrackName.Foreground = Brushes.White;
            }
            else
            {
                NextTrackName.Text = "Плейлист закончился";
                NextTrackName.Foreground = Brushes.DimGray;
            }
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            _playService.IsShuffleEnabled = !_playService.IsShuffleEnabled;

            if (_playService.IsShuffleEnabled)
            {
                // Создаем перемешанную копию текущей очереди
                var shuffledList = MusicLibrary.Instance.PlaybackQueue.ToList();

                // Если есть текущий трек, удаляем его из списка для перемешивания
                Track? currentTrack = Player.CurrentTrack;
                if (currentTrack != null && shuffledList.Contains(currentTrack))
                {
                    shuffledList.Remove(currentTrack);
                }

                // Перемешиваем остальные треки
                shuffledList = shuffledList.OrderBy(x => Guid.NewGuid()).ToList();

                // Вставляем текущий трек в начало
                if (currentTrack != null)
                {
                    shuffledList.Insert(0, currentTrack);
                }

                _playService.ShuffledQueue = shuffledList;

                System.Diagnostics.Debug.WriteLine($"=== ВКЛЮЧЕН SHUFFLE ===");
                System.Diagnostics.Debug.WriteLine($"ShuffledQueue.Count: {_playService.ShuffledQueue.Count}");
                for (int i = 0; i < Math.Min(5, _playService.ShuffledQueue.Count); i++)
                {
                    System.Diagnostics.Debug.WriteLine($"  {i}: {_playService.ShuffledQueue[i].Name}");
                }

                ShuffleImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/shuffle_on.png"));
                UpdateNextTrackUI();
            }
            else
            {
                _playService.ShuffledQueue.Clear();
                ShuffleImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/shuffle.png"));
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

        private void AddMusicButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                MenuAddFilesPlaylist.Header = $"Добавить файлы в \"{selectedPlaylist.Name}\"";
                MenuAddFolderPlaylist.Header = $"Добавить папку в \"{selectedPlaylist.Name}\"";

                MenuAddFilesPlaylist.Visibility = Visibility.Visible;
                MenuAddFolderPlaylist.Visibility = Visibility.Visible;
            }
            else
            {
                MenuAddFilesPlaylist.Visibility = Visibility.Collapsed;
                MenuAddFolderPlaylist.Visibility = Visibility.Collapsed;
            }

            AddMusicMenu.PlacementTarget = sender as Button;
            AddMusicMenu.IsOpen = true;
        }

        private void AddFilesToPlaylist_Click(object sender, RoutedEventArgs e) => AddFilesToCurrentPlaylist();
        private void AddFolderToPlaylist_Click(object sender, RoutedEventArgs e) => AddFolderToCurrentPlaylist();

        private void AddFolderToCurrentPlaylist()
        {
            if (MusicLibrary.Instance.CurrentPlaylist == null)
            {
                NotificationWindow.Show("Сначала выберите плейлист!", this);
                return;
            }

            var folderDialog = new OpenFolderDialog
            {
                Title = "Выберите папку с музыкой (включая подпапки)"
            };

            if (folderDialog.ShowDialog() == true)
            {
                DatabaseService.AddFolderToPlaylist(MusicLibrary.Instance.CurrentPlaylist.Id, folderDialog.FolderName);

                // Обновляем плейлисты
                MusicLibrary.Instance.RefreshPlaylists();

                // Находим обновленный плейлист
                var updatedPlaylist = MusicLibrary.Instance.Playlists
                    .FirstOrDefault(p => p.Id == MusicLibrary.Instance.CurrentPlaylist.Id);

                if (updatedPlaylist != null)
                {
                    // Выбираем обновленный плейлист в списке
                    PlaylistsListBox.SelectedItem = updatedPlaylist;

                    // Если Shuffle включен, обновляем перемешанную очередь
                    if (_playService.IsShuffleEnabled)
                    {
                        // Создаем перемешанную копию
                        var shuffledList = updatedPlaylist.Tracks.OrderBy(x => Guid.NewGuid()).ToList();
                        _playService.ShuffledQueue = shuffledList;

                        // Если есть текущий трек, перемещаем его в начало
                        if (Player.CurrentTrack != null && _playService.ShuffledQueue.Contains(Player.CurrentTrack))
                        {
                            int currentIndex = _playService.ShuffledQueue.IndexOf(Player.CurrentTrack);
                            var current = _playService.ShuffledQueue[currentIndex];
                            _playService.ShuffledQueue.RemoveAt(currentIndex);
                            _playService.ShuffledQueue.Insert(0, current);
                        }

                        UpdateNextTrackUI();
                    }
                }

                NotificationWindow.Show("Папка успешно добавлена!", this);
            }
        }

        private void AddFilesToCurrentPlaylist()
        {
            if (PlaylistsListBox.SelectedItem is not Playlist selectedPlaylist) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Music files (*.mp3;*.wav;*.flac)|*.mp3;*.wav;*.flac"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                int addedCount = 0;
                foreach (string filePath in openFileDialog.FileNames)
                {
                    Track? newTrack = TagReader.ReadTrackFromFile(filePath);
                    if (newTrack != null)
                    {
                        DatabaseService.SaveTrackToPlaylist(selectedPlaylist.Id, newTrack);
                        addedCount++;
                    }
                }

                // Обновляем плейлисты
                MusicLibrary.Instance.RefreshPlaylists();

                // Находим обновленный плейлист
                var updatedPlaylist = MusicLibrary.Instance.Playlists
                    .FirstOrDefault(p => p.Id == selectedPlaylist.Id);

                if (updatedPlaylist != null)
                {
                    // Выбираем обновленный плейлист в списке
                    PlaylistsListBox.SelectedItem = updatedPlaylist;

                    // Если Shuffle включен и это текущий воспроизводимый плейлист, обновляем перемешанную очередь
                    if (_playService.IsShuffleEnabled && MusicLibrary.Instance.CurrentPlaylist?.Id == updatedPlaylist.Id)
                    {
                        // Создаем перемешанную копию
                        var shuffledList = updatedPlaylist.Tracks.OrderBy(x => Guid.NewGuid()).ToList();
                        _playService.ShuffledQueue = shuffledList;

                        // Если есть текущий трек, перемещаем его в начало
                        if (Player.CurrentTrack != null && _playService.ShuffledQueue.Contains(Player.CurrentTrack))
                        {
                            int currentIndex = _playService.ShuffledQueue.IndexOf(Player.CurrentTrack);
                            var current = _playService.ShuffledQueue[currentIndex];
                            _playService.ShuffledQueue.RemoveAt(currentIndex);
                            _playService.ShuffledQueue.Insert(0, current);
                        }

                        UpdateNextTrackUI();
                    }
                }

                NotificationWindow.Show($"Добавлено {addedCount} треков", this);
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
                // Создаем плейлист и получаем его ID
                long newId = DatabaseService.CreatePlaylist(
                    dialog.PlaylistName,
                    dialog.PlaylistDescription,
                    dialog.PlaylistCoverImage);

                // Обновляем список плейлистов
                MusicLibrary.Instance.RefreshPlaylists();

                // Находим созданный плейлист по ID
                var newPlaylist = MusicLibrary.Instance.Playlists
                    .FirstOrDefault(p => p.Id == (int)newId);

                if (newPlaylist != null)
                {
                    // Выбираем новый плейлист
                    PlaylistsListBox.SelectedItem = newPlaylist;

                    // Обновляем отображение
                    CurrentPlaylistNameText.Text = newPlaylist.Name;
                    CurrentPlaylistDescriptionText.Text = newPlaylist.Description;
                    CurrentTracksCountText.Text = "0 треков";

                    // Очищаем DataGrid
                    TracksDataGrid.ItemsSource = newPlaylist.Tracks;

                    // Обновляем обложку
                    if (newPlaylist.CoverImage != null && newPlaylist.CoverImage.Length > 0)
                    {
                        CurrentPlaylistCover.Source = LoadImage(newPlaylist.CoverImage);
                    }
                    else
                    {
                        CurrentPlaylistCover.Source = null;
                    }
                }

                NotificationWindow.Show($"Плейлист \"{dialog.PlaylistName}\" создан", this);
            }
        }

        private void PlaylistsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selected)
            {
                PlayPlaylist(selected);
                // NotificationWindow.Show($"Воспроизведение плейлиста \"{selected.Name}\"", this);
            }
        }
        private void PlayPlaylist(Playlist playlist)
        {
            MusicLibrary.Instance.PlayPlaylist(playlist);
            UpdateNextTrackUI();
        }
        private void PlaylistsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selected)
            {
                System.Diagnostics.Debug.WriteLine($"=== ПРОСМОТР ПЛЕЙЛИСТА: {selected.Name} ===");

                MusicLibrary.Instance.CurrentPlaylist = selected;

                // Обновляем UI
                CurrentPlaylistNameText.Text = selected.Name;
                CurrentPlaylistDescriptionText.Text = selected.Description;
                CurrentTracksCountText.Text = $"{selected.Tracks.Count} треков";

                if (selected.CoverImage != null && selected.CoverImage.Length > 0)
                {
                    CurrentPlaylistCover.Source = LoadImage(selected.CoverImage);
                }
                else
                {
                    CurrentPlaylistCover.Source = null;
                }

                // Только обновляем отображение в DataGrid
                TracksDataGrid.ItemsSource = selected.Tracks;

                // НЕ трогаем PlaybackQueue!
            }
        }

        private static BitmapImage? LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;

            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private void TracksDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                var currentPlaylist = PlaylistsListBox.SelectedItem as Playlist;
                if (currentPlaylist != null)
                {
                    MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, currentPlaylist);
                    UpdateNextTrackUI();
                }
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

            Library.CurrentTrack = track;
            _lastTrackWithCover = track;
        }

        private void PlayTrackMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                var currentPlaylist = PlaylistsListBox.SelectedItem as Playlist;
                if (currentPlaylist != null)
                {
                    MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, currentPlaylist);
                    UpdateNextTrackUI();

                    if (PlayerService.Instance.IsShuffleEnabled)
                    {
                        PlayerService.Instance.ShuffledQueue = new List<Track>(
                            MusicLibrary.Instance.PlaybackQueue.OrderBy(x => Guid.NewGuid()));
                    }
                }
            }
        }

        private void RemoveFromPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (Library.CurrentPlaylist == null)
            {
                NotificationWindow.Show("Сначала выберите плейлист", Application.Current.MainWindow);
                return;
            }

            if (TracksDataGrid.SelectedItem is Track selectedTrack &&
                PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                var result = NotificationWindow.Show(
                   $"Удалить трек \"{selectedTrack.Name}\" из плейлиста \"{Library.CurrentPlaylist.Name}\"?",
                   this,
                   NotificationMode.Confirm);

                if (result == true)
                {
                    DatabaseService.RemoveTrackFromPlaylist(selectedPlaylist.Id, selectedTrack.Id);

                    // Обновляем плейлисты
                    MusicLibrary.Instance.RefreshPlaylists();

                    // Восстанавливаем текущий плейлист
                    PlaylistsListBox.SelectedItem = MusicLibrary.Instance.Playlists
                        .FirstOrDefault(p => p.Id == selectedPlaylist.Id);

                    UpdateNextTrackUI();
                }
            }
        }

        private void RemovePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                if (selectedPlaylist.Name == MusicLibrary.FavoritesName)
                {
                    NotificationWindow.Show("Системный плейлист нельзя удалить", this);
                    return;
                }

                if (NotificationWindow.Show($"Удалить плейлист \"{selectedPlaylist.Name}\"?", this, NotificationMode.Confirm) == true)
                {
                    DatabaseService.DeletePlaylist(selectedPlaylist.Id);

                    if (MusicLibrary.Instance.CurrentPlaylist?.Id == selectedPlaylist.Id)
                    {
                        MusicLibrary.Instance.CurrentPlaylist = null;
                    }

                    MusicLibrary.Instance.RefreshPlaylists();
                    PlaylistsListBox.SelectedItem = null;

                    // Очищаем центральную панель
                    CurrentPlaylistNameText.Text = "";
                    CurrentPlaylistDescriptionText.Text = "";
                    CurrentPlaylistCover.Source = null;
                    CurrentTracksCountText.Text = "0 треков";

                    NotificationWindow.Show("Плейлист удален", this);
                }
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Player != null)
            {
                double volume = VolumeSlider.Value / 100.0;
                Player.Volume = volume;

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

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Player.RepeatMode)
            {
                case RepeatMode.NoRepeat:
                    Player.RepeatMode = RepeatMode.RepeatAll;
                    break;
                case RepeatMode.RepeatAll:
                    Player.RepeatMode = RepeatMode.RepeatOne;
                    break;
                case RepeatMode.RepeatOne:
                    Player.RepeatMode = RepeatMode.NoRepeat;
                    break;
            }
            UpdateNextTrackUI();
        }

        private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                if (selectedPlaylist.Name == MusicLibrary.FavoritesName)
                {
                    NotificationWindow.Show("Системный плейлист нельзя удалить", this);
                    return;
                }

                if (NotificationWindow.Show($"Удалить плейлист \"{selectedPlaylist.Name}\"?", this, NotificationMode.Confirm) == true)
                {
                    DatabaseService.DeletePlaylist(selectedPlaylist.Id);

                    if (MusicLibrary.Instance.CurrentPlaylist?.Id == selectedPlaylist.Id)
                    {
                        MusicLibrary.Instance.CurrentPlaylist = null;
                        MusicLibrary.Instance.PlaybackQueue.Clear();
                    }

                    MusicLibrary.Instance.RefreshPlaylists();
                    PlaylistsListBox.SelectedItem = null;

                    NotificationWindow.Show("Плейлист удален", this);
                }
            }
        }

        private void EditPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Playlist selectedPlaylist)
            {
                var dialog = new EditPlaylistDialog
                {
                    Owner = this,
                    DataContext = selectedPlaylist
                };

                if (dialog.ShowDialog() == true)
                {
                    MusicLibrary.Instance.RefreshPlaylists();

                    // Восстанавливаем текущий плейлист
                    PlaylistsListBox.SelectedItem = MusicLibrary.Instance.Playlists
                        .FirstOrDefault(p => p.Id == selectedPlaylist.Id);
                }
            }
        }

        private void ShowTrackInfo_Click(object sender, RoutedEventArgs e)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                var fullInfo = TagReader.GetFullTrackInfo(selectedTrack.Path);

                if (fullInfo != null)
                {
                    var infoWindow = new Windows.ShowTrackInfo(fullInfo)
                    {
                        Owner = this 
                    };
                    infoWindow.ShowDialog();
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

            if (favoritePlaylist == null)
            {
                byte[]? favCover = null;
                try
                {
                    var uri = new Uri("pack://application:,,,/Resources/favorite_cover.png");
                    var resourceStream = Application.GetResourceStream(uri);
                    if (resourceStream != null)
                    {
                        using var ms = new MemoryStream();
                        resourceStream.Stream.CopyTo(ms);
                        favCover = ms.ToArray();
                    }
                }
                catch { }

                long newId = DatabaseService.CreatePlaylist(MusicLibrary.FavoritesName, "Ваши любимые треки", favCover);
                Library.RefreshPlaylists();
                favoritePlaylist = Library.Playlists.FirstOrDefault(p => p.Id == (int)newId);
            }

            if (favoritePlaylist == null) return;

            bool isAlreadyFavorite = favoritePlaylist.Tracks.Any(t => t.Path == Player.CurrentTrack.Path);

            if (!isAlreadyFavorite)
            {
                DatabaseService.SaveTrackToPlaylist(favoritePlaylist.Id, Player.CurrentTrack);
                UpdateFavoriteIcon(Player.CurrentTrack, true);
                NotificationWindow.Show("Добавлено в Избранное", this);
            }
            else
            {
                DatabaseService.RemoveTrackFromPlaylist(favoritePlaylist.Id, Player.CurrentTrack.Id);
                UpdateFavoriteIcon(Player.CurrentTrack, false);
                NotificationWindow.Show("Удалено из Избранного", this);
            }

            if (Library.CurrentPlaylist?.Id == favoritePlaylist.Id)
            {
                Library.RefreshPlaylists();
                PlaylistsListBox.SelectedItem = Library.Playlists
                    .FirstOrDefault(p => p.Id == favoritePlaylist.Id);
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
                ? "/Resources/favorites_added.png"
                : "/Resources/add_favorites.png";

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
                    using var ms = new MemoryStream(track.CoverImage);
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

        private static void LoadPlaylists()
        {
            System.Diagnostics.Debug.WriteLine("=== НАЧАЛО ЗАГРУЗКИ ПЛЕЙЛИСТОВ ===");

            // Прямая проверка БД
            // DatabaseService.DebugDirectDatabaseCheck();

            // Загружаем плейлисты
            MusicLibrary.Instance.RefreshPlaylists();

            System.Diagnostics.Debug.WriteLine("=== КОНЕЦ ЗАГРУЗКИ ПЛЕЙЛИСТОВ ===");
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
    }
}