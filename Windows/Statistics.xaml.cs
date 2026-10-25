using System.Windows;
using System.Windows.Input;
using QAMP.Models;
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
        AppConsole.Log("Statistics window: subscribed to StatisticsChanged");
    }

    private void UnsubscribeFromStatisticsChanges()
    {
        DatabaseService.StatisticsChanged -= OnStatisticsChanged;
        _debounceTimer?.Dispose();
        System.Diagnostics.Debug.WriteLine("Statistics window: unsubscribed from StatisticsChanged");
        AppConsole.Log("Statistics window: unsubscribed from StatisticsChanged");
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
            var firstTrackInLib = await Task.Run(() => DatabaseService.GetFirstTrackInLibrary());
            var lastTrackInLib = await Task.Run(() => DatabaseService.GetLastTrackInLibrary());
            var mostPopularAlbum = await Task.Run(() => DatabaseService.GetMostPopularAlbum());
            var mostPopularGenre = await Task.Run(() => DatabaseService.GetMostPopularGenre());
            var lastListenedTrack = await Task.Run(() => DatabaseService.GetLastListenedTrack());
            var genreStatistics = await Task.Run(() => DatabaseService.GetGenreStatistics());

            await Dispatcher.InvokeAsync(() =>
            {
                TracksWithoutListening.Text = tracksWithoutListening;
                MostListenedArtistText.Text = LocalizationService.GetFormattedString("LngMostListenedArtistValue", mostListenedArtistText);
                FirstTrackInLibText.Text = firstTrackInLib;
                LastTrackInLibText.Text = lastTrackInLib;
                MostPopularAlbum.Text = mostPopularAlbum;
                MostPopularGenre.Text = mostPopularGenre;
                PlaylistCountText.Text = playlistCount;
                TrackCountText.Text = trackCount;
                MostListenedTrackText.Text = LocalizationService.GetFormattedString("LngMostListenedTrackValue", mostListened);
                HighestBitrateText.Text = hiResKing;
                LongestTrackText.Text = longestTrack;
                ShortestTrackText.Text = shortestTrack;
                TotalLibrarySizeText.Text = totalLibrarySize;
                TotalLibraryWeightText.Text = totalLibraryWeight;
                LastListenedTrack.Text = lastListenedTrack;
                GenreStatisticsItems.ItemsSource = genreStatistics.Count == 0
                    ? [LocalizationService.GetString("LngNoGenreStatistics")]
                    : genreStatistics.Select(genre => LocalizationService.GetFormattedString(
                        "LngGenreStatisticsFormat",
                        genre.Genre,
                        genre.TrackCount,
                        genre.Percentage));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing statistics: {ex.Message}");
            AppConsole.Log($"Error refreshing statistics: {ex.Message}");
            await Dispatcher.InvokeAsync(() =>
            {
                MostListenedTrackText.Text = "Error loading statistics. Please try again.";
            });
        }
    }

    private async void CreatePlaylistWithTop50Tracks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DatabaseService.CreatePlaylistWithTop50Tracks();
            string message = Application.Current.Resources["LngSuccessfully"] as string ?? "Successfully";
            await StatisticsNotice.ShowAsync(message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating playlist with top 50 tracks: {ex.Message}");
            string message = Application.Current.Resources["LngUnsuccessful"] as string ?? "Unsuccessful";
            await StatisticsNotice.ShowAsync(message);
            AppConsole.Log($"Error creating playlist with top 50 tracks: {ex.Message}");
            App.LogException(ex, "Error creating playlist with top 50 tracks");
        }
    }

    private async void CreatePlaylistWithTracksNotListening(object sender, RoutedEventArgs e)
    {
        try
        {
            DatabaseService.CreatePlaylistTracksWithoutListening();
            string message = Application.Current.Resources["LngSuccessfully"] as string ?? "Successfully";
            await StatisticsNotice.ShowAsync(message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating playlist with tracks without listening: {ex.Message}");
            string message = Application.Current.Resources["LngUnsuccessful"] as string ?? "Unsuccessful";
            await StatisticsNotice.ShowAsync(message);
            AppConsole.Log($"Error creating playlist with tracks without listening: {ex.Message}");
            App.LogException(ex, "Error creating playlist with tracks without listening");
        }
    }

    public static void RefreshRightPanel(Playlist newPlaylist) //не RightPanel, а LeftPanel xd
    {
        if (newPlaylist != null)
        {
            ViewModels.MusicLibrary.Instance.Playlists.Add(newPlaylist);
        }
    }


    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            MemoryOptimizer.RunAsync(Dispatcher);
        }
    }

    public void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
        MemoryOptimizer.RunAsync(Dispatcher);
    }
}