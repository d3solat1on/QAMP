using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QAMP.Models;

namespace QAMP.Dialogs
{
    public partial class CreatePlaylistDialog : Window
    {
        public string PlaylistName { get; private set; }
        public string PlaylistDescription { get; private set; }
        public byte[] PlaylistCoverImage { get; private set; }

        public CreatePlaylistDialog(Playlist existingPlaylist = null)
        {
            InitializeComponent();
        }

        
        private void SelectCoverButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите обложку для плейлиста",
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp|Все файлы|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Загружаем изображение
                    var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
                    CoverImage.Source = bitmap;

                    // Конвертируем в байты для сохранения
                    using var stream = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read);
                    using var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    PlaylistCoverImage = memoryStream.ToArray();

                    // Меняем текст кнопки
                    // SelectCoverButton.Content = "Изменить обложку";
                }
                catch (Exception ex)
                {
                    
                    NotificationWindow.Show($"Ошибка при загрузке изображения: {ex.Message}", this);               
                }
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PlaylistNameTextBox.Text))
            {
                NotificationWindow.Show("Введите название плейлиста", this);
                return;
            }

            PlaylistName = PlaylistNameTextBox.Text.Trim();
            PlaylistDescription = PlaylistDescriptionTextBox.Text?.Trim() ?? "";

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