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
        private double _lastFormattedSeconds = -1;
        private double _lastVolume = 0.5;

        private readonly Grid? _playlistsLoadingPlaceholder;
        private readonly Grid? _tracksLoadingPlaceholder;
        private readonly Grid? _nowPlayingLoadingPlaceholder;
        private readonly StackPanel? _nowPlayingPanel;
        private MediaControlsManager? _mediaManager;
        private bool _isClosing = false;


        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            TestCppDll();
#endif
            _playlistsLoadingPlaceholder = (Grid?)FindName("PlaylistsLoadingPlaceholder");
            _tracksLoadingPlaceholder = (Grid?)FindName("TracksLoadingPlaceholder");
            _nowPlayingLoadingPlaceholder = (Grid?)FindName("NowPlayingLoadingPlaceholder");
            _nowPlayingPanel = (StackPanel?)FindName("NowPlayingPanel");

            System.Diagnostics.Debug.WriteLine("=== ПУТЬ К БАЗЕ ДАННЫХ ===");
            System.Diagnostics.Debug.WriteLine($"Путь: {DatabaseService.DatabasePath}");
            System.Diagnostics.Debug.WriteLine($"Папка существует: {Directory.Exists(Path.GetDirectoryName(DatabaseService.DatabasePath))}");

            DatabaseService.EnsureDatabaseCreated();

            System.Diagnostics.Debug.WriteLine($"База данных существует: {File.Exists(DatabaseService.DatabasePath)}");
            DataContext = MusicLibrary.Instance;

            Player.TrackChanged += OnTrackChanged;
            Player.PositionChanged += OnPositionChanged;
            Player.PlaybackPaused += OnPlaybackPaused;
            Player.VolumeChanged += OnVolumeChanged;
            Player.DurationChanged += OnDurationChanged;
            _playService.TrackChanged += UpdateNextTrackUI;
            PreviewKeyDown += Window_PreviewKeyDown;
            PreviewKeyDown += TracksDataGrid_PreviewKeyDown;
            PreviewMouseLeftButtonDown += Window_PreviewMouseLeftButtonDown;
            PlayerService.Instance.AddSpectrumControl(SpectrumViewer);
            Closing += (s, e) => OnClosing(e);
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== MainWindow_Loaded НАЧАЛО ===");

            App.RegisterWindowForSingleInstance(this);

            if (_mediaManager == null)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    _mediaManager = new MediaControlsManager();
                    InitializeMediaControlsManagerHandlers();

                    _mediaManager.UpdatePlaybackStatus(Player.IsPlaying);
                }
            }

            string savedVolume = DatabaseService.GetSetting("Volume", "0.5");

            if (double.TryParse(savedVolume, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double vol))
            {
                VolumeSlider.Value = vol * 100;
                if (Math.Abs(Player.Volume - vol) > 0.01)
                {
                    Player.Volume = vol;
                }
            }

            VolumePercentage?.Text = $"{VolumeSlider.Value:F0}%";

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
                _playlistsLoadingPlaceholder?.Visibility = Visibility.Visible;
                PlaylistsListBox.Visibility = Visibility.Collapsed;

                await MusicLibrary.Instance.RefreshPlaylistsAsync();

                _playlistsLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                PlaylistsListBox.Visibility = Visibility.Visible;

                _tracksLoadingPlaceholder?.Visibility = Visibility.Visible;
                TracksDataGrid.Visibility = Visibility.Collapsed;

                await MusicLibrary.Instance.LoadAllPlaylistsTracksAsync(
                    onProgress: (current, total) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Прогресс загрузки треков: {current}/{total}");
                    }
                );

                _tracksLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                TracksDataGrid.Visibility = Visibility.Visible;

                _nowPlayingLoadingPlaceholder?.Visibility = Visibility.Visible;
                _nowPlayingPanel?.Visibility = Visibility.Collapsed;

                await RestoreLastPlaylistAndTrackAsync();

                _nowPlayingLoadingPlaceholder?.Visibility = Visibility.Collapsed;
                _nowPlayingPanel?.Visibility = Visibility.Visible;

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

        private void OnPlaybackPaused(bool isPaused)
        {
            Dispatcher.Invoke(UpdatePlayPauseIconState);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
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
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        // ?
#if DEBUG
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
#endif

    }
}