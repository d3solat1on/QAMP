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
                Title = "Редактировать плейлист";
                PlaylistNameTextBox.Text = existingPlaylist.Name;
                PlaylistDescriptionTextBox.Text = existingPlaylist.Description;
                
                if (existingPlaylist.CoverImage != null && existingPlaylist.CoverImage.Length > 0)
                {
                    PlaylistCoverImage = existingPlaylist.CoverImage;
                    var bitmap = ByteArrayToBitmapImage(existingPlaylist.CoverImage);
                    CoverImage.Source = bitmap;
                    if (PlaceholderText != null) PlaceholderText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Title = "Создать плейлист";
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
                Title = "Выберите изображение для обложки",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. Загружаем изображение
                    var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
                    
                    // 2. Проверяем размер файла (не более 2 МБ)
                    // var fileInfo = new FileInfo(openFileDialog.FileName);
                    // if (fileInfo.Length > 2 * 1024 * 1024)
                    // {
                    //     NotificationWindow.Show("Изображение слишком большое. Максимальный размер - 2 МБ.", this);
                    //     return;
                    // }

                    // 3. Проверяем, является ли оно квадратным (допуск 2 пикселя)
                    if (Math.Abs(bitmap.PixelWidth - bitmap.PixelHeight) < 2)
                    {
                        // Изображение уже 1 к 1
                        CoverImage.Source = bitmap;
                        if (PlaceholderText != null) PlaceholderText.Visibility = Visibility.Collapsed;
                        
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
                            if (PlaceholderText != null) PlaceholderText.Visibility = Visibility.Collapsed;
                            
                            // Сохраняем обрезанное изображение
                            PlaylistCoverImage = BitmapSourceToByteArray(cropper.ResultImage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    NotificationWindow.Show($"Ошибка загрузки изображения: {ex.Message}", this);
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
                NotificationWindow.Show("Введите название плейлиста", this);
                return;
            }
            
            // Проверка на дубликат имени (если создаем новый)
            if (Title == "Создать плейлист")
            {
                if (DatabaseService.PlaylistExists(name, -1))
                {
                    NotificationWindow.Show("Плейлист с таким названием уже существует", this);
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