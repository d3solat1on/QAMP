using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using TagLib;
using QAMP.Dialogs;
using QAMP.Models;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using HtmlAgilityPack;
using System.Net;


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
            NotificationWindow.Show("Ищем текст на Genius...", this);

            // Передаем текущие данные из полей (вдруг ты их уже подправил)
            string foundLyrics = await FetchLyricsFromGenius(_track.Executor, _track.Name);

            if (!string.IsNullOrEmpty(foundLyrics) && !foundLyrics.StartsWith("Ошибка"))
            {
                _track.Lyrics = foundLyrics;

                // ПРИНУДИТЕЛЬНОЕ ОБНОВЛЕНИЕ (на случай, если Binding молчит)
                // Находим твой TextBox для лирики по имени (например, LyricsTextBox)
                if (FindName("LyricsTextBox") is System.Windows.Controls.TextBox tb)
                {
                    tb.Text = foundLyrics;
                }

                NotificationWindow.Show("Текст успешно загружен!", this);
            }
            else
            {
                NotificationWindow.Show(foundLyrics, this); // Покажет причину ошибки
            }
        }

        public static async Task<string> FetchLyricsFromGenius(string artist, string title)
        {
            string accessToken = "5Vqtv4BG4gXkIO9wQlrZbDpkRg_lt8PHs88WsvxIfgg5tJH5SZPpdDnSEH83powx";
            using HttpClient client = new();
            try
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                // 1. Поиск ID песни
                string query = Uri.EscapeDataString($"{artist} {title}");
                string searchUrl = $"https://api.genius.com/search?q={query}";
                var response = await client.GetStringAsync(searchUrl);
                var json = JObject.Parse(response);

                var sUrl = json["response"]["hits"]?.FirstOrDefault()?["result"]?["url"]?.ToString();
                if (string.IsNullOrEmpty(sUrl)) return "Ошибка: API Genius не нашло такую песню.";

                // Выведи ссылку в консоль отладки (Output в Visual Studio)
                Debug.WriteLine($"Найдена ссылка: {sUrl}");

                // 2. Загрузка страницы и парсинг текста
                var web = new HtmlWeb();
                var doc = await web.LoadFromWebAsync(sUrl);

                // Genius часто меняет структуру, но обычно текст лежит в контейнерах с атрибутом 'data-lyrics-container'
                var nodes = doc.DocumentNode.SelectNodes("//div[@data-lyrics-container='true']")
            ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'Lyrics__Container')]")
            ?? doc.DocumentNode.SelectNodes("//div[@id='lyrics-root']");

                if (nodes == null)
                {
                    // Если ничего не нашли, давай проверим, не попали ли мы на капчу или пустую страницу
                    return "Ошибка: Не удалось найти блок с текстом на странице.";
                }

                if (nodes == null) return "Не удалось извлечь текст (возможно, дизайн сайта изменился).";

                // Собираем текст, заменяя <br> на переносы строк
                string lyrics = "";
                foreach (var node in nodes)
                {
                    // Заменяем <br> на новую строку перед получением текста
                    var html = node.InnerHtml.Replace("<br>", "\n");
                    var tempDoc = new HtmlDocument();
                    tempDoc.LoadHtml(html);
                    lyrics += tempDoc.DocumentNode.InnerText + "\n";
                }

                return WebUtility.HtmlDecode(lyrics).Trim();
            }
            catch (Exception ex)
            {
                return $"Ошибка при поиске: {ex.Message}";
            }
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