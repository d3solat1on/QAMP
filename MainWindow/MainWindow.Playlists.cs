using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QAMP.Converters;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using static QAMP.Dialogs.NotificationWindow;
using Track = QAMP.Models.Track;

namespace QAMP
{
    public partial class MainWindow
    {
        private readonly SettingsManager _settingsManager = SettingsManager.Instance;
        private AppSettings AppSettings => _settingsManager.Config;
        private readonly LargeTrackImageConverter _imageConverter = new();
        private DispatcherTimer? _scrollCleanupTimer;
        private async Task ProcessDroppedPaths(string[] paths)
        {
            if (PlaylistsListBox.SelectedItem is not Playlist selectedPlaylist) return;
            if (MusicLibrary.Instance.CurrentPlaylist == null) return;

            Cursor = Cursors.Wait;

            try
            {
                var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".ape", ".opus", ".mpc", ".alac"
                };

                var allFiles = new List<string>();

                foreach (var path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        var folderFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(file => supportedExtensions.Contains(Path.GetExtension(file)));
                        allFiles.AddRange(folderFiles);
                    }
                    else if (File.Exists(path) && supportedExtensions.Contains(Path.GetExtension(path)))
                    {
                        allFiles.Add(path);
                    }
                }

                if (allFiles.Count == 0) return;

                var (tracksToAdd, addedCount) = await Task.Run(() =>
                {
                    var readTracks = TagReader.ReadTracksFromFiles([.. allFiles]);

                    var existingPaths = new HashSet<string>(
                        MusicLibrary.Instance.CurrentPlaylist.Tracks.Select(t => t.Path),
                        StringComparer.OrdinalIgnoreCase
                    );

                    var filteredTracks = new List<Track>();
                    var playlistId = MusicLibrary.Instance.CurrentPlaylist.Id;

                    foreach (var track in readTracks)
                    {
                        if (track != null && !existingPaths.Contains(track.Path))
                        {
                            DatabaseService.SaveTrackToPlaylist(playlistId, track);
                            filteredTracks.Add(track);
                        }
                    }

                    return (filteredTracks, filteredTracks.Count);
                });

                if (addedCount > 0)
                {
                    foreach (var track in tracksToAdd)
                    {
                        MusicLibrary.Instance.CurrentPlaylist.Tracks.Add(track);
                    }

                    if (MusicLibrary.Instance.PlayingPlaylist?.Id == selectedPlaylist.Id)
                    {
                        Player.AppendTracksToQueue(tracksToAdd);
                    }

                    if (selectedPlaylist.SortType != TrackSortType.AddedDate)
                    {
                        ApplySort(selectedPlaylist.SortType);
                    }
                    else
                    {
                        TracksDataGrid.ItemsSource = null;
                        TracksDataGrid.ItemsSource = selectedPlaylist.Tracks;
                    }

                    UpdateNextTrackUI();

                    string toastTemplate = Application.Current.FindResource("LngTracksAddedToast") as string ?? "Добавлено \"{0}\" треков";
                    string toastMessage = string.Format(toastTemplate, addedCount);
                    await MyToast.ShowAsync(toastMessage);
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "ProcessDroppedPaths");
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }
        }
        private void AddMusicButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                string filesTemplate = Application.Current.TryFindResource("LngMenuAddFilesTo") as string
                                       ?? "Добавить файлы в \"{0}\"";
                string folderTemplate = Application.Current.TryFindResource("LngMenuAddFolderTo") as string
                                        ?? "Добавить папку в \"{0}\"";

                MenuAddFilesPlaylist.Header = string.Format(filesTemplate, selectedPlaylist.Name);
                MenuAddFolderPlaylist.Header = string.Format(folderTemplate, selectedPlaylist.Name);

                MenuAddFilesPlaylist.Visibility = Visibility.Visible;
                MenuAddFolderPlaylist.Visibility = Visibility.Visible;
            }
            else
            {
                MenuAddFilesPlaylist.Visibility = Visibility.Collapsed;
                MenuAddFolderPlaylist.Visibility = Visibility.Collapsed;
            }

            AddMusicMenu.PlacementTarget = sender as Button;
            AddMusicMenu.IsOpen = true;
        }

        private void AddFilesToPlaylist_Click(object sender, RoutedEventArgs e) => AddFilesToCurrentPlaylist();
        private void AddFolderToPlaylist_Click(object sender, RoutedEventArgs e) => AddFolderToCurrentPlaylist();

        private async void AddFolderToCurrentPlaylist()
        {
            if (PlaylistsListBox.SelectedItem is not Playlist) return;
            if (MusicLibrary.Instance.CurrentPlaylist == null) return;

            var folderDialog = new OpenFolderDialog { Multiselect = true };
            if (folderDialog.ShowDialog() == true)
            {
                await ProcessDroppedPaths([folderDialog.FolderName]);
            }
        }
        private async void AddFilesToCurrentPlaylist()
        {
            if (PlaylistsListBox.SelectedItem is not Playlist selectedPlaylist) return;

            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".ape", ".opus", ".mpc", ".alac"
            };
            string extensionsPattern = string.Join(";", supportedExtensions.Select(ext => $"*{ext}"));

            var openFileDialog = new OpenFileDialog { Multiselect = true, Filter = $"Music Files|{extensionsPattern}" };
            if (openFileDialog.ShowDialog() == true)
            {
                await ProcessDroppedPaths(openFileDialog.FileNames);
            }
        }
        private async void RemoveFromPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var selectedPlaylist = Library.CurrentPlaylist;
            if (selectedPlaylist == null)
            {
                string message = Application.Current.Resources["LngSelectPlaylistFirst"] as string ?? "Сначала выберите плейлист";
                await MyToast.ShowAsync(message);
                return;
            }

            var selectedTracks = TracksDataGrid.SelectedItems.Cast<Track>().ToList();

            if (selectedTracks.Count > 0)
            {
                string trackName = selectedTracks.Count == 1
                    ? $"\"{selectedTracks[0].Name}\""
                    : $"{selectedTracks.Count} " + (Application.Current.Resources["LngTitlePlaylist"] as string ?? "треков");

                var confirmMessage = (Application.Current.Resources["LngRemoveFromPlaylistConfirm"] as string ?? "Удалить \"{0}\" из плейлиста \"{1}\"?")
                    .Replace("{0}", trackName)
                    .Replace("{1}", selectedPlaylist.Name);

                var result = NotificationWindow.Show(
                    confirmMessage,
                    this,
                    NotificationMode.Confirm);

                if (result == true)
                {
                    foreach (var track in selectedTracks)
                    {
                        bool isUsedElsewhere = DatabaseService.IsTrackInOtherPlaylists(track.Id, selectedPlaylist.Id);

                        DatabaseService.RemoveTrackFromPlaylist(selectedPlaylist.Id, track.Id);

                        if (!isUsedElsewhere)
                        {
                            DatabaseService.DeleteTrackCompletely(track.Id);
                        }

                        selectedPlaylist.Tracks.Remove(track);
                    }
                    TracksDataGrid.ItemsSource = null;
                    TracksDataGrid.ItemsSource = selectedPlaylist.Tracks;
                    UpdateNextTrackUI();
                    DatabaseService.OnStatisticsChanged();

                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                    return element;

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }



        

        

        // ?
        private void PlaylistsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selected)
            {
                MusicLibrary.Instance.CurrentPlaylist = selected;
                System.Diagnostics.Debug.WriteLine($"=== ПРОСМОТР ПЛЕЙЛИСТА: {selected.Name} ===");
                System.Diagnostics.Debug.WriteLine($"SortType из БД: {selected.SortType}");
                ApplySort(selected.SortType);

                if (selected.IsSystemPlaylist || selected.CoverImage == null)
                {
                    UpdateUpperPanelGradientForFavorites();
                }
                else if (_imageConverter.Convert(selected.CoverImage, typeof(BitmapSource), null, System.Globalization.CultureInfo.InvariantCulture) is BitmapSource bitmap)
                {
                    UpdateUpperPanelGradient(bitmap);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Бу-ба-бэ, что-то пошло не так");
                }
                if (Player.CurrentTrack != null)
                {
                    UpdateFavoriteIcon(Player.CurrentTrack);
                    UpdatePlayPauseIconState();
                    UpdateShuffleUI();
                }
            }
        }

        



    }
}
