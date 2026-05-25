using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QAMP.Converters;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using Track = QAMP.Models.Track;

namespace QAMP
{
    public partial class MainWindow
    {
        private readonly SettingsManager _settingsManager = SettingsManager.Instance;
        private AppSettings AppSettings => _settingsManager.Config;
        private readonly LargeTrackImageConverter _imageConverter = new();
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

        private void AddFolderToCurrentPlaylist() //Вроде норм (не, не норм xd)
        {
            if (PlaylistsListBox.SelectedItem is not Playlist selectedPlaylist) return;

            if (MusicLibrary.Instance.CurrentPlaylist == null)
            {
                NotificationWindow.Show("Сначала выберите плейлист!", this);
                return;
            }

            var folderDialog = new OpenFolderDialog
            {
                Title = "Выберите папку с музыкой (включая подпапки)",
                Multiselect = true 
            };

            if (folderDialog.ShowDialog() == true)
            {
                Cursor = Cursors.Wait;
                try
                {
                    var files = Directory.GetFiles(folderDialog.FolderName, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".flac"))
                    .ToArray();

                    var tracks = TagReader.ReadTracksFromFiles(files);
                    int addedCount = 0;

                    foreach (var track in tracks)
                    {
                        if (track != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (!MusicLibrary.Instance.CurrentPlaylist.Tracks.Any(t => t.Path == track.Path))
                                {
                                    DatabaseService.SaveTrackToPlaylist(MusicLibrary.Instance.CurrentPlaylist.Id, track);
                                    MusicLibrary.Instance.CurrentPlaylist.Tracks.Add(track);
                                    addedCount++;
                                }
                            });
                        }
                    }
                    if (MusicLibrary.Instance.PlayingPlaylist?.Id == selectedPlaylist.Id)
                    {
                        Player.UpdateQueueOrder([.. selectedPlaylist.Tracks]);
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
                    NotificationWindow.Show($"Добавлено {addedCount} треков", this);
                }
                finally
                {
                    Cursor = Cursors.Arrow;
                }
            }
        }
        private void AddFilesToCurrentPlaylist()
        {
            if (PlaylistsListBox.SelectedItem is not Playlist selectedPlaylist) return;

            var openFileDialog = new OpenFileDialog
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
                if (MusicLibrary.Instance.PlayingPlaylist?.Id == selectedPlaylist.Id)
                {
                    Player.UpdateQueueOrder([.. selectedPlaylist.Tracks]);
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
                NotificationWindow.Show($"Добавлено {addedCount} треков", this);
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

                // Вместо RefreshPlaylists() загружаем только новый плейлист
                var newPlaylist = DatabaseService.GetPlaylistById((int)newId);

                if (newPlaylist != null)
                {
                    // Добавляем новый плейлист в коллекцию (оптимизировано)
                    MusicLibrary.Instance.AddNewPlaylist(newPlaylist);

                    PlaylistsListBox.SelectedItem = newPlaylist;

                    TracksDataGrid.ItemsSource = newPlaylist.Tracks;


                    NotificationWindow.Show($"Плейлист \"{dialog.PlaylistName}\" создан", this);
                }
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
                MusicLibrary.Instance.CurrentPlaylist = selected;
                System.Diagnostics.Debug.WriteLine($"=== ПРОСМОТР ПЛЕЙЛИСТА: {selected.Name} ===");
                System.Diagnostics.Debug.WriteLine($"SortType из БД: {selected.SortType}");
                App.LogInfo($"SelectPlaylist: {selected.Name} | Tracks: {selected.Tracks.Count}");
                ApplySort(selected.SortType);

                // Для плейлиста "Избранное" используем цвет приложения
                if (selected.IsSystemPlaylist)
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
                }

                UpdatePlayPauseIconState();
            }
        }

        private void TracksDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                if (PlaylistsListBox.SelectedItem is Playlist currentPlaylist)
                {
                    App.LogInfo($"TrackDoubleClick: {selectedTrack.Executor} - {selectedTrack.Name} | Playlist: {currentPlaylist.Name}");
                    var displayOrder = TracksDataGrid.ItemsSource as IEnumerable<Track>;
                    MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, currentPlaylist, displayOrder);
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
            if (track == null)
            {
                LastTrackName.Text = string.Empty;
                LastTrackExecutor.Text = string.Empty;
                UpperPanel.Background = (Brush)Application.Current.Resources["BackgroundBrush"];
                return;
            }
            else
            {
                LastTrackName.Text = track.Name;
                LastTrackExecutor.Text = track.Executor;
            }
            Library.CurrentTrack = track;
            _lastTrackWithCover = track;
        }
        private void UpdateUpperPanelGradient(BitmapSource cover)
        {
            if (cover == null || !AppSettings.UseAdaptiveGradients)
            {
                UpperPanel.Background = (Brush)Application.Current.Resources["BackgroundBrush"];
                return;
            }
            try
            {
                Color dominant = ThemeHelper.GetDominantColor(cover);

                // Создаем градиент
                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };

                brush.GradientStops.Add(new GradientStop(dominant, 0));

                brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(25, 25, 25), 1));

                UpperPanel.Background = brush;
            }
            catch (Exception ex)
            {
                App.LogException(ex, "GradientUpdate");
            }
        }

        /// <summary>
        /// Обновляет градиент верхней панели для плейлиста "Избранное" на основе главного цвета приложения
        /// </summary>
        private void UpdateUpperPanelGradientForFavorites()
        {
            try
            {
                // Получаем цвет Accent из ресурсов приложения
                if (Application.Current.Resources["AccentBrush"] is SolidColorBrush accentBrush)
                {
                    var brush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1)
                    };

                    brush.GradientStops.Add(new GradientStop(accentBrush.Color, 0));
                    brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(25, 25, 25), 1));

                    UpperPanel.Background = brush;
                }
                else
                {
                    UpperPanel.Background = (Brush)Application.Current.Resources["BackgroundBrush"];
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "FavoritesGradientUpdate");
                UpperPanel.Background = (Brush)Application.Current.Resources["BackgroundBrush"];
            }
        }

        /// <summary>
        /// Обновляет градиент верхней панели при изменении настроек адаптивных градиентов
        /// </summary>
        public void RefreshAdaptiveGradients()
        {
            var currentPlaylist = MusicLibrary.Instance.CurrentPlaylist;
            if (currentPlaylist == null)
                return;

            if (currentPlaylist.IsSystemPlaylist)
            {
                UpdateUpperPanelGradientForFavorites();
            }
            else
            {
                var bitmap = _imageConverter.Convert(currentPlaylist.CoverImage, typeof(BitmapSource), null, System.Globalization.CultureInfo.InvariantCulture) as BitmapSource;
                UpdateUpperPanelGradient(bitmap!);
            }
        }
        public void RefreshPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                var updatedPlaylist = DatabaseService.GetPlaylistById(selectedPlaylist.Id);
                if (updatedPlaylist != null)
                {
                    MusicLibrary.Instance.UpdatePlaylist(updatedPlaylist);
                    if (MusicLibrary.Instance.PlayingPlaylist?.Id == selectedPlaylist.Id)
                    {
                        Player.UpdateQueueOrder([.. updatedPlaylist.Tracks]);
                    }
                    ApplySort(updatedPlaylist.SortType);
                    UpdateNextTrackUI();
                    NotificationWindow.Show($"Плейлист \"{updatedPlaylist.Name}\" обновлен", this);
                }
            }
        }
    }
}
