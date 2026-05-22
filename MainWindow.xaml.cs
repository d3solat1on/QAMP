using System.IO;
using System.Windows;
using System.Windows.Threading;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;

namespace QAMP
{
    public partial class MainWindow : Window
    {
        private readonly PlayerService _playService = PlayerService.Instance;
        private readonly DispatcherTimer _memoryCleanupTimer;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        public static MusicLibrary Library => MusicLibrary.Instance;
        private static PlayerService Player => PlayerService.Instance;
        private bool _isSliderDragging = false;
        private double _lastFormattedSeconds = -1;
        private double _lastVolume = 0.5;
        private Track? _lastTrackWithCover;
        private bool _isLyricsMode = false;
        // private StressTester? _stressTester;

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();

            System.Diagnostics.Debug.WriteLine("=== ПУТЬ К БАЗЕ ДАННЫХ ===");
            System.Diagnostics.Debug.WriteLine($"Путь: {DatabaseService.DatabasePath}");
            System.Diagnostics.Debug.WriteLine($"Папка существует: {Directory.Exists(Path.GetDirectoryName(DatabaseService.DatabasePath))}");

            DatabaseService.EnsureDatabaseCreated();

            System.Diagnostics.Debug.WriteLine($"База данных существует: {File.Exists(DatabaseService.DatabasePath)}");
            LoadPlaylists();
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

            Closing += (s, e) => OnClosing(e);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Загружаем громкость - устанавливаем в слайдер, это вызовет VolumeSlider_ValueChanged
            string savedVolume = DatabaseService.GetSetting("Volume", "0.5");

            // ВАЖНО: парсим с InvariantCulture! В БД сохраняется с точкой (0.5), а не с запятой (0,5)
            if (double.TryParse(savedVolume, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double vol))
            {
                // Важно: устанавливаем сначала Value слайдера
                // Это вызовет VolumeSlider_ValueChanged, который установит Player.Volume
                VolumeSlider.Value = vol * 100;

                // На случай если ValueChanged не сработал (например, если новое значение = старому)
                // Явно установим Player.Volume
                if (Math.Abs(Player.Volume - vol) > 0.01)
                {
                    Player.Volume = vol;
                }
            }

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
                    System.Diagnostics.Debug.WriteLine($"DEBUG MainWindow_Loaded: LastTrackPath={lastTrackPath}");

                    if (!string.IsNullOrEmpty(lastTrackPath))
                    {
                        var lastTrack = playlist.Tracks.FirstOrDefault(t => t.Path == lastTrackPath);
                        if (lastTrack != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"DEBUG MainWindow_Loaded: Загружаю трек {lastTrack.Name}");
                            _playService.LoadTrack(lastTrack);

                            // Восстанавливаем позицию проигрывания
                            string positionStr = DatabaseService.GetSetting("LastTrackPosition", "0");
                            if (double.TryParse(positionStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double position))
                            {
                                _playService.Seek(position);
                                System.Diagnostics.Debug.WriteLine($"DEBUG MainWindow_Loaded: Позиция установлена на {position}s");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"DEBUG MainWindow_Loaded: Трек не найден, инициализирую пустое состояние");
                            OnTrackChanged(null);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG MainWindow_Loaded: LastTrackPath пуст, инициализирую пустое состояние");
                        OnTrackChanged(null);
                    }
                }
                else
                {
                    // Плейлист не найден - инициализируем UI для пустого состояния
                    System.Diagnostics.Debug.WriteLine($"DEBUG MainWindow_Loaded: Плейлист не найден");
                    OnTrackChanged(null);
                }
            }
            else
            {
                // Нет последнего плейлиста (первый запуск или БД пуста) - инициализируем UI для пустого состояния
                System.Diagnostics.Debug.WriteLine($"DEBUG MainWindow_Loaded: Нет последнего плейлиста");
                OnTrackChanged(null);
            }

            // Обновляем UI элементы
            if (VolumePercentage != null)
            {
                VolumePercentage.Text = $"{VolumeSlider.Value:F0}%";
                System.Diagnostics.Debug.WriteLine($"DEBUG: VolumePercent обновлен: {VolumeSlider.Value:F0}%");
            }
            // _stressTester = new StressTester(
            //     togglePlayPause: TogglePlayPause,
            //     setPlaylistIndex: (index) => PlaylistsListBox.SelectedIndex = index,
            //     getPlaylistsCount: () => PlaylistsListBox.Items.Count,
            //     getTracksCount: () => TracksDataGrid.Items.Count,
            //     playTrackByIndex: (index) =>
            //     {
            //         if (TracksDataGrid.Items[index] is Track track)
            //             _ = Player.PlayTrack(track);
            //     },
            //     showMessage: (text) => Task.Run(() =>
            //     {
            //         Application.Current.Dispatcher.Invoke(() =>
            //         {
            //             NotificationWindow.Show(text, this, NotificationMode.Info);
            //         });
            //     })
            // );

            // KeyDown += async (s, e) =>
            // {
            //     if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            //     {
            //         if (_stressTester.IsRunning)
            //             _stressTester.Stop();
            //         else
            //             await _stressTester.Run(TimeSpan.FromMinutes(5));
            //     }
            // };
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
                    CurrentTrackYear.Text = track.Year > 0 ? track.Year.ToString() : "Неизвестный год";
                    NextTrack.Text = "Следующий трек";
                    NowPlaying.Text = "NOW PLAYING";
                    Title = $"{track.Name} - {track.Executor} | QAMP";

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
    }
}
