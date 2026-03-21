using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using QAMP.Dialogs;
using QAMP.Models;

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