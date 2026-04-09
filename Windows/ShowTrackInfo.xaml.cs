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
            if (string.IsNullOrEmpty(_track?.Path)) return;

            try
            {
                // Получаем директорию файла
                string directory = Path.GetDirectoryName(_track.Path);

                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Process.Start("explorer.exe", $"/select,\"{_track.Path}\"");
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

        private void Save_Click(object sender, RoutedEventArgs e)
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

                    // if (uint.TryParse(_track.TrackNumber.ToString(), out uint trackNum))
                    //     file.Tag.TrackNumber = trackNum;

                    // if (uint.TryParse(_track.Bpm.ToString(), out uint bpm))
                    //     file.Tag.BeatsPerMinute = bpm;

                    if (uint.TryParse(_track.Year.ToString(), out uint year))
                        file.Tag.Year = year;

                    file.Save();
                }

                NotificationWindow.Show("Теги сохранены!", this);

                EditModeButton.IsChecked = false;
            }
            catch (Exception ex)
            {
                NotificationWindow.Show($"Ошибка: {ex.Message}", this);
            }
        }

        private void ExtractCover_Click(object sender, RoutedEventArgs e)
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
                    NotificationWindow.Show("Обложка извлечена!", this);
                }
                catch (Exception ex)
                {
                    NotificationWindow.Show($"Ошибка сохранения: {ex.Message}", this);
                }
            }
        }

        private void ChangeCover_Click(object sender, MouseButtonEventArgs e)
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
                    NotificationWindow.Show("Обложка обновлена! Перезапустите трек.", this);
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
            NotificationWindow.Show("Searching in LRCLIB...", this);

            string lyrics = await FetchLrcFromLrcLib(_track.Executor, _track.Name);

            if (!string.IsNullOrEmpty(lyrics))
            {
                _track.Lyrics = lyrics;
                if (FindName("LyricsTextBox") is System.Windows.Controls.TextBox tb)
                {
                    tb.Text = lyrics;
                }
                try
                {
                    using (var file = TagLib.File.Create(_track.Path))
                    {
                        file.Tag.Lyrics = lyrics;
                        file.Save();
                    }
                    NotificationWindow.Show("Lyrics loaded and SAVED to file!", this);
                }
                catch (Exception ex)
                {
                    NotificationWindow.Show($"Loaded, but save failed: {ex.Message}", this);
                }
            }
            else
            {
                NotificationWindow.Show("Lyrics not found in LRCLIB database.", this);
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
                System.Diagnostics.Debug.WriteLine($"LRCLIB Error: {ex.Message}");
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
    }
}