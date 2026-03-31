using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using Track = QAMP.Models.Track;

namespace QAMP
{
    public partial class MainWindow
    {
        private void AddMusicButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                MenuAddFilesPlaylist.Header = $"Добавить файлы в \"{selectedPlaylist.Name}\"";
                MenuAddFolderPlaylist.Header = $"Добавить папку в \"{selectedPlaylist.Name}\"";

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

        private void AddFolderToCurrentPlaylist()
        {
            if (MusicLibrary.Instance.CurrentPlaylist == null)
            {
                NotificationWindow.Show("Сначала выберите плейлист!", this);
                return;
            }

            using var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите папку с музыкой (включая подпапки)",
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var files = Directory.GetFiles(folderDialog.SelectedPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".flac"))
                    .ToArray();

                var tracks = TagReader.ReadTracksFromFiles(files);
                int addedCount = 0;

                foreach (var track in tracks)
                {
                    if (track != null)
                    {
                        if (!MusicLibrary.Instance.CurrentPlaylist.Tracks.Any(t => t.Path == track.Path))
                        {
                            DatabaseService.SaveTrackToPlaylist(MusicLibrary.Instance.CurrentPlaylist.Id, track);
                            MusicLibrary.Instance.CurrentPlaylist.Tracks.Add(track);
                            addedCount++;
                        }
                    }
                }

                TracksDataGrid.ItemsSource = null;
                TracksDataGrid.ItemsSource = MusicLibrary.Instance.CurrentPlaylist.Tracks;
                CurrentTracksCountText.Text = $"{MusicLibrary.Instance.CurrentPlaylist.Tracks.Count} треков";

                NotificationWindow.Show($"Добавлено {addedCount} треков", this);
            }
        }

        private void AddFilesToCurrentPlaylist()
        {
            if (PlaylistsListBox.SelectedItem is not Playlist selectedPlaylist) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Music files (*.mp3;*.wav;*.flac)|*.mp3;*.wav;*.flac"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                int addedCount = 0;
                foreach (string filePath in openFileDialog.FileNames)
                {
                    var newTrack = TagReader.ReadTrackFromFile(filePath);
                    if (newTrack != null)
                    {
                        if (!selectedPlaylist.Tracks.Any(t => t.Path == newTrack.Path))
                        {
                            DatabaseService.SaveTrackToPlaylist(selectedPlaylist.Id, newTrack);
                            selectedPlaylist.Tracks.Add(newTrack);
                            addedCount++;
                        }
                    }
                }

                if (MusicLibrary.Instance.CurrentPlaylist?.Id == selectedPlaylist.Id)
                {
                    TracksDataGrid.ItemsSource = null;
                    TracksDataGrid.ItemsSource = selectedPlaylist.Tracks;
                    CurrentTracksCountText.Text = $"{selectedPlaylist.Tracks.Count} треков";
                }
                else
                {
                    var listBoxItem = PlaylistsListBox.ItemContainerGenerator.ContainerFromItem(selectedPlaylist) as ListBoxItem;
                    if (listBoxItem != null)
                    {
                        var trackCountText = FindVisualChild<TextBlock>(listBoxItem, "CurrentTracksCountText");
                        if (trackCountText != null)
                        {
                            trackCountText.Text = $"{selectedPlaylist.Tracks.Count} треков";
                        }
                    }
                }

                NotificationWindow.Show($"Добавлено {addedCount} треков", this);
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
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

        private void CreatePlaylistButton_Click(object sender, RoutedEventArgs e)
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

                MusicLibrary.Instance.RefreshPlaylists();

                var newPlaylist = MusicLibrary.Instance.Playlists
                    .FirstOrDefault(p => p.Id == (int)newId);

                if (newPlaylist != null)
                {
                    PlaylistsListBox.SelectedItem = newPlaylist;

                    CurrentPlaylistNameText.Text = newPlaylist.Name;
                    CurrentPlaylistDescriptionText.Text = newPlaylist.Description;
                    CurrentTracksCountText.Text = "0 треков";

                    TracksDataGrid.ItemsSource = newPlaylist.Tracks;

                    if (newPlaylist.CoverImage != null && newPlaylist.CoverImage.Length > 0)
                    {
                        CurrentPlaylistCover.Source = LoadImage(newPlaylist.CoverImage);
                    }
                    else
                    {
                        CurrentPlaylistCover.Source = null;
                    }
                }

                NotificationWindow.Show($"Плейлист \"{dialog.PlaylistName}\" создан", this);
            }
        }

        private void PlaylistsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selected)
            {
                PlayPlaylist(selected);
            }
        }

        private void PlayPlaylist(Playlist playlist)
        {
            MusicLibrary.Instance.PlayPlaylist(playlist);
            UpdateNextTrackUI();
        }

        private void PlaylistsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selected)
            {
                System.Diagnostics.Debug.WriteLine($"=== ПРОСМОТР ПЛЕЙЛИСТА: {selected.Name} ===");
                System.Diagnostics.Debug.WriteLine($"SortType из БД: {selected.SortType}");
                MusicLibrary.Instance.CurrentPlaylist = selected;

                CurrentPlaylistNameText.Text = selected.Name;
                CurrentPlaylistDescriptionText.Text = selected.Description;
                CurrentTracksCountText.Text = $"{selected.Tracks.Count} треков";
                TimeSpan totalTime = selected.TotalDuration;
                string timeString = totalTime.TotalHours >= 1
                    ? totalTime.ToString(@"hh\:mm\:ss")
                    : totalTime.ToString(@"mm\:ss");

                CurrentTracksTimeText.Text = timeString;
                CurrentTracksDateCreateText.Text = selected.CreatedDateDisplay;

                if (selected.CoverImage != null && selected.CoverImage.Length > 0)
                {
                    CurrentPlaylistCover.Source = LoadImage(selected.CoverImage);
                }
                else
                {
                    CurrentPlaylistCover.Source = null;
                }

                // Применяем сохраненный тип сортировки к трекам
                // ApplySortToPlaylist сама будет обновлять ItemsSource после сортировки
                ApplySortToPlaylist(selected);

                // Если сортировка по дате добавления (по умолчанию), просто устанавливаем ItemsSource
                if (selected.SortType == TrackSortType.AddedDate)
                {
                    System.Diagnostics.Debug.WriteLine("Устанавливаю ItemsSource для дефолтной сортировки");
                    TracksDataGrid.ItemsSource = selected.Tracks;
                }

                // Обновляем иконку избранного при переключении плейлиста
                // в случае если текущий трек все еще воспроизводится
                if (Player.CurrentTrack != null)
                {
                    UpdateFavoriteIcon(Player.CurrentTrack);
                }
            }
        }

        /// <summary>
        /// Применяет сохраненный тип сортировки к плейлисту
        /// </summary>
        private void ApplySortToPlaylist(Playlist playlist)
        {
            System.Diagnostics.Debug.WriteLine($"=== ApplySortToPlaylist ===");
            System.Diagnostics.Debug.WriteLine($"Плейлист: {playlist.Name}");
            System.Diagnostics.Debug.WriteLine($"SortType: {playlist.SortType}");
            System.Diagnostics.Debug.WriteLine($"Треков в коллекции: {playlist.Tracks.Count}");
            
            if (playlist.SortType != TrackSortType.AddedDate)
            {
                System.Diagnostics.Debug.WriteLine($"Применяю сортировку: {playlist.SortType}");
                var sortedTracks = SortTracks(playlist.Tracks.ToList(), playlist.SortType);
                
                // Выводим первые 3 трека до и после сортировки
                System.Diagnostics.Debug.WriteLine("После сортировки:");
                for (int i = 0; i < Math.Min(3, sortedTracks.Count); i++)
                {
                    System.Diagnostics.Debug.WriteLine($"  {i}: {sortedTracks[i].Name} ({sortedTracks[i].Album}) #{sortedTracks[i].TrackNumber}");
                }
                
                playlist.Tracks.Clear();
                foreach (var track in sortedTracks)
                {
                    playlist.Tracks.Add(track);
                }

                // Обновляем DataGrid для отображения отсортированных треков
                System.Diagnostics.Debug.WriteLine("Переустанавливаю ItemsSource для DataGrid");
                TracksDataGrid.ItemsSource = null;
                TracksDataGrid.ItemsSource = playlist.Tracks;

                // Обновляем иконку сортировки (светит при активной сортировке)
                if (sortImage1 != null)
                {
                    sortImage1.Fill = (Brush)Application.Current.Resources["AccentBrush"];
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Сортировка по умолчанию (AddedDate), не применяю");
                // Иконка тусклая при дефолтной сортировке
                if (sortImage1 != null)
                {
                    sortImage1.Fill = (Brush)Application.Current.Resources["DisabledBrush"] ?? 
                                     (Brush)Application.Current.Resources["AccentBrush"];
                }
            }
        }

        private static BitmapImage? LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;

            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private void TracksDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                if (PlaylistsListBox.SelectedItem is Playlist currentPlaylist)
                {
                    // Проверяем, воспроизводится ли музыка из другого плейлиста
                    var playbackPlaylist = MusicLibrary.Instance.CurrentPlaylist;
                    var isPlayingFromOtherPlaylist = playbackPlaylist != null && playbackPlaylist.Id != currentPlaylist.Id;
                    
                    if (isPlayingFromOtherPlaylist && PlayerService.Instance.IsPlaying)
                    {
                        // Уведомляем пользователя и не переключаемся
                        NotificationWindow.Show($"Воспроизведение из плейлиста '{playbackPlaylist.Name}'", this);
                        return;
                    }
                    
                    // Разрешаем воспроизведение если это тот же плейлист или ничего не играет
                    MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, currentPlaylist);
                    UpdateNextTrackUI();
                }
            }
        }

        private void UpdateNowPlayingInfo(Track track)
        {
            if (_lastTrackWithCover != null && _lastTrackWithCover != track)
            {
                _lastTrackWithCover.UnloadCover();
            }
            LastTrackName.Text = track.Name;
            LastTrackExecutor.Text = track.Executor;

            Library.CurrentTrack = track;
            _lastTrackWithCover = track;
        }
    }
}
