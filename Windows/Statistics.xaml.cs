using System.Windows;
using QAMP.Services;

namespace QAMP.Windows;

public partial class Statistics : Window
{
    private Timer? _debounceTimer;
    private const int DebounceDelayMs = 1000;

    public Statistics()
    {
        InitializeComponent();
        MinWidth = 800;
        MinHeight = 600;

        Loaded += async (s, e) =>
        {
            await RefreshAllStatisticsAsync();
            SubscribeToStatisticsChanges();
        };

        Closed += (s, e) => UnsubscribeFromStatisticsChanges();

        MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    private void SubscribeToStatisticsChanges()
    {
        DatabaseService.StatisticsChanged += OnStatisticsChanged;
        System.Diagnostics.Debug.WriteLine("Statistics window: subscribed to StatisticsChanged");
    }

    private void UnsubscribeFromStatisticsChanges()
    {
        DatabaseService.StatisticsChanged -= OnStatisticsChanged;
        _debounceTimer?.Dispose();
        System.Diagnostics.Debug.WriteLine("Statistics window: unsubscribed from StatisticsChanged");
    }

    private void OnStatisticsChanged()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async (_) =>
        {
            await Dispatcher.InvokeAsync(async () => await RefreshAllStatisticsAsync());
        }, null, DebounceDelayMs, Timeout.Infinite);
    }

    public async Task RefreshAllStatisticsAsync()
    {
        try
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
            var tracksWithoutListening = await Task.Run(() => DatabaseService.GetTracksWithoutListening());

            await Dispatcher.InvokeAsync(() =>
            {
                TracksWithoutListening.Text = tracksWithoutListening;
                MostListenedArtistText.Text = LocalizationService.GetFormattedString("LngMostListenedArtistValue", mostListenedArtistText);
                PlaylistCountText.Text = playlistCount;
                TrackCountText.Text = trackCount;
                MostListenedTrackText.Text = LocalizationService.GetFormattedString("LngMostListenedTrackValue", mostListened);
                HighestBitrateText.Text = hiResKing;
                LongestTrackText.Text = longestTrack;
                ShortestTrackText.Text = shortestTrack;
                TotalLibrarySizeText.Text = totalLibrarySize;
                TotalLibraryWeightText.Text = totalLibraryWeight;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing statistics: {ex.Message}");
            await Dispatcher.InvokeAsync(() =>
            {
                MostListenedTrackText.Text = "Error loading statistics. Please try again.";
            });
        }
    }

    public void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
        MemoryOptimizer.RunAsync(Dispatcher);
    }
}