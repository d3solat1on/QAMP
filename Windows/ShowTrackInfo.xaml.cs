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
using QAMP.Services;
using System.Text.Json;


namespace QAMP.Windows
{
    public partial class ShowTrackInfo : Window
    {
        private readonly Track _track;

        private static bool IsInvalidSearchValue(string? value, string localizedUnknown) =>
            string.IsNullOrWhiteSpace(value) ||
            value.Equals(localizedUnknown, StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

        private static bool IsInvalidExecutor(string? executor) =>
            IsInvalidSearchValue(executor, "Неизвестный");

        private static bool IsInvalidTrackName(string? name) =>
            IsInvalidSearchValue(name, "Неизвестно");

        public ShowTrackInfo(Track track)
        {
            InitializeComponent();
            _track = track;
            DataContext = track;
            Loaded += ShowTrackInfo_Loaded;
            KeyDown += (s, e) =>
            {
                var config = SettingsManager.Instance.Config;
                if (config?.Hotkeys == null) return;

                var targetHotkey = config.Hotkeys.FirstOrDefault(h => h.Action == HotkeyAction.ShowTrackInfo);

                if (targetHotkey != null)
                {
                    Key pressedKey = (e.Key == Key.System) ? e.SystemKey : e.Key;

                    if (pressedKey == targetHotkey.Key && Keyboard.Modifiers == targetHotkey.Modifiers ||
                        pressedKey == Key.Escape)
                    {
                        Close();
                        e.Handled = true;
                    }
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

        private void ShowPlayHistory_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new PlaybackHistoryWindow(_track)
            {
                Owner = this
            };
            historyWindow.ShowDialog();
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
                    int decodeStream = PlayerService.CreateDecodeStreamFromFile(filePath);

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
                App.LogException(ex, "DetectBPM_Click");
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string messageSavingTags = (string)Application.Current.FindResource("LngSavingTags");
                TrackInfoToast.StartLoading(messageSavingTags);
                SaveButton.IsEnabled = false;
                bool isCurrentTrack = PlayerService.Instance.CurrentTrack?.Path == _track.Path;
                if (isCurrentTrack)
                {
                    PlayerService.Instance.PrepareForTagEdit();
                }
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

                DatabaseService.UpdateTrackMetadata(_track);

                if (isCurrentTrack)
                {
                    PlayerService.Instance.ResumeAfterTagEdit(_track);
                }

                string message = (string)Application.Current.FindResource("LngTagsSaved");
                await TrackInfoToast.ShowAsync(message);
                EditModeButton.IsChecked = false;
                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.RefreshSingleTrackInUI(_track);
                }
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message} {ex.Message}", this, NotificationWindow.NotificationMode.Info);
                System.Diagnostics.Debug.WriteLine($"Error saving tags: {ex}");
                App.LogException(ex, "SaveClickShowTrackInfo");
            }
            finally
            {
                SaveButton.IsEnabled = true;
                await TrackInfoToast.StopLoadingAsync();
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
                    App.LogException(ex, "ExtractCover_Click");
                }
            }
        }

        private async void RemoveCover_Click(object sender, RoutedEventArgs e)
        {
            bool isCurrentTrack = PlayerService.Instance.CurrentTrack?.Path == _track.Path;
            if (isCurrentTrack)
            {
                PlayerService.Instance.PrepareForTagEdit();
            }

            try
            {
                using (var file = TagLib.File.Create(_track.Path))
                {
                    file.Tag.Pictures = [];
                    file.Save();
                }

                _track.CoverImage = [];

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.RefreshSingleTrackInUI(_track);
                }

                string message = (string)Application.Current.FindResource("LngCoverRemoved");
                await TrackInfoToast.ShowAsync(message);
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message} {ex.Message}", this);
                App.LogException(ex, "RemoveCover_Click");
            }
            finally
            {
                if (isCurrentTrack)
                {
                    PlayerService.Instance.ResumeAfterTagEdit(_track);
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
                    string message = (string)Application.Current.FindResource("LngError");
                    NotificationWindow.Show($"{message} {ex.Message}", this);
                    App.LogException(ex, "ChangeCover_Click");
                }
            }
        }


        private void LyricsBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                var lyricsWindow = new LyricsWindow(_track)
                {
                    Owner = this
                };
                lyricsWindow.ShowDialog();
            }
        }

        private async void SearchMetadata_Click(object sender, RoutedEventArgs e)
        {
            var track = _track;
            if (IsInvalidExecutor(track.Executor) || IsInvalidTrackName(track.Name))
            {
                string message = (string)Application.Current.FindResource("LngNoDataToSeacrh");
                NotificationWindow.Show(message, this);
                return;
            }

            string messageSearching = (string)Application.Current.FindResource("LngSearchMusicBrainz");
            TrackInfoToast.StartLoading(messageSearching);

            try
            {
                var meta = await FetchMetadataFromMusicBrainzAsync(_track.Executor, _track.Name);

                await TrackInfoToast.StopLoadingAsync();

                if (meta != null)
                {
                    if (!string.IsNullOrEmpty(meta.Album) && string.IsNullOrEmpty(_track.Album))
                        _track.Album = meta.Album;

                    if (meta.Year > 0 && _track.Year == 0)
                        _track.Year = meta.Year;

                    if (!string.IsNullOrEmpty(meta.Genre) && string.IsNullOrEmpty(_track.Genre))
                        _track.Genre = meta.Genre;

                    string message = (string)Application.Current.FindResource("LngSuccessfully");
                    await TrackInfoToast.ShowAsync(message);
                    AlbumTextBox.Text = meta.Album ?? string.Empty;
                    YearTextBox.Text = meta.Year > 0 ? meta.Year.ToString() : string.Empty;
                    GenreTextBox.Text = meta.Genre ?? string.Empty;
                }
                else
                {
                    string message = (string)Application.Current.FindResource("LngUnsuccessful");
                    await TrackInfoToast.ShowAsync(message);
                }
            }
            catch (Exception ex)
            {
                await TrackInfoToast.StopLoadingAsync();
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message}: {ex.Message}", this);
            }
        }
        public static async Task<MusicBrainzResult?> FetchMetadataFromMusicBrainzAsync(string artist, string title)
        {
            using HttpClient client = new();
            try
            {
                string query = $"artist:\"{artist}\" AND recording:\"{title}\"";
                string url = $"https://musicbrainz.org/ws/2/recording/?query={Uri.EscapeDataString(query)}&fmt=json";

                client.DefaultRequestHeaders.Add("User-Agent", "QAMP-MusicPlayer/1.0 (https://github.com/d3solat1on/QAMP)");

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("recordings", out var recordings) && recordings.GetArrayLength() > 0)
                {
                    var bestMatch = recordings[0];
                    var result = new MusicBrainzResult();

                    if (bestMatch.TryGetProperty("releases", out var releases) && releases.GetArrayLength() > 0)
                    {
                        var firstRelease = releases[0];
                        if (firstRelease.TryGetProperty("title", out var albumTitle))
                        {
                            result.Album = albumTitle.GetString();
                        }

                        if (firstRelease.TryGetProperty("date", out var dateProp))
                        {
                            string? dateStr = dateProp.GetString();
                            if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 4)
                            {
                                if (int.TryParse(dateStr.AsSpan(0, 4), out int year))
                                {
                                    result.Year = year;
                                }
                            }
                        }
                    }

                    if (bestMatch.TryGetProperty("tags", out var tags) && tags.GetArrayLength() > 0)
                    {
                        var firstTag = tags[0];
                        if (firstTag.TryGetProperty("name", out var tagName))
                        {
                            result.Genre = tagName.GetString();
                        }
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MusicBrainz API Error: {ex.Message}");
            }

            return null;
        }
        private async void SearchCover_Click(object sender, RoutedEventArgs e)
        {
            var track = _track;
            if (IsInvalidExecutor(track.Executor) || IsInvalidTrackName(track.Name))
            {
                string message = (string)Application.Current.FindResource("LngNoDataToSeacrh");
                NotificationWindow.Show(message, this);
                return;
            }

            string messageSearching = (string)Application.Current.FindResource("LngSearchingCover");
            TrackInfoToast.StartLoading(messageSearching);
            byte[]? coverBytes = null;
            try
            {
                coverBytes = await FetchAlbumCoverFromItunesAsync(track.Executor!, track.Album, track.Name!);
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message} {ex.Message}", this);
                App.LogException(ex, "SearchCover_Click");
                return;
            }
            finally
            {
                await TrackInfoToast.StopLoadingAsync();
            }

            if (coverBytes is not { Length: > 0 })
            {
                string message = (string)Application.Current.FindResource("LngCoverNotFound");
                await TrackInfoToast.ShowAsync(message);
                return;
            }

            try
            {
                SaveCoverToTrackFile(track.Path, coverBytes, "image/jpeg");
                track.CoverImage = coverBytes;

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.RefreshSingleTrackInUI(track);
                }

                string successMessage = (string)Application.Current.FindResource("LngCoverArtUpdate");
                await TrackInfoToast.ShowAsync(successMessage);
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message} {ex.Message}", this);
                App.LogException(ex, "SearchCover_Click");
            }
        }
        public void SaveCoverToTrackFile(string filePath, byte[] coverBytes, string mimeType = "image/jpeg")
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                throw new FileNotFoundException("The audio file for the cover could not be found.", filePath);
            if (coverBytes == null || coverBytes.Length == 0)
                throw new ArgumentException("Cover image data is empty.", nameof(coverBytes));

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            var fileAbstraction = new StreamFileAbstraction(filePath, fileStream, fileStream);

            using var file = TagLib.File.Create(fileAbstraction);

            IPicture picture = new Picture([.. coverBytes])
            {
                Type = PictureType.FrontCover, // Указываем, что это обложка (лицевая сторона)
                MimeType = mimeType            // "image/jpeg" или "image/png"
            };

            file.Tag.Pictures = [picture];

            file.Save();

            if (FindName("CoverImage") is System.Windows.Controls.Image img)
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                using var imageStream = new MemoryStream(coverBytes);
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = imageStream;
                bitmap.EndInit();
                bitmap.Freeze();
                img.Source = bitmap;
            }
        }
        public static async Task<byte[]?> FetchAlbumCoverFromItunesAsync(string artist, string album) =>
            await FetchAlbumCoverFromItunesAsync(artist, album, album);

        public static async Task<byte[]?> FetchAlbumCoverFromItunesAsync(string artist, string? album, string title)
        {
            using HttpClient client = new();
            try
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("QAMP-MusicPlayer (https://github.com/d3solat1on/QAMP)");

                if (!IsInvalidSearchValue(album, "Неизвестный"))
                {
                    var albumResults = await SearchItunesArtworkAsync(client, $"{artist} {album}", artist, album!, "album", "collectionName");
                    if (albumResults != null)
                        return albumResults;
                }

                return await SearchItunesArtworkAsync(client, $"{artist} {title}", artist, title, "song", "trackName");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iTunes API Error: {ex.Message}");
                App.LogException(ex, "FetchAlbumCoverFromItunesAsync");
            }

            return null;
        }

        private static async Task<byte[]?> SearchItunesArtworkAsync(
            HttpClient client,
            string term,
            string artist,
            string expectedTitle,
            string entity,
            string titleProperty)
        {
            string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(term)}&entity={entity}&limit=10";
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array)
                return null;

            string normalizedArtist = NormalizeSearchText(artist);
            string normalizedTitle = NormalizeSearchText(expectedTitle);
            var candidates = results.EnumerateArray()
                .OrderByDescending(result =>
                {
                    string resultArtist = result.TryGetProperty("artistName", out var artistProp)
                        ? NormalizeSearchText(artistProp.GetString())
                        : string.Empty;
                    string resultTitle = result.TryGetProperty(titleProperty, out var titleProp)
                        ? NormalizeSearchText(titleProp.GetString())
                        : string.Empty;
                    return (resultArtist == normalizedArtist ? 2 : 0) +
                           (resultTitle == normalizedTitle ? 1 : 0);
                });

            foreach (var result in candidates)
            {
                if (!result.TryGetProperty("artworkUrl100", out var artworkProp))
                    continue;

                string? artworkUrl = artworkProp.GetString();
                if (string.IsNullOrWhiteSpace(artworkUrl))
                    continue;

                string highResolutionUrl = artworkUrl.Replace("100x100bb", "600x600bb", StringComparison.OrdinalIgnoreCase);
                using var imageResponse = await client.GetAsync(highResolutionUrl);
                if (!imageResponse.IsSuccessStatusCode)
                    continue;

                byte[] imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                if (imageBytes.Length > 0)
                    return imageBytes;
            }

            return null;
        }

        private static string NormalizeSearchText(string? value) =>
            string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();
        private async void SearchLyrics_Click(object sender, RoutedEventArgs e)
        {
            var track = _track;
            if (IsInvalidExecutor(track.Executor) || IsInvalidTrackName(track.Name))
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
                    await TrackInfoToast.StopLoadingAsync();
                }
                else
                {
                    await TrackInfoToast.StopLoadingAsync();
                    string message = (string)Application.Current.FindResource("LngTextNotFound");
                    await TrackInfoToast.ShowAsync(message);
                }
            }
            catch (Exception ex)
            {
                await TrackInfoToast.StopLoadingAsync();
                string message = (string)Application.Current.FindResource("LngError");
                NotificationWindow.Show($"{message} {ex.Message}", this);
                App.LogException(ex, "SearchLyrics_Click");
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
                App.LogException(ex, "FetchLrcFromLrcLib");
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
    public class MusicBrainzResult
    {
        public string? Album { get; set; }
        public int Year { get; set; }
        public string? Genre { get; set; }
    }
}