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

            string message = (string)Application.Current.FindResource("LngTextSaved");

            await TrackInfoToast.ShowAsync(message);
            Close();
        }
        catch (Exception ex)
        {
            string message = (string)Application.Current.FindResource("LngErorr");

            NotificationWindow.Show($"{message} {ex.Message}", this);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
        Services.MemoryOptimizer.RunAsync(this.Dispatcher);
    }
}