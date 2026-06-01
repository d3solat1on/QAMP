using System.Windows;
using QAMP.Dialogs;
using QAMP.Models;
namespace QAMP.Windows;
public partial class LyricsWindow : Window
{
    private readonly Track _track;

    public LyricsWindow(Track track)
    {
        InitializeComponent();
        _track = track;
        DataContext = _track;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using (var file = TagLib.File.Create(_track.Path))
            {
                file.Tag.Lyrics = FullLyricsEditor.Text;
                file.Save();
            }
            _track.Lyrics = FullLyricsEditor.Text; // Обновляем модель
            await TrackInfoToast.ShowAsync("Текст сохранен в файл!");
            Close();
        }
        catch (Exception ex)
        {
            NotificationWindow.Show($"Ошибка сохранения: {ex.Message}", this);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}