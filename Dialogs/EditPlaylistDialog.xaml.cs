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
                var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));

                if (Math.Abs(bitmap.PixelWidth - bitmap.PixelHeight) < 2)
                {
                    CoverImage.Source = bitmap;
                    if (DataContext is Playlist playlist)
                    {
                        playlist.CoverImage = BitmapSourceToByteArray(bitmap);
                    }
                }
                else
                {
                    var cropper = new ImageCropperDialog(openFileDialog.FileName)
                    {
                        Owner = GetWindow(this)
                    };

                    if (cropper.ShowDialog() == true && cropper.ResultImage != null)
                    {
                        CoverImage.Source = cropper.ResultImage;

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
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(stream);
            return stream.ToArray();
        }

        private void EditPlaylist_Click(object sender, RoutedEventArgs e)
        {
            string newName = PlaylistNameTextBox.Text.Trim();

            if (DataContext is not Playlist currentPlaylist) return;

            if (string.IsNullOrWhiteSpace(newName))
            {
                string message = Application.Current.Resources["LngNameCannotBeEmpty"] as string ?? "Название не может быть пустым!";
                NotificationWindow.Show(message, this);
                return;
            }

            _ = new DatabaseService();

            if (DatabaseService.PlaylistExists(newName, currentPlaylist.Id))
            {
                string message = Application.Current.Resources["LngPlaylistNameExists"] as string ?? "Плейлист с таким названием уже существует!";
                NotificationWindow.Show(message, this);
                return;
            }

            DatabaseService.UpdatePlaylist(
                currentPlaylist.Id,
                newName,
                PlaylistDescriptionTextBox.Text,
                currentPlaylist.CoverImage
            );

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}