using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using Track = QAMP.Models.Track;

namespace QAMP
{
    public partial class MainWindow
    {
        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            if (Library.CurrentPlaylist == null)
            {
                _ = NotificationWindow.Show(Application.Current.FindResource("LngSelectPlaylistFirst") as string ?? "Please select a playlist first!", this);
                return;
            }
            var contextMenu = new ContextMenu();

            // По дате добавления
            var menuItemDate = new MenuItem { Header = Application.Current.FindResource("LngSortDateNewest") as string ?? "By date added" };
            menuItemDate.Click += (s, args) => ApplySort(TrackSortType.AddedDate, true);
            _ = contextMenu.Items.Add(menuItemDate);

            // По альбому 
            var menuItemAlbum = new MenuItem { Header = Application.Current.FindResource("LngSortAlbumAZ") as string ?? "By album (A-Z)" };
            menuItemAlbum.Click += (s, args) => ApplySort(TrackSortType.AlbumAZ, true);
            _ = contextMenu.Items.Add(menuItemAlbum);

            // По исполнителю 
            var menuItemExecutor = new MenuItem { Header = Application.Current.FindResource("LngSortExecutorAZ") as string ?? "By artist (A-Z)" };
            menuItemExecutor.Click += (s, args) => ApplySort(TrackSortType.ExecutorAZ, true);
            _ = contextMenu.Items.Add(menuItemExecutor);

            //По названию 
            var menuItemName = new MenuItem { Header = Application.Current.FindResource("LngSortNameAZ") as string ?? "By title (A-Z)" };
            menuItemName.Click += (s, args) => ApplySort(TrackSortType.NameAZ, true);
            _ = contextMenu.Items.Add(menuItemName);

            if (sender is Button button)
            {
                contextMenu.PlacementTarget = button;
                contextMenu.Placement = PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }

        private async void ApplySort(TrackSortType sortType, bool showNotification = false)
        {
            if (Library.CurrentPlaylist == null) return;

            Library.CurrentPlaylist.SortType = sortType;

            DatabaseService.UpdatePlaylistSortType(Library.CurrentPlaylist.Id, sortType);

            var sortedTracks = SortTracks([.. Library.CurrentPlaylist.Tracks], sortType);

            Library.CurrentPlaylist.Tracks.Clear();
            foreach (var track in sortedTracks)
            {
                Library.CurrentPlaylist.Tracks.Add(track);
            }

            TracksDataGrid.ItemsSource = null;
            TracksDataGrid.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<Track>(sortedTracks);

            if (showNotification)
            {
                string sortName = sortType switch
                {
                    TrackSortType.AddedDate => Application.Current.FindResource("LngSortDateNewest") as string ?? "by date added",
                    TrackSortType.AlbumAZ => Application.Current.FindResource("LngSortAlbumAZ") as string ?? "by album (A-Z)",
                    TrackSortType.ExecutorAZ => Application.Current.FindResource("LngSortExecutorAZ") as string ?? "by artist (A-Z)",
                    TrackSortType.NameAZ => Application.Current.FindResource("LngSortNameAZ") as string ?? "by name (A-Z)",
                    _ => "неизвестно"
                };
                string message = Application.Current.FindResource("LngPlaylistSorted") as string ?? $"Playlist sorted {sortName}";
                await MyToast.ShowAsync($"{message} {sortName}");
            }
        }

        /// <summary>
        /// Сортирует список треков по выбранному критерию
        /// </summary>
        private static List<Track> SortTracks(List<Track> tracks, TrackSortType sortType)
        {
            return sortType switch
            {
                TrackSortType.AddedDate => [.. tracks.OrderBy(t => t.AddedDate)],
                // Сначала по альбому, потом по номеру трека в альбоме
                TrackSortType.AlbumAZ => [.. tracks
                    .OrderBy(t => t.Album ?? "")
                    .ThenBy(t => t.TrackNumber)],
                // Сначала по исполнителю, потом по альбому, потом по номеру трека
                TrackSortType.ExecutorAZ => [.. tracks
                    .OrderBy(t => t.Executor ?? "")
                    .ThenBy(t => t.Album ?? "")
                    .ThenBy(t => t.TrackNumber)],
                TrackSortType.NameAZ => [.. tracks.OrderBy(t => t.Name ?? "")],
                _ => tracks
            };
        }
    }
}
