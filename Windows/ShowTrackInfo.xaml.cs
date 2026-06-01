using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using TagLib;
using QAMP.Dialogs;
using QAMP.Models;
using Microsoft.Win32;
using System.Net.Http;


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
                    NotificationWindow.Show("Папка с файлом не найдена", this);
                }
            }
            catch (Exception ex)
            {
                NotificationWindow.Show($"Ошибка открытия папки: {ex.Message}", this);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var file = TagLib.File.Create(_track.Path))
                {
                    file.Tag.Title = _track.Name;
                    file.Tag.Performers = [_track.Executor];
                    file.Tag.Album = _track.Album;
                    file.Tag.AlbumArtists = [_track.AlbumArtist];
                    file.Tag.Genres = [_track.Genre];
                    file.Tag.Comment = _track.Comment;
                    file.Tag.Lyrics = _track.Lyrics;
                    file.Tag.Composers = [_track.Composer];
                    file.Tag.Track = (uint)_track.TrackNumber;

                    if (uint.TryParse(_track.Year.ToString(), out uint year))
                        file.Tag.Year = year;

                    file.Save();
                }

                Services.DatabaseService.UpdateTrackMetadata(_track); // <-- дополнительно

                await TrackInfoToast.ShowAsync("Теги сохранены!");

                EditModeButton.IsChecked = false;
            }
            catch (Exception ex)
            {
                NotificationWindow.Show($"Ошибка: {ex.Message}", this, NotificationWindow.NotificationMode.Info);
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
                NotificationWindow.Show("В треке нет встроенной обложки.", this);
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
                    await TrackInfoToast.ShowAsync("Обложка извлечена!");
                }
                catch (Exception ex)
                {
                    NotificationWindow.Show($"Ошибка сохранения: {ex.Message}", this);
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
                    await TrackInfoToast.ShowAsync("Обложка обновлена! Перезапустите трек.");
                }
                catch (Exception ex)
                {
                    NotificationWindow.Show($"Ошибка: {ex.Message}", this);
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
                NotificationWindow.Show("Недостаточно данных для поиска", this);
                return;
            }

            TrackInfoToast.StartLoading("Поиск в LRCLIB");

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
                        await TrackInfoToast.ShowAsync("Текст найден и сохранен!");
                        await TrackInfoToast.StopLoadingAsync();
                    }
                    catch (Exception ex)
                    {
                        await TrackInfoToast.StopLoadingAsync();
                        NotificationWindow.Show($"Ошибка сохранения файла: {ex.Message}", this);
                    }
                }
                else
                {
                    await TrackInfoToast.ShowAsync("Текст не найден.");
                }
            }
            catch (Exception ex)
            {
                await TrackInfoToast.StopLoadingAsync();
                NotificationWindow.Show($"Ошибка: {ex.Message}", this);
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
        }
    }
}