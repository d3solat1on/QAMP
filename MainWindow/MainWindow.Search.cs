using System.Windows;
using System.Windows.Input;
using QAMP.Models;
using QAMP.ViewModels;

namespace QAMP;

public partial class MainWindow
{
    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch();
    }
    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            PerformSearch();
            e.Handled = true;
        }

        if (e.Key == Key.Escape)
        {
            ReturnToPrevPlaylist();
            e.Handled = true;
        }
    }

    private async void PerformSearch()
    {
        string searchQuery = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            string message = Application.Current.Resources["LngEnterTextToSearch"] as string ?? "Please enter text to search";
            await MyToast.ShowAsync(message);
            return;
        }

        List<Track> results = await Task.Run(() =>
        {
            string query = searchQuery.ToLowerInvariant();
            List<Track> matchedTracks = [];
            HashSet<string> seenPaths = [];

            foreach (Playlist playlist in ViewModels.MusicLibrary.Instance.Playlists)
            {
                foreach (Track track in playlist.Tracks)
                {
                    if (seenPaths.Contains(track.Path)) continue;

                    if ((track.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                        (track.Executor?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                        (track.Album?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                        (track.Genre?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        seenPaths.Add(track.Path);
                        matchedTracks.Add(track);
                    }
                }
            }

            return matchedTracks;
        });

        ShowSearchResults(results, searchQuery);
    }
    private async void ShowSearchResults(List<Track> results, string searchQuery)
    {
        if (results.Count == 0)
        {
            string message = Application.Current.Resources["LngNoResultsFound"] as string ?? "No results found";
            await MyToast.ShowAsync(message);
            return;
        }

        PlaylistRS.Visibility = Visibility.Collapsed;

        string namePL = Application.Current.FindResource("LngSearchResults") as string ?? "Результаты поиска";

        string descriptionTemplate = Application.Current.FindResource("LngSearchDescription") as string
                                     ?? "По запросу: \"{0}\" найдено \"{1}\" треков";

        string formattedDescription = string.Format(descriptionTemplate, searchQuery, results.Count);
        var searchGeometry = Application.Current.TryFindResource("SearchOtherGeometry") as System.Windows.Media.Geometry;
        var brush = Application.Current.TryFindResource("AccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Black;
        var searchPlaylist = new Playlist
        {
            Name = namePL,
            Description = formattedDescription,
            Tracks = [with(results)],
            CoverImage = Services.GeometryToPng.GeometryToPngBytes(searchGeometry, width: 150, height: 150, brush)
        };
        ViewModels.MusicLibrary.Instance.CurrentPlaylist = searchPlaylist;
        TracksDataGrid.ItemsSource = searchPlaylist.Tracks;
    }
    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ReturnToPrevPlaylist();
    }
    private void ReturnToPrevPlaylist()
    {
        SearchBox.Clear();
        if (_previousPlaylist == null) return;
        MusicLibrary.Instance.CurrentPlaylist = _previousPlaylist;
        TracksDataGrid.ItemsSource = MusicLibrary.Instance.CurrentPlaylist.Tracks;
        PlaylistRS.Visibility = Visibility.Visible;
    }
}