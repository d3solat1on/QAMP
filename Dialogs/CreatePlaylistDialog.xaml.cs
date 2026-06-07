using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QAMP.Models;
using QAMP.Services;

namespace QAMP.Dialogs
{
    public partial class CreatePlaylistDialog : Window
    {
        public string PlaylistName { get; private set; } = "";
        public string PlaylistDescription { get; private set; } = "";
        public byte[]? PlaylistCoverImage { get; private set; } 

        public CreatePlaylistDialog(Playlist? existingPlaylist = null)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            
            // Если редактируем существующий плейлист
            if (existingPlaylist != null)
            {
                Title = Application.Current.Resources["LngEditPlaylistTitle"] as string ?? "Редактировать плейлист";
                PlaylistNameTextBox.Text = existingPlaylist.Name;
                PlaylistDescriptionTextBox.Text = existingPlaylist.Description;
                
                if (existingPlaylist.CoverImage != null && existingPlaylist.CoverImage.Length > 0)
                {
                    PlaylistCoverImage = existingPlaylist.CoverImage;
                    var bitmap = ByteArrayToBitmapImage(existingPlaylist.CoverImage);
                    CoverImage.Source = bitmap;
                    PlaceholderText?.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Title = Application.Current.Resources["LngCreatePlaylistTitle"] as string ?? "Создать плейлист";
            }
        }

        private static BitmapImage ByteArrayToBitmapImage(byte[] imageData)
        {
            var bitmap = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                bitmap.BeginInit();
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = mem;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
        }

        private void SelectCoverButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = Application.Current.Resources["LngSelectCoverImage"] as string ?? "Выберите изображение для обложки",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. Загружаем изображение
                    var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
                    if (Math.Abs(bitmap.PixelWidth - bitmap.PixelHeight) < 2)
                    {
                        // Изображение уже 1 к 1
                        CoverImage.Source = bitmap;
                        PlaceholderText?.Visibility = Visibility.Collapsed;
                        
                        // Сохраняем в байты
                        PlaylistCoverImage = BitmapSourceToByteArray(bitmap);
                    }
                    else
                    {
                        // Изображение не квадратное — открываем кроппер
                        var cropper = new ImageCropperDialog(openFileDialog.FileName)
                        {
                            Owner = this
                        };

                        if (cropper.ShowDialog() == true && cropper.ResultImage != null)
                        {
                            CoverImage.Source = cropper.ResultImage;
                            PlaceholderText?.Visibility = Visibility.Collapsed;
                            
                            // Сохраняем обрезанное изображение
                            PlaylistCoverImage = BitmapSourceToByteArray(cropper.ResultImage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = (Application.Current.Resources["LngImageLoadError"] as string ?? "Ошибка загрузки изображения: {0}")
                        .Replace("{0}", ex.Message);
                    NotificationWindow.Show(errorMsg, this);
                }
            }
        }

        private static byte[] BitmapSourceToByteArray(BitmapSource bitmapSource)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем данные из полей
            string name = PlaylistNameTextBox.Text?.Trim() ?? "";
            string description = PlaylistDescriptionTextBox.Text?.Trim() ?? "";
            
            // Валидация
            if (string.IsNullOrWhiteSpace(name))
            {
                string message = Application.Current.Resources["LngPlaylistNameEmpty"] as string ?? "Пожалуйста, введите название плейлиста";
                NotificationWindow.Show(message, this);
                return;
            }
            
            // Проверка на дубликат имени (если создаем новый)
            if (Title == (Application.Current.Resources["LngCreatePlaylistTitle"] as string ?? "Создать плейлист"))
            {
                if (DatabaseService.PlaylistExists(name, -1))
                {
                    string message = Application.Current.Resources["LngPlaylistNameExists"] as string ?? "Плейлист с таким названием уже существует";
                    NotificationWindow.Show(message, this);
                    return;
                }
            }
            
            // Сохраняем данные
            PlaylistName = name;
            PlaylistDescription = description;
            
            // Закрываем диалог с успехом
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}