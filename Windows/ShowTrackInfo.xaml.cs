using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using TagLib;
using QAMP.Audio;
using QAMP.Dialogs;
using QAMP.Models;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.AddOn.Flac;


namespace QAMP.Windows
{
    public partial class ShowTrackInfo : Window
    {
        private readonly Track _track;
        public ShowTrackInfo(Track track)
        {
            InitializeComponent();
            _track = track;
            DataContext = track;
            Loaded += ShowTrackInfo_Loaded;
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.I && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    Close();
                }
            };
        }
        private void ShowTrackInfo_Loaded(object sender, RoutedEventArgs e)
        {
            if (FindName("PathTextBlock") is System.Windows.Controls.TextBlock pathTextBlock)
            {
                pathTextBlock.MouseLeftButtonDown += PathTextBlock_MouseLeftButtonDown;
            }

            var player = Services.PlayerService.Instance;
            if (player != null)
            {
                player.PlayCountUpdated += Player_PlayCountUpdated;

                Closed += (s, e) => player.PlayCountUpdated -= Player_PlayCountUpdated;
            }
        }

        private void Player_PlayCountUpdated(int trackId)
        {
            if (_track != null && _track.Id == trackId)
            {
                Debug.WriteLine($"[ShowTrackInfo] Updating PlayCount for track {trackId}");

                _track.NotifyPropertyChanged(nameof(Track.PlayCount));
            }
        }

        private void PathTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var currentTrack = _track;

            if (string.IsNullOrEmpty(currentTrack?.Path)) return;
            try
            {
                string? directory = Path.GetDirectoryName(currentTrack.Path);

                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Process.Start("explorer.exe", $"/select,\"{currentTrack.Path}\"");
                }
                else
                {
                    string message = (string)Application.Current.FindResource("LngFolderNotFound");
                    NotificationWindow.Show(message, this);
                }
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message} {ex.Message}", this);
            }
        }
        private async void DetectBPM_Click(object sender, RoutedEventArgs e)
        {
            string filePath = _track.Path;

            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                string message = (string)Application.Current.FindResource("LngFileNotFound");
                NotificationWindow.Show(message, this);
                return;
            }

            BPMTextBlock.Text = "...";

            try
            {
                var (bpm, error) = await Task.Run(() =>
                {
                    int decodeStream = 0;
                    string extension = System.IO.Path.GetExtension(filePath).ToLower();

                    if (extension == ".flac")
                    {
                        decodeStream = BassFlac.BASS_FLAC_StreamCreateFile(filePath, 0L, 0L, BASSFlag.BASS_STREAM_DECODE);
                    }
                    else
                    {
                        decodeStream = Bass.BASS_StreamCreateFile(filePath, 0L, 0L,
                            BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_STREAM_PRESCAN);
                    }

                    if (decodeStream == 0)
                    {
                        var errorCode = Bass.BASS_ErrorGetCode();
                        return (Bpm: -1, Error: errorCode);
                    }

                    try
                    {
                        double startSec = 60.0;
                        double endSec = 110.0;
                        int minBpm = 45;
                        int maxBpm = 160;

                        float detectedBpm = BassFx.BASS_FX_BPM_DecodeGet(
                            decodeStream,
                            startSec,
                            endSec,
                            minBpm | (maxBpm << 16),
                            BASSFXBpm.BASS_FX_BPM_DEFAULT,
                            null,
                            IntPtr.Zero
                        );

                        if (detectedBpm > 0)
                        {
                            while (detectedBpm > 165.0f)
                            {
                                detectedBpm /= 2.0f;
                            }
                            return (Bpm: (int)Math.Round(detectedBpm), Error: BASSError.BASS_OK);
                        }

                        return (Bpm: 0, Error: BASSError.BASS_OK);
                    }
                    finally
                    {
                        Bass.BASS_StreamFree(decodeStream);
                    }
                });

                if (bpm > 0)
                {
                    _track.BPM = bpm;
                    BPMTextBlock.Text = bpm.ToString();
                    string message = (string)Application.Current.FindResource("LngBPMDetected");
                    await TrackInfoToast.ShowAsync(message);
                }
                else if (bpm == -1)
                {
                    string message = (string)Application.Current.FindResource("LngBASSErorr");
                    NotificationWindow.Show($"{message} {error}", this);
                }
                else
                {
                    string message = (string)Application.Current.FindResource("LngNoBPM");
                    NotificationWindow.Show(message, this);
                }
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message} {ex.Message}", this);
                System.Diagnostics.Debug.WriteLine($"BPM Detection Error: {ex}");
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var fileStream = new FileStream(_track.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                var fileAbstraction = new StreamFileAbstraction(_track.Path, fileStream, fileStream);
                using var file = TagLib.File.Create(fileAbstraction);

                file.Tag.Title = _track.Name ?? string.Empty;
                file.Tag.Performers = [_track.Executor ?? string.Empty];
                file.Tag.Album = _track.Album ?? string.Empty;
                file.Tag.AlbumArtists = [_track.AlbumArtist ?? string.Empty];
                file.Tag.Genres = [_track.Genre ?? string.Empty];
                file.Tag.Comment = _track.Comment ?? string.Empty;
                file.Tag.Lyrics = _track.Lyrics ?? string.Empty;
                file.Tag.Composers = [_track.Composer ?? string.Empty];
                file.Tag.Track = _track.TrackNumber > 0 ? (uint)_track.TrackNumber : 0;
                file.Tag.BeatsPerMinute = _track.BPM > 0 ? (uint)_track.BPM : 0;

                if (_track.Year > 0)
                    file.Tag.Year = (uint)_track.Year;

                file.Save();

                Services.DatabaseService.UpdateTrackMetadata(_track);

                string message = (string)Application.Current.FindResource("LngTagsSaved");
                await TrackInfoToast.ShowAsync(message);
                EditModeButton.IsChecked = false;
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message} {ex.Message}", this, NotificationWindow.NotificationMode.Info);
                System.Diagnostics.Debug.WriteLine($"Error saving tags: {ex}");
            }
        }

        private async void ExtractCover_Click(object sender, RoutedEventArgs e)
        {
            string safeFileName = $"{_track.Executor} - {_track.Name} Cover";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeFileName = safeFileName.Replace(c, '_');
            }
            if (_track.CoverImage == null || _track.CoverImage.Length == 0)
            {
                string message = (string)Application.Current.FindResource("LngTrackNoArt");
                NotificationWindow.Show(message, this);
                return;
            }

            SaveFileDialog sfd = new()
            {
                Filter = "JPEG Image|*.jpg|PNG Image|*.png",
                FileName = safeFileName
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllBytes(sfd.FileName, _track.CoverImage);
                    string message = (string)Application.Current.FindResource("LngCoverArtExtract");
                    await TrackInfoToast.ShowAsync(message);
                }
                catch (Exception ex)
                {
                    string message = (string)Application.Current.FindResource("LngError");
                    NotificationWindow.Show($"{message} {ex.Message}", this);
                }
            }
        }

        private async void ChangeCover_Click(object sender, MouseButtonEventArgs e)
        {
            if (EditModeButton.IsChecked != true) return;

            OpenFileDialog ofd = new()
            {
                Filter = "Images|*.jpg;*.jpeg;*.png"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    using (var file = TagLib.File.Create(_track.Path))
                    {
                        var picture = new Picture(ofd.FileName);
                        file.Tag.Pictures = [picture];
                        file.Save();
                    }

                    _track.CoverImage = System.IO.File.ReadAllBytes(ofd.FileName);
                    string message = (string)Application.Current.FindResource("LngCoverArtUpdate");
                    await TrackInfoToast.ShowAsync(message);
                }
                catch (Exception ex)
                {
                    string message = (string)Application.Current.FindResource("LngErorr");
                    NotificationWindow.Show($"{message} {ex.Message}", this);
                }
            }
        }


        private void LyricsBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                var lyricsWindow = new LyricsWindow(_track)
                {
                    Owner = this // Чтобы окно центрировалось относительно родителя
                };
                lyricsWindow.ShowDialog();
            }
        }

        private async void SearchLyrics_Click(object sender, RoutedEventArgs e)
        {
            var track = _track;
            if (track == null) return;

            bool isInvalidExecutor = string.IsNullOrWhiteSpace(track.Executor) ||
                                     track.Executor.Equals("Неизвестный", StringComparison.OrdinalIgnoreCase) ||
                                     track.Executor.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

            bool isInvalidName = string.IsNullOrWhiteSpace(track.Name) ||
                                 track.Name.Equals("Неизвестно", StringComparison.OrdinalIgnoreCase) ||
                                 track.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

            if (isInvalidExecutor || isInvalidName)
            {
                string message = (string)Application.Current.FindResource("LngNoDataToSeacrh");
                NotificationWindow.Show(message, this);
                return;
            }

            string SearchMessage = (string)Application.Current.FindResource("LngSearchInLRCLIB");
            TrackInfoToast.StartLoading(SearchMessage);

            try
            {
                string? lyrics = await FetchLrcFromLrcLib(track.Executor, track.Name);

                if (!string.IsNullOrEmpty(lyrics))
                {
                    track.Lyrics = lyrics;
                    if (FindName("LyricsTextBox") is System.Windows.Controls.TextBox tb)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            tb.Text = lyrics;
                        });
                    }
                    try
                    {
                        using (var file = TagLib.File.Create(track.Path))
                        {
                            file.Tag.Lyrics = lyrics;
                            file.Save();
                        }
                        string message = (string)Application.Current.FindResource("LngTextSaved");
                        await TrackInfoToast.ShowAsync(message);
                        await TrackInfoToast.StopLoadingAsync();
                    }
                    catch (Exception ex)
                    {
                        await TrackInfoToast.StopLoadingAsync();
                        string message = (string)Application.Current.FindResource("LngErrorFile");
                        NotificationWindow.Show($"{message} {ex.Message}", this);
                    }
                }
                else
                {
                    string message = (string)Application.Current.FindResource("LngTextNotFound");
                    await TrackInfoToast.ShowAsync(message);
                }
            }
            catch (Exception ex)
            {
                await TrackInfoToast.StopLoadingAsync();
                string message = (string)Application.Current.FindResource("LngErorr");
                NotificationWindow.Show($"{message} {ex.Message}", this);
            }
            finally
            {
                await TrackInfoToast.StopLoadingAsync();
            }
        }
        private static async Task<string?> FetchLrcFromLrcLib(string artist, string title)
        {
            using HttpClient client = new();
            try
            {
                string url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}";

                client.DefaultRequestHeaders.Add("User-Agent", "QAMP-MusicPlayer (https://github.com/d3solat1on/QAMP)");

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("syncedLyrics", out var synced) && !string.IsNullOrEmpty(synced.GetString()))
                {
                    return synced.GetString();
                }

                if (root.TryGetProperty("plainLyrics", out var plain))
                {
                    return plain.GetString();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LRCLIB Error: {ex.Message}");
            }
            return null;
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (Owner != null && Owner.IsVisible)
            {
                Owner.Focus();
                Owner.Activate();
            }
            Services.MemoryOptimizer.RunAsync(this.Dispatcher);
        }
    }
}