using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QAMP.Audio;
using QAMP.Dialogs;
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
        private double _lastVolume = 0.5;
        private Track? _lastTrackWithCover;
        private bool _isLyricsMode = false;

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
            PreviewKeyDown += Window_KeyDown;
            PreviewKeyDown += TracksDataGrid_PreviewKeyDown;

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
                }
            }

            // Загружаем последний трек
            string lastTrackPath = DatabaseService.GetSetting("LastTrackPath", "");
            
            if (!string.IsNullOrEmpty(lastTrackPath) && lastPlaylistId != -1)
            {
                var currentPlaylist = PlaylistsListBox.SelectedItem as Playlist 
                    ?? MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == lastPlaylistId);
                
                if (currentPlaylist != null)
                {
                    var lastTrack = currentPlaylist.Tracks.FirstOrDefault(t => t.Path == lastTrackPath);
                    
                    if (lastTrack != null)
                    {
                        // Загружаем трек, но не воспроизводим его
                        _playService.LoadTrack(lastTrack);
                        
                        // Восстанавливаем позицию проигрывания
                        string positionStr = DatabaseService.GetSetting("LastTrackPosition", "0");
                        // ВАЖНО: парсим с InvariantCulture! В БД сохраняется с точкой, а не с запятой
                        if (double.TryParse(positionStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double position))
                        {
                            _playService.Seek(position);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Трек загружен на паузе. IsPlaying={_playService.IsPlaying}");
                    }
                }
            }

            // Обновляем UI элементы
            if (VolumePercentage != null)
            {
                VolumePercentage.Text = $"{VolumeSlider.Value:F0}%";
                System.Diagnostics.Debug.WriteLine($"DEBUG: VolumePercent обновлен: {VolumeSlider.Value:F0}%");
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
            if (track == null) return;
            Dispatcher.Invoke(() =>
            {
                UpdateNowPlayingInfo(track);
                UpdatePlayPauseIcon(Player.IsPlaying);
                UpdateFavoriteIcon(track);
                CurrentTrackName.Text = track.Name;
                CurrentTrackExecutor.Text = track.Executor;
                CurrentTrackAlbum.Text = track.Album;
                CurrentTrackData.Text = $"{track.Genre} | {track.Duration} | {track.SampleRate} Hz | {track.Bitrate} kbps";
                CurrentTrackExtension.Text = track.Extension;
                CurrentTrackYear.Text = track.Year > 0 ? track.Year.ToString() : "Неизвестно";

                string totalTime = Player.Duration > 0 ? FormatTime(Player.Duration) : "Загрузка...";
                if (Player.Duration <= 0) CheckDurationAsync();

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
                    Dispatcher.Invoke(() => { TotalTimeText.Text = FormatTime(Player.Duration); });
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
            Dispatcher.Invoke(() => { UpdatePlayPauseIcon(!isPaused); });
        }
    }
}
