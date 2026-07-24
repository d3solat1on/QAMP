using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using QAMP.Models;

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

        var searchResults = new List<Track>();
        string searchLower = searchQuery.ToLower();

        foreach (var playlist in ViewModels.MusicLibrary.Instance.Playlists)
        {
            foreach (var track in playlist.Tracks)
            {
                if (track.Name.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                    track.Executor.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                    track.Album.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                    track.Genre.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!searchResults.Any(t => t.Path == track.Path))
                    {
                        searchResults.Add(track);
                    }
                }
            }
        }
        ShowSearchResults(searchResults, searchQuery);
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

        var searchPlaylist = new Playlist
        {
            Name = namePL,
            Description = formattedDescription,
            Tracks = new ObservableCollection<Track>(results),
            CoverImage = null
        };

        ViewModels.MusicLibrary.Instance.CurrentPlaylist = searchPlaylist;
        TracksDataGrid.ItemsSource = searchPlaylist.Tracks;
    }
    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        if (PlaylistsListBox.SelectedItem is Playlist currentPlaylist)
        {
            TracksDataGrid.ItemsSource = currentPlaylist.Tracks;
        }
    }
}