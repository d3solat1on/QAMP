using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QAMP.Models;
using QAMP.Services;
namespace QAMP.Dialogs
{

    public partial class EditPlaylistDialog : Window
    {

        public EditPlaylistDialog()
        {
            InitializeComponent();
        }
        private void SelectCoverButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // 1. Загружаем изображение для проверки размеров
                var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));

                // 2. Проверяем, является ли оно квадратным
                // Используем допуск (например, 1-2 пикселя), на случай микро-ошибок в размерах
                if (Math.Abs(bitmap.PixelWidth - bitmap.PixelHeight) < 2)
                {
                    // Изображение уже 1 к 1 — просто применяем его
                    CoverImage.Source = bitmap;
                    if (PlaceholderText != null) PlaceholderText.Visibility = Visibility.Collapsed;

                    // Сохраняем в данные плейлиста (конвертируем в байты)
                    if (DataContext is Playlist playlist)
                    {
                        playlist.CoverImage = BitmapSourceToByteArray(bitmap);
                    }
                }
                else
                {
                    // Изображение не квадратное — открываем кроппер
                    var cropper = new ImageCropperDialog(openFileDialog.FileName)
                    {
                        Owner = GetWindow(this)
                    };

                    if (cropper.ShowDialog() == true && cropper.ResultImage != null)
                    {
                        CoverImage.Source = cropper.ResultImage;
                        if (PlaceholderText != null) PlaceholderText.Visibility = Visibility.Collapsed;

                        if (DataContext is Playlist playlist)
                        {
                            playlist.CoverImage = BitmapSourceToByteArray(cropper.ResultImage);
                        }
                    }
                }
            }
        }
        public static byte[] BitmapSourceToByteArray(BitmapSource bitmapSource)
        {
            using var stream = new MemoryStream();
            // Используем PngBitmapEncoder для сохранения прозрачности и качества
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(stream);
            return stream.ToArray();
        }

        // Метод для кнопки "Сохранить" (обратите внимание на маленькую 'c' в 'click' в вашем XAML)
        private void EditPlaylist_Click(object sender, RoutedEventArgs e)
        {
            string newName = PlaylistNameTextBox.Text.Trim();

            if (DataContext is not Playlist currentPlaylist) return;

            if (string.IsNullOrWhiteSpace(newName))
            {
                NotificationWindow.Show("Название не может быть пустым!", this);
                return;
            }

            _ = new DatabaseService();

            // Проверяем через базу данных
            if (DatabaseService.PlaylistExists(newName, currentPlaylist.Id))
            {
                NotificationWindow.Show("Плейлист с таким названием уже существует!", this);
                return;
            }

            // Если всё ок, сохраняем изменения в базу
            DatabaseService.UpdatePlaylist(
                currentPlaylist.Id,
                newName,
                PlaylistDescriptionTextBox.Text,
                currentPlaylist.CoverImage
            );

            DialogResult = true;
        }

        // Метод для кнопки "Отмена"
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}