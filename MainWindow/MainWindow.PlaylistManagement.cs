using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using QAMP.Windows;
using static QAMP.Dialogs.NotificationWindow;

namespace QAMP;

public partial class MainWindow
{
    private void DeletePlaylist_Click(object sender, RoutedEventArgs e) => RemovePlaylist_Click(sender, e);

    private async void RemovePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var selectedPlaylist = (sender as MenuItem)?.DataContext as Playlist ??
                               PlaylistsListBox.SelectedItem as Playlist;
        if (selectedPlaylist != null)
        {
            if (selectedPlaylist.Name == MusicLibrary.FavoritesName)
            {
                string message = Application.Current.Resources["LngSystemPlaylistCannotBeDeleted"] as string ?? "Системный плейлист нельзя удалить";
                await MyToast.ShowAsync(message);
                return;
            }

            var confirmMessage = (Application.Current.Resources["LngRemovePlaylistConfirm"] as string ?? "Удалить плейлист \"{0}\"?")
                .Replace("{0}", selectedPlaylist.Name);

            if (NotificationWindow.Show(confirmMessage, this, NotificationMode.Confirm) == true)
            {
                var previouslySelectedPlaylist = MusicLibrary.Instance.CurrentPlaylist;

                DatabaseService.DeletePlaylist(selectedPlaylist.Id);

                var playlistToRemove = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == selectedPlaylist.Id);
                if (playlistToRemove != null)
                {
                    MusicLibrary.Instance.Playlists.Remove(playlistToRemove);
                }
                MusicLibrary.Instance.RefreshPlaylistSidebarItems();

                if (previouslySelectedPlaylist != null)
                {
                    var stillExistsPlaylist = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == previouslySelectedPlaylist.Id);
                    if (stillExistsPlaylist != null)
                    {
                        SelectPlaylist(stillExistsPlaylist);
                    }
                    else
                    {
                        if (MusicLibrary.Instance.Playlists.Count > 0)
                        {
                            SelectPlaylist(MusicLibrary.Instance.Playlists.FirstOrDefault());
                        }
                        else
                        {
                            MusicLibrary.Instance.CurrentPlaylist = null;
                        }
                    }
                }
                else
                {
                    if (MusicLibrary.Instance.Playlists.Count > 0)
                    {
                        SelectPlaylist(MusicLibrary.Instance.Playlists.FirstOrDefault());
                    }
                }

                string message = Application.Current.Resources["LngPlaylistDeleted"] as string ?? "Плейлист удален";
                await MyToast.ShowAsync(message);
            }
        }
    }
    private async void CreatePlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CreatePlaylistDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            long newId = DatabaseService.CreatePlaylist(
                dialog.PlaylistName,
                dialog.PlaylistDescription,
                dialog.PlaylistCoverImage);

            var newPlaylist = DatabaseService.GetPlaylistById((int)newId);

            if (newPlaylist != null)
            {
                MusicLibrary.Instance.AddNewPlaylist(newPlaylist);

                SelectPlaylist(newPlaylist);

                TracksDataGrid.ItemsSource = newPlaylist.Tracks;

                string toastTemplate = Application.Current.TryFindResource("LngCreatedPlaylist") as string
                                       ?? "Плейлист \"{0}\" создан";

                string message = string.Format(toastTemplate, dialog.PlaylistName);
                await MyToast.ShowAsync(message); string toastMessage = string.Format(toastTemplate, dialog.PlaylistName);
                await MyToast.ShowAsync(toastMessage);
            }
        }
    }
    private void EditPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Playlist selectedPlaylist)
        {
            var dialog = new EditPlaylistDialog
            {
                Owner = this,
                DataContext = selectedPlaylist
            };

            if (dialog.ShowDialog() == true)
            {
                MusicLibrary.Instance.RefreshSinglePlaylist(selectedPlaylist.Id);
                var updatedPlaylist = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == selectedPlaylist.Id);
                SelectPlaylist(updatedPlaylist);
                TracksDataGrid.ItemsSource = updatedPlaylist?.Tracks;
            }
        }
    }
    public async void RefreshPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var selectedPlaylist = (sender as MenuItem)?.DataContext as Playlist ??
                               PlaylistsListBox.SelectedItem as Playlist;
        if (selectedPlaylist != null)
        {
            var updatedPlaylist = DatabaseService.GetPlaylistById(selectedPlaylist.Id);
            if (updatedPlaylist != null)
            {
                MusicLibrary.Instance.UpdatePlaylist(updatedPlaylist);
                ApplySort(updatedPlaylist.SortType);
                UpdateNextTrackUI();
                string toastTemple = Application.Current.FindResource("LngPlaylistUpdate") as string ?? "Плейсит обновлен";
                string toastMessage = string.Format(toastTemple);
                await MyToast.ShowAsync(toastMessage);
            }
        }
    }
    private async void PinPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Playlist selectedPlaylist)
        {
            bool newPinnedState = !selectedPlaylist.IsPinned;
            DatabaseService.UpdatePlaylistPinnedState(selectedPlaylist.Id, newPinnedState);
            selectedPlaylist.IsPinned = newPinnedState;

            ICollectionView view = CollectionViewSource.GetDefaultView(MusicLibrary.Instance.Playlists);
            view?.Refresh();
            MusicLibrary.Instance.RefreshPlaylistSidebarItems();

            SelectPlaylist(selectedPlaylist);

            var message = newPinnedState ? (Application.Current.Resources["LngPinnedPlaylist"] as string ?? "Плейсит закреплен")
                                        : (Application.Current.Resources["LngUnpinnedPlaylist"] as string ?? "Плейсит откреплен");
            await MyToast.ShowAsync(message);
        }
    }
    private void Playlist_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed &&
            sender is TreeViewItem { DataContext: Playlist } item)
        {
            DragDrop.DoDragDrop(item, item.DataContext, DragDropEffects.Move);
        }
    }

    private void Playlist_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(Playlist)) is not Playlist droppedPlaylist ||
            sender is not TreeViewItem targetItem)
        {
            return;
        }

        if (targetItem.DataContext is PlaylistFolder folder)
        {
            MovePlaylistToFolder(droppedPlaylist, folder);
            e.Handled = true;
            return;
        }

        if (targetItem.DataContext is Playlist targetPlaylist)
        {
            if (droppedPlaylist != targetPlaylist &&
                droppedPlaylist.FolderId == targetPlaylist.FolderId &&
                droppedPlaylist.IsPinned == targetPlaylist.IsPinned &&
                AppSettings.CurrentPlaylistSort is PlaylistSortOrder.Custom or PlaylistSortOrder.Manual)
            {
                var playlists = MusicLibrary.Instance.Playlists;
                int oldIndex = playlists.IndexOf(droppedPlaylist);
                int newIndex = playlists.IndexOf(targetPlaylist);
                if (oldIndex != -1 && newIndex != -1)
                {
                    playlists.Move(oldIndex, newIndex);
                    DatabaseService.SavePlaylistsOrder(playlists);
                    MusicLibrary.Instance.RefreshPlaylistSidebarItems();
                    SelectPlaylist(droppedPlaylist);
                }
            }

            e.Handled = true;
        }
    }
    public void SortByButton_Click(object sender, RoutedEventArgs e)
    {
        var contextMenu = new ContextMenu
        {
            Style = Application.Current.FindResource("ModernContextMenu") as Style
        };

        foreach (PlaylistSortOrder sortOrder in Enum.GetValues<PlaylistSortOrder>())
        {
            if (sortOrder == PlaylistSortOrder.Manual) continue;

            string resourceKey = sortOrder switch
            {
                PlaylistSortOrder.NameAZ => "LngSortNameAZ",
                PlaylistSortOrder.NameZA => "LngSortNameZA",
                PlaylistSortOrder.CreatedDateNewest => "LngSortDateNewest",
                PlaylistSortOrder.CreatedDateOldest => "LngSortDateOldest",
                PlaylistSortOrder.Custom => "LngSortCustom",
                _ => "LngSortNone"
            };

            string headerText = Application.Current.FindResource(resourceKey) as string ?? "Без сортировки";

            var menuItem = new MenuItem
            {
                Header = headerText,
                Tag = sortOrder
            };

            menuItem.Click += (s, args) =>
            {
                ApplyPlaylistSorting(sortOrder);

                AppSettings.CurrentPlaylistSort = sortOrder;
                SettingsManager.Instance.Save();
                MusicLibrary.Instance.RefreshPlaylistSidebarItems();
                string toastMessage = Application.Current.FindResource("LngSortingChanged") as string ?? "Сортировка изменена";
                _ = MyToast.ShowAsync(toastMessage);
            };

            contextMenu.Items.Add(menuItem);
        }

        if (sender is FrameworkElement element)
        {
            contextMenu.PlacementTarget = element;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }
    }
    private static void ApplyPlaylistSorting(PlaylistSortOrder sortOrder)
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(MusicLibrary.Instance.Playlists);

        if (view == null) return;

        view.SortDescriptions.Clear();

        view.SortDescriptions.Add(new SortDescription("IsPinned", ListSortDirection.Descending));

        switch (sortOrder)
        {
            case PlaylistSortOrder.NameAZ:
                view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                break;

            case PlaylistSortOrder.NameZA:
                view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Descending));
                break;

            case PlaylistSortOrder.CreatedDateNewest:
                view.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Descending));
                break;

            case PlaylistSortOrder.CreatedDateOldest:
                view.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Ascending));
                break;

            case PlaylistSortOrder.Custom:
            case PlaylistSortOrder.Manual:
                view.SortDescriptions.Add(new SortDescription("OrderIndex", ListSortDirection.Ascending));
                break;
        }
        view.Refresh();
        MusicLibrary.Instance.RefreshPlaylistSidebarItems();
    }
    private void ShowPlaylistInfo_Click(object sender, RoutedEventArgs e)
    {
        var selectedPlaylist = (sender as MenuItem)?.DataContext as Playlist ??
                               PlaylistsListBox.SelectedItem as Playlist;
        if (selectedPlaylist != null)
        {
            var infoWindow = new ShowInfoPlaylist(selectedPlaylist)
            {
                Owner = this
            };
            infoWindow.Show();
        }
        else
        {
            string message = (string)Application.Current.FindResource("LngSelectPlaylistFirst");
            NotificationWindow.Show(message, this);
        }
    }
}