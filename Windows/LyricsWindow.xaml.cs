using System.Windows;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;

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
            SaveButton.IsEnabled = false;
            string messageSavingTags = (string)Application.Current.FindResource("LngTextSaved");
            TrackInfoToast.StartLoading(messageSavingTags);

            bool isCurrentTrack = PlayerService.Instance.CurrentTrack?.Path == _track.Path;

            if (isCurrentTrack)
            {
                PlayerService.Instance.PrepareForTagEdit();
            }

            await Task.Run(() =>
            {
                using (var file = TagLib.File.Create(_track.Path))
                {
                    file.Tag.Lyrics = FullLyricsEditor.Text; 
                    file.Save();
                }

                DatabaseService.UpdateTrackMetadata(_track);
            });

            _track.Lyrics = FullLyricsEditor.Text;

            if (isCurrentTrack)
            {
                PlayerService.Instance.ResumeAfterTagEdit(_track);
            }

            string message = (string)Application.Current.FindResource("LngTextSaved") ?? "Сохранено";
            await TrackInfoToast.ShowAsync(message);

            Close();
        }
        catch (Exception ex)
        {
            string message = (string)Application.Current.FindResource("LngError") ?? "Ошибка";
            NotificationWindow.Show($"{message} {ex.Message}", this);

            SaveButton.IsEnabled = true;
        }
        finally
        {
            await TrackInfoToast.StopLoadingAsync();
            SaveButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
        Services.MemoryOptimizer.RunAsync(this.Dispatcher);
    }
}