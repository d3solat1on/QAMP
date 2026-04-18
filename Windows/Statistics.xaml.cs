using System.Windows;
using QAMP.Services;
namespace QAMP.Windows;

public partial class Statistics : Window
{
    public Statistics()
    {        
        InitializeComponent();
        Loaded += async (s, e) => await RefreshAllStatisticsAsync();
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }
    public async Task RefreshAllStatisticsAsync()
    {
        var playlistCount = await Task.Run(() => DatabaseService.GetPlaylistCount().ToString());
        var trackCount = await Task.Run(() => DatabaseService.GetTrackCount().ToString());
        var mostListened = await Task.Run(() => DatabaseService.GetMostListenedTracks());
        var hiResKing = await Task.Run(() => DatabaseService.GetHiResKing());
        var longestTrack = await Task.Run(() => DatabaseService.GetLongestTrack());
        var shortestTrack = await Task.Run(() => DatabaseService.GetShortestTrack());
        var totalLibrarySize = await Task.Run(() => DatabaseService.GetTotalLibrarySize());
        var totalLibraryWeight = await Task.Run(() => DatabaseService.GetTotalLibraryWeight());
        var mostListenedArtistText = await Task.Run(() => DatabaseService.GetMostListenedArtist());
        var tracksWithoutListening = await Task.Run(() => DatabaseService.GetTracksWithoutListnenig());
        TracksWithoutListening.Text = $"Треки без прослушивания \n{tracksWithoutListening}";
        MostListenedArtistText.Text = $"Самый прослушиваемый исполнитель:{mostListenedArtistText}";
        PlaylistCountText.Text = playlistCount;
        TrackCountText.Text = trackCount;
        MostListenedTrackText.Text = mostListened;
        HighestBitrateText.Text = hiResKing;
        LongestTrackText.Text = longestTrack;
        ShortestTrackText.Text = shortestTrack;
        TotalLibrarySizeText.Text = totalLibrarySize;
        TotalLibraryWeightText.Text = totalLibraryWeight;
    }
    public void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}