using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;

namespace QAMP
{
    public partial class MainWindow : Window
    {
        [DllImport("QampCore.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetCoreVersion();
        private readonly PlayerService _playService = PlayerService.Instance;
        public static MusicLibrary Library => MusicLibrary.Instance;
        private static PlayerService Player => PlayerService.Instance;
        private bool _isSliderDragging = false;
        private double _lastFormattedSeconds = -1;
        private double _lastVolume = 0.5;
        private Track? _lastTrackWithCover;
        private bool _isLyricsMode = false;

        private readonly Grid? _playlistsLoadingPlaceholder;
        private readonly Grid? _tracksLoadingPlaceholder;
        private readonly Grid? _nowPlayingLoadingPlaceholder;
        private readonly StackPanel? _nowPlayingPanel;
        private MediaControlsManager? _mediaManager;

        public MainWindow()
        {
            InitializeComponent();
            TestCppDll();
            _playlistsLoadingPlaceholder = (Grid?)FindName("PlaylistsLoadingPlaceholder");
            _tracksLoadingPlaceholder = (Grid?)FindName("TracksLoadingPlaceholder");
            _nowPlayingLoadingPlaceholder = (Grid?)FindName("NowPlayingLoadingPlaceholder");
            _nowPlayingPanel = (StackPanel?)FindName("NowPlayingPanel");

            System.Diagnostics.Debug.WriteLine("=== ПУТЬ К БАЗЕ ДАННЫХ ===");
            System.Diagnostics.Debug.WriteLine($"Путь: {DatabaseService.DatabasePath}");
            System.Diagnostics.Debug.WriteLine($"Папка существует: {Directory.Exists(Path.GetDirectoryName(DatabaseService.DatabasePath))}");

            DatabaseService.EnsureDatabaseCreated();

            System.Diagnostics.Debug.WriteLine($"База данных существует: {File.Exists(DatabaseService.DatabasePath)}");
            LoadApplicationSettings();
            DataContext = MusicLibrary.Instance;

            Player.TrackChanged += OnTrackChanged;
            Player.PositionChanged += OnPositionChanged;
            Player.PlaybackPaused += OnPlaybackPaused;
            Player.VolumeChanged += OnVolumeChanged;
            Player.DurationChanged += OnDurationChanged;
            _playService.TrackChanged += UpdateNextTrackUI;
            PlaylistsListBox.MouseDoubleClick += PlaylistsListBox_MouseDoubleClick;
            PreviewKeyDown += Window_PreviewKeyDown;
            PreviewKeyDown += TracksDataGrid_PreviewKeyDown;
            PlayerService.Instance.AddSpectrumControl(SpectrumViewer);
            Closing += (s, e) => OnClosing(e);
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== MainWindow_Loaded НАЧАЛО ===");

            if (_mediaManager == null)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    _mediaManager = new MediaControlsManager(hwnd);
                    InitializeMediaControlsManagerHandlers();

                    _mediaManager.UpdatePlaybackStatus(Player.IsPlaying);
                }
            }

            // Загружаем громкость - устанавливаем в слайдер, это вызовет VolumeSlider_ValueChanged
            string savedVolume = DatabaseService.GetSetting("Volume", "0.5");

            // ВАЖНО: парсим с InvariantCulture! В БД сохраняется с точкой (0.5), а не с запятой (0,5)
            if (double.TryParse(savedVolume, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double vol))
            {
                VolumeSlider.Value = vol * 100;
                if (Math.Abs(Player.Volume - vol) > 0.01)
                {
                    Player.Volume = vol;
                }
            }

            // Обновляем UI элементы
            VolumePercentage?.Text = $"{VolumeSlider.Value:F0}%";

            System.Diagnostics.Debug.WriteLine("=== ЗАПУСК АСИНХРОННОЙ ЗАГРУЗКИ ===");
            var savedSort = AppSettings.CurrentPlaylistSort;
            ApplyPlaylistSorting(savedSort);
            _ = InitializePlaylistsAndTracksAsync();
        }

        /// <summary>
        /// Асинхронно загружает плейлисты и треки, показывая плейсхолдеры во время загрузки
        /// </summary>
        private async Task InitializePlaylistsAndTracksAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Шаг 1: Показываем плейсхолдер плейлистов");
                _playlistsLoadingPlaceholder?.Visibility = Visibility.Visible;
                PlaylistsListBox.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine("Шаг 2: Загружаем плейлисты");
                await MusicLibrary.Instance.RefreshPlaylistsAsync();

                System.Diagnostics.Debug.WriteLine("Шаг 3: Показываем плейлисты");
                _playlistsLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                PlaylistsListBox.Visibility = Visibility.Visible;

                System.Diagnostics.Debug.WriteLine("Шаг 4: Показываем плейсхолдер треков");
                _tracksLoadingPlaceholder?.Visibility = Visibility.Visible;
                TracksDataGrid.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine("Шаг 5: Загружаем треки для плейлистов");
                await MusicLibrary.Instance.LoadAllPlaylistsTracksAsync(
                    onProgress: (current, total) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Прогресс загрузки треков: {current}/{total}");
                    }
                );

                System.Diagnostics.Debug.WriteLine("Шаг 6: Показываем DataGrid");
                _tracksLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                TracksDataGrid.Visibility = Visibility.Visible;

                System.Diagnostics.Debug.WriteLine("Шаг 7: Показываем плейсхолдер информации о треке");
                _nowPlayingLoadingPlaceholder?.Visibility = Visibility.Visible;
                _nowPlayingPanel?.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine("Шаг 8: Восстанавливаем последний плейлист и трек");
                await RestoreLastPlaylistAndTrackAsync();

                System.Diagnostics.Debug.WriteLine("Шаг 9: Показываем информацию о треке");
                _nowPlayingLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                _nowPlayingPanel?.Visibility = Visibility.Visible;

                System.Diagnostics.Debug.WriteLine("=== ИНИЦИАЛИЗАЦИЯ ЗАВЕРШЕНА ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при инициализации: {ex.Message}");
                _playlistsLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                PlaylistsListBox.Visibility = Visibility.Visible;
                _tracksLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                TracksDataGrid.Visibility = Visibility.Visible;
                _nowPlayingLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                _nowPlayingPanel?.Visibility = Visibility.Visible;
            }
            finally
            {
                MemoryOptimizer.RunAsync(Dispatcher);
            }
        }

        private void InitializeMediaControlsManagerHandlers()
        {
            if (_mediaManager == null) return;

            _mediaManager.OnPlayRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine("SMTC: Play Requested");
                    if (_playService.CurrentTrack != null)
                    {
                        if (!_playService.IsPlaying)
                        {
                            _playService.Resume();
                        }
                    }
                    else if (MusicLibrary.Instance.PlaybackQueue.Count > 0)
                    {
                        var t = MusicLibrary.Instance.PlaybackQueue[0];
                        _ = _playService.PlayTrack(t);
                    }
                });
            };

            _mediaManager.OnPauseRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine("SMTC: Pause Requested");
                    if (_playService.IsPlaying)
                    {
                        _ = _playService.PauseAsync();
                    }
                });
            };

            _mediaManager.OnNextRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine("SMTC: Next Requested");
                    _playService.PlayNextTrack();
                });
            };

            _mediaManager.OnPreviousRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine("SMTC: Previous Requested");
                    _playService.PlayPreviousTrack();
                });
            };
        }

        /// <summary>
        /// Восстанавливает последний выбранный плейлист и трек
        /// </summary>
        private async Task RestoreLastPlaylistAndTrackAsync()
        {
            // Загружаем последний выбранный плейлист
            string lastPlaylistIdStr = DatabaseService.GetSetting("LastPlaylistId", "-1");

            int lastPlaylistId = -1;
            if (int.TryParse(lastPlaylistIdStr, out int id) && id != -1)
            {
                var playlist = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == id);

                if (playlist != null)
                {
                    lastPlaylistId = id;
                    PlaylistsListBox.SelectedItem = playlist;
                    MusicLibrary.Instance.PlayingPlaylist = playlist;
                    MusicLibrary.Instance.PlaybackQueue.Clear();
                    foreach (var t in playlist.Tracks)
                    {
                        MusicLibrary.Instance.PlaybackQueue.Add(t);
                    }
                    Player.UpdateQueueOrder([.. MusicLibrary.Instance.PlaybackQueue]);

                    // Явно загружаем последний трек этого плейлиста
                    string lastTrackPath = DatabaseService.GetSetting("LastTrackPath", "");
                    System.Diagnostics.Debug.WriteLine($"DEBUG: RestoreLastPlaylistAndTrackAsync - LastTrackPath={lastTrackPath}");

                    if (!string.IsNullOrEmpty(lastTrackPath))
                    {
                        var lastTrack = playlist.Tracks.FirstOrDefault(t => t.Path == lastTrackPath);
                        if (lastTrack != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"DEBUG: Загружаю трек {lastTrack.Name}");
                            _playService.LoadTrack(lastTrack);

                            // Восстанавливаем позицию проигрывания
                            string positionStr = DatabaseService.GetSetting("LastTrackPosition", "0");
                            if (double.TryParse(positionStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double position))
                            {
                                _playService.Seek(position);
                                System.Diagnostics.Debug.WriteLine($"DEBUG: Позиция установлена на {position}s");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"DEBUG: Трек не найден, инициализирую пустое состояние");
                            OnTrackChanged(null);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG: LastTrackPath пуст, инициализирую пустое состояние");
                        OnTrackChanged(null);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Плейлист не найден");
                    OnTrackChanged(null);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: Нет последнего плейлиста");
                OnTrackChanged(null);
            }
        }

        private static void LoadApplicationSettings()
        {
            ThemeManager.ApplyTheme(SettingsManager.Instance.Config.ColorScheme);
        }

        private void OnDurationChanged()
        {
            Dispatcher.Invoke(() => { TotalTimeText.Text = FormatTime(Player.Duration); });
        }

        private void OnVolumeChanged(double volume)
        {
            Dispatcher.Invoke(() =>
            {
                VolumeSlider.Value = volume * 100;
                VolumePercentage.Text = $"{volume * 100:F0}%";
            });
        }

        private void OnTrackChanged(Track? track)
        {
            if (track == null)
            {
                CurrentTrackImage.Source = null;
                FavoriteButton1Grid.Visibility = Visibility.Collapsed;
                return;
            }
            Dispatcher.Invoke(() =>
            {
                try
                {
                    DatabaseService.SaveSettingSync("LastTrackPath", track.Path ?? "");
                    UpdateNowPlayingInfo(track);
                    UpdatePlayPauseIconState();
                    UpdateFavoriteIcon(track);
                    FavoriteButton1Grid.Visibility = Visibility.Visible;
                    CurrentTrackName.Text = track.Name;
                    CurrentTrackExecutor.Text = track.Executor;
                    CurrentTrackAlbum.Text = track.Album;
                    CurrentTrackData.Text = $"{track.Genre} | {track.Duration} | {track.SampleRate} Hz | {track.Bitrate} kbps";
                    CurrentTrackExtension.Text = track.DisplayExtension;
                    CurrentTrackYear.Text = track.Year > 0 ? track.Year.ToString() : "Unknown year";
                    NextTrack.Text = "Next Track";
                    NowPlaying.Text = "NOW PLAYING";
                    Title = $"{track.Name} - {track.Executor} | QAMP";
                    if (_mediaManager != null)
                    {
                        try
                        {
                            _mediaManager.UpdateTrackInfo(
                                track.Name ?? "Неизвестный трек",
                                track.Executor ?? "Неизвестный исполнитель",
                                track.Album ?? "Неизвестный альбом"
                            );
                            _mediaManager.UpdatePlaybackStatus(PlayerService.Instance.IsPlaying);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка обновления SMTC: {ex.Message}");
                        }
                    }

                    string totalTime = Player.Duration > 0 ? FormatTime(Player.Duration) : "Загрузка...";
                    if (Player.Duration <= 0) CheckDurationAsync();
                    TotalTimeText.Text = totalTime;
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] OnTrackChanged: {track.Name}");
                    if (_imageConverter.Convert(track.CoverImage, typeof(System.Windows.Media.Imaging.BitmapSource), null, System.Globalization.CultureInfo.InvariantCulture) is System.Windows.Media.ImageSource cover)
                    {
                        CurrentTrackImage.Source = cover;
                        CurrentTrackImage.Stretch = System.Windows.Media.Stretch.UniformToFill;
                    }
                    else
                    {
                        CurrentTrackImage.Source = (System.Windows.Media.ImageSource)FindResource("default_coverDrawingImage");
                        CurrentTrackImage.Stretch = System.Windows.Media.Stretch.UniformToFill;
                        CurrentTrackImage.HorizontalAlignment = HorizontalAlignment.Center;
                        CurrentTrackImage.VerticalAlignment = VerticalAlignment.Center;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Error in OnTrackChanged: {ex.Message}");
                }
            });
        }

        private async void CheckDurationAsync()
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(100);
                if (Player.Duration > 0)
                {
                    Dispatcher.Invoke(() => { TotalTimeText.Text = FormatTime(Player.Duration); });
                    break;
                }
            }
        }

        private void OnPositionChanged(double position)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<double>(OnPositionChanged), position);
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

                double currentWholeSecond = Math.Floor(position);
                if (currentWholeSecond != _lastFormattedSeconds)
                {
                    _lastFormattedSeconds = currentWholeSecond;
                    CurrentTimeText.Text = FormatTime(position);
                }
            }
            if (_isLyricsMode)
            {
                UpdateLyricsHighlight(TimeSpan.FromSeconds(position));
            }
        }

        private void OnPlaybackPaused(bool isPaused)
        {
            Dispatcher.Invoke(UpdatePlayPauseIconState);
        }
        private static void TestCppDll()
        {
            try
            {
                int version = GetCoreVersion();
                System.Diagnostics.Debug.WriteLine($"[QAMP Native] Версия C++ ядра: {version}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QAMP Native] Ошибка вызова DLL: {ex.Message}");
            }
        }
    }
}
