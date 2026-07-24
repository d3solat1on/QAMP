using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using QAMP.Windows;

namespace QAMP;

public partial class MainWindow
{
    // M
    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        string message = (string)Application.Current.FindResource("LngAddToPlaylist");
        var subMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header is string header && header.Contains(message));
        if (subMenu == null) return;

        subMenu.ItemsSource = null;
        subMenu.Items.Clear();

        var playlists = MusicLibrary.Instance.Playlists;
        if (playlists == null || playlists.Count == 0)
        {
            string message1 = (string)Application.Current.FindResource("LngNoPlaylist");
            subMenu.Items.Add(new MenuItem { Header = message1, IsEnabled = false });
        }
        else
        {
            foreach (var p in playlists)
            {
                var item = new MenuItem { Header = p.Name, DataContext = p };
                item.Click += AddToSpecificPlaylist_Click;
                subMenu.Items.Add(item);
            }
        }
        subMenu.IsEnabled = true;
    }
    private async void AddToSpecificPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Playlist targetPlaylist)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                if (string.IsNullOrEmpty(selectedTrack.Path))
                {
                    string message = Application.Current.FindResource("LngErrorTrackPath") as string
                                     ?? "Неверный путь к треку!";
                    NotificationWindow.Show(message, this);
                    return;
                }

                if (IsTrackInPlaylist(targetPlaylist.Id, selectedTrack.Id))
                {
                    string template = Application.Current.FindResource("LngTrackAlreadyInPlaylist") as string
                                      ?? "Трек уже есть в плейлисте \"{0}\"!";

                    string formattedMessage = string.Format(template, targetPlaylist.Name);

                    NotificationWindow.Show(formattedMessage, this);
                    return;
                }

                DatabaseService.AddTrackToPlaylist(targetPlaylist.Id, selectedTrack.Path);
                var tracksFromDb = DatabaseService.GetTracksForPlaylist(targetPlaylist.Id);
                targetPlaylist.Tracks = new ObservableCollection<Track>(tracksFromDb);

                string toastTemplate = Application.Current.FindResource("LngAddedToPlaylistToast") as string
                                       ?? "Добавлено в \"{0}\"";

                string toastMessage = string.Format(toastTemplate, targetPlaylist.Name);

                await MyToast.ShowAsync(toastMessage);
            }
        }
    }
    private static bool IsTrackInPlaylist(int playlistId, int trackId)
    {
        try
        {
            return DatabaseService.IsTrackInPlaylist(playlistId, trackId);
        }
        catch (Exception)
        {
            return false;
        }
    }
    // B
    private void ShowTrackInfo_Click(object sender, RoutedEventArgs e)
    {
        var selectedTrack = TracksDataGrid.SelectedItem as Track;

        if (selectedTrack == null)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var grid = contextMenu?.PlacementTarget as DataGrid;
            selectedTrack = grid?.SelectedItem as Track;
        }

        if (selectedTrack != null)
        {
            var fullInfo = TagReader.GetFullTrackInfo(selectedTrack.Path);
            if (fullInfo != null)
            {
                fullInfo.PlayCount = selectedTrack.PlayCount;
                var infoWindow = new ShowTrackInfo(fullInfo) { Owner = this };
                infoWindow.Show();
            }
        }
        else
        {
            string message = Application.Current.Resources["LngSelectTrackFirst"] as string ?? "Please select a track first.";
            _ = MyToast.ShowAsync(message);
        }
    }
}