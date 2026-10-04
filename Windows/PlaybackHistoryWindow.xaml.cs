using System.Globalization;
using System.Windows;
using System.Windows.Input;
using QAMP.Models;
using QAMP.Services;

namespace QAMP.Windows;

public partial class PlaybackHistoryWindow : Window
{
    public PlaybackHistoryWindow(Track track)
    {
        InitializeComponent();
        TrackTitleText.Text = $"{track.Executor} - {track.Name}";

        var playDates = DatabaseService.GetTrackPlayHistory(track.Id)
            .Select(date => date.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture))
            .ToList();

        PlayDates.ItemsSource = playDates;
        HistoryScrollViewer.Visibility = playDates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHistoryText.Visibility = playDates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}