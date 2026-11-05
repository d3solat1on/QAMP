using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using static QAMP.Dialogs.NotificationWindow;

namespace QAMP;

public partial class MainWindow
{
    private bool _restoringPlaylistFolderExpansion;

    private void CreateFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var library = MusicLibrary.Instance;
        string baseName = Application.Current.FindResource("LngNewFolder") as string ?? "New Folder";
        string title = baseName;
        int suffix = 1;
        while (library.PlaylistFolders.Any(folder =>
                   string.Equals(folder.Title, title, StringComparison.OrdinalIgnoreCase)))
        {
            title = $"{baseName} ({suffix++})";
        }

        var folder = new PlaylistFolder
        {
            Title = title,
            EditingTitle = title,
            IsExpanded = true,
            Id = DatabaseService.CreatePlaylistFolder(title)
        };
        library.PlaylistFolders.Add(folder);
        library.RefreshPlaylistSidebarItems();

        PlaylistsListBox.UpdateLayout();
        if (PlaylistsListBox.ItemContainerGenerator.ContainerFromItem(folder) is TreeViewItem container)
        {
            container.IsExpanded = true;
        }
        string createdMessage = Application.Current.FindResource("LngCreatedFolder") as string
                                ?? "Folder \"{0}\" created";
        _ = MyToast.ShowAsync(string.Format(createdMessage, title));
    }

    private void PlaylistTree_Loaded(object sender, RoutedEventArgs e)
    {
        MusicLibrary.Instance.PlaylistSidebarItems.CollectionChanged -= PlaylistSidebarItems_CollectionChanged;
        MusicLibrary.Instance.PlaylistSidebarItems.CollectionChanged += PlaylistSidebarItems_CollectionChanged;
        RestoreFolderExpansionStates();
    }

    private void PlaylistSidebarItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(RestoreFolderExpansionStates, DispatcherPriority.Loaded);
    }

    private void RestoreFolderExpansionStates()
    {
        PlaylistsListBox.UpdateLayout();
        _restoringPlaylistFolderExpansion = true;
        foreach (var folder in MusicLibrary.Instance.PlaylistFolders)
        {
            if (PlaylistsListBox.ItemContainerGenerator.ContainerFromItem(folder) is TreeViewItem item)
            {
                item.IsExpanded = folder.IsExpanded;
            }
        }
        _restoringPlaylistFolderExpansion = false;
    }

    private void PlaylistFolder_Expanded(object sender, RoutedEventArgs e)
    {
        if (_restoringPlaylistFolderExpansion ||
            sender is not TreeViewItem { DataContext: PlaylistFolder folder })
        {
            return;
        }

        folder.IsExpanded = true;
        DatabaseService.SetPlaylistFolderExpanded(folder.Id, true);
    }

    private void PlaylistFolder_Collapsed(object sender, RoutedEventArgs e)
    {
        if (_restoringPlaylistFolderExpansion ||
            sender is not TreeViewItem { DataContext: PlaylistFolder folder })
        {
            return;
        }

        folder.IsExpanded = false;
        DatabaseService.SetPlaylistFolderExpanded(folder.Id, false);
    }

    private void FolderTitle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement element &&
            element.DataContext is PlaylistFolder folder)
        {
            BeginFolderRename(folder);
            e.Handled = true;
        }
    }

    private void RenamePlaylistFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: PlaylistFolder folder })
        {
            BeginFolderRename(folder);
        }
    }

    private void BeginFolderRename(PlaylistFolder folder)
    {
        folder.EditingTitle = folder.Title;
        folder.IsEditing = true;
        PlaylistsListBox.UpdateLayout();

        if (FindTreeViewItem(PlaylistsListBox, folder) is { } item)
        {
            item.IsSelected = true;
            if (FindVisualChild<TextBox>(item, "FolderNameEditor") is { } textBox)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }, DispatcherPriority.Input);
            }
        }
    }

    private void FolderTitle_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: PlaylistFolder folder } textBox) return;

        if (e.Key == Key.Enter)
        {
            SaveFolderRename(folder);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            folder.EditingTitle = folder.Title;
            folder.IsEditing = false;
            e.Handled = true;
        }
    }

    private void FolderTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: PlaylistFolder folder } && folder.IsEditing)
        {
            SaveFolderRename(folder);
        }
    }

    private void SaveFolderRename(PlaylistFolder folder)
    {
        string title = folder.EditingTitle.Trim();
        if (title.Length == 0)
        {
            folder.EditingTitle = folder.Title;
            folder.IsEditing = false;
            _ = MyToast.ShowAsync(Application.Current.FindResource("LngEmptyTitle") as string
                                  ?? "Folder name cannot be empty");
            return;
        }

        bool duplicate = MusicLibrary.Instance.PlaylistFolders.Any(existing =>
            existing.Id != folder.Id &&
            string.Equals(existing.Title, title, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            folder.EditingTitle = folder.Title;
            folder.IsEditing = false;
            _ = MyToast.ShowAsync(Application.Current.FindResource("LngFolderAlreadyExists") as string
                                  ?? "A folder with this name already exists");
            return;
        }

        if (!string.Equals(folder.Title, title, StringComparison.Ordinal))
        {
            DatabaseService.RenamePlaylistFolder(folder.Id, title);
            folder.Title = title;
        }

        folder.EditingTitle = title;
        folder.IsEditing = false;
    }

    private async void DeletePlaylistFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: PlaylistFolder folder }) return;

        string template = Application.Current.FindResource("LngDeleteFolderConfirm") as string
                          ?? "Delete folder \"{0}\"? Its playlists will be left unfiled.";
        if (NotificationWindow.Show(string.Format(template, folder.Title), this, NotificationMode.Confirm) != true) return;

        DatabaseService.DeletePlaylistFolder(folder.Id);
        var library = MusicLibrary.Instance;
        foreach (var playlist in folder.PlaylistInFolder)
        {
            playlist.FolderId = null;
        }
        library.PlaylistFolders.Remove(folder);
        library.RefreshPlaylistSidebarItems();

        if (library.CurrentPlaylist is { } currentPlaylist)
        {
            SelectPlaylist(currentPlaylist);
        }

        await MyToast.ShowAsync(Application.Current.FindResource("LngFolderDeleted") as string ?? "Folder deleted");
    }

    private async void PinPlaylistFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: PlaylistFolder folder }) return;

        bool isPinned = !folder.IsPinned;
        DatabaseService.UpdatePlaylistFolderPinnedState(folder.Id, isPinned);
        folder.IsPinned = isPinned;
        MusicLibrary.Instance.RefreshPlaylistSidebarItems();

        string resourceKey = isPinned ? "LngPinnedFolder" : "LngUnpinnedFolder";
        string fallback = isPinned ? "Folder pinned" : "Folder unpinned";
        await MyToast.ShowAsync(Application.Current.FindResource(resourceKey) as string ?? fallback);
    }

    private void PlaylistFolderMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        menuItem.Items.Clear();
        if (menuItem.DataContext is not Playlist playlist) return;

        var folders = MusicLibrary.Instance.PlaylistFolders;
        if (folders.Count == 0)
        {
            menuItem.Items.Add(new MenuItem
            {
                Header = Application.Current.FindResource("LngNoPlaylistFolders") as string ?? "No folders",
                IsEnabled = false
            });
            return;
        }

        foreach (var folder in folders)
        {
            var folderItem = new MenuItem
            {
                Header = folder.Title,
                IsCheckable = true,
                IsChecked = playlist.FolderId == folder.Id
            };
            folderItem.Click += (_, _) => MovePlaylistToFolder(playlist, folder);
            menuItem.Items.Add(folderItem);
        }
    }

    private void RemovePlaylistFromFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: Playlist playlist })
        {
            MovePlaylistToFolder(playlist, null);
        }
    }

    private void MovePlaylistToFolder(Playlist playlist, PlaylistFolder? folder)
    {
        if (playlist.FolderId == folder?.Id) return;

        DatabaseService.SetPlaylistFolder(playlist.Id, folder?.Id);
        playlist.FolderId = folder?.Id;
        MusicLibrary.Instance.RefreshPlaylistSidebarItems();
        SelectPlaylist(playlist);
    }

    private void PlaylistTree_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(Playlist)) is Playlist playlist)
        {
            MovePlaylistToFolder(playlist, null);
            e.Handled = true;
        }
    }

    private void SelectPlaylist(Playlist? playlist)
    {
        if (playlist == null) return;

        var library = MusicLibrary.Instance;
        PlaylistsListBox.UpdateLayout();
        var folder = library.PlaylistFolders.FirstOrDefault(item => item.Id == playlist.FolderId);
        if (folder != null)
        {
            folder.IsExpanded = true;
            if (PlaylistsListBox.ItemContainerGenerator.ContainerFromItem(folder) is TreeViewItem folderContainer)
            {
                folderContainer.IsExpanded = true;
            }
        }

        PlaylistsListBox.UpdateLayout();
        if (FindTreeViewItem(PlaylistsListBox, playlist) is { } playlistContainer)
        {
            playlistContainer.IsSelected = true;
            playlistContainer.BringIntoView();
        }
    }

    private static TreeViewItem? FindTreeViewItem(ItemsControl parent, object target)
    {
        foreach (object item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem container) continue;

            if (ReferenceEquals(item, target))
            {
                return container;
            }

            if (item is PlaylistFolder folder && target is Playlist playlist &&
                folder.PlaylistInFolder.Any(child => child.Id == playlist.Id))
            {
                container.IsExpanded = true;
                container.UpdateLayout();
            }

            var childContainer = FindTreeViewItem(container, target);
            if (childContainer != null) return childContainer;
        }

        return null;
    }
}
