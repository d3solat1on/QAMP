using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;
using QAMP.Windows;
using static QAMP.Dialogs.NotificationWindow;

namespace QAMP
{
    public partial class MainWindow
    {
        private void RemoveFromPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var selectedPlaylist = Library.CurrentPlaylist;
            if (selectedPlaylist == null)
            {
                NotificationWindow.Show("Сначала выберите плейлист", this);
                return;
            }

            var selectedTracks = TracksDataGrid.SelectedItems.Cast<Track>().ToList();

            if (selectedTracks.Count > 0)
            {
                string trackName = selectedTracks.Count == 1
                    ? $"\"{selectedTracks[0].Name}\""
                    : $"{selectedTracks.Count} треков";

                var result = NotificationWindow.Show(
                    $"Удалить {trackName} из плейлиста \"{selectedPlaylist.Name}\"?",
                    this,
                    NotificationMode.Confirm);

                if (result == true)
                {
                    foreach (var track in selectedTracks)
                    {
                        DatabaseService.RemoveTrackFromPlaylist(selectedPlaylist.Id, track.Id);
                        selectedPlaylist.Tracks.Remove(track);
                    }

                    if (selectedPlaylist.Tracks is not System.Collections.Specialized.INotifyCollectionChanged)
                    {
                        TracksDataGrid.ItemsSource = null;
                        TracksDataGrid.ItemsSource = selectedPlaylist.Tracks;
                    }

                    // CurrentTracksCountText.Text = $"{selectedPlaylist.Tracks.Count} треков";
                    UpdateNextTrackUI();
                }
            }
        }

        private void RemovePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                if (selectedPlaylist.Name == MusicLibrary.FavoritesName)
                {
                    NotificationWindow.Show("Системный плейлист нельзя удалить", this);
                    return;
                }

                if (NotificationWindow.Show($"Удалить плейлист \"{selectedPlaylist.Name}\"?", this, NotificationMode.Confirm) == true)
                {
                    // КРИТИЧНО: Сохраняем какой плейлист был выбран ДО удаления
                    var previouslySelectedPlaylist = MusicLibrary.Instance.CurrentPlaylist;

                    DatabaseService.DeletePlaylist(selectedPlaylist.Id);

                    // Удаляем плейлист из коллекции
                    var playlistToRemove = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == selectedPlaylist.Id);
                    if (playlistToRemove != null)
                    {
                        MusicLibrary.Instance.Playlists.Remove(playlistToRemove);
                    }

                    // ПОСЛЕ удаления: восстанавливаем правильный выбор в ListBox
                    // Проверяем, существует ли плейлист, который был выбран ранее
                    if (previouslySelectedPlaylist != null)
                    {
                        var stillExistsPlaylist = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == previouslySelectedPlaylist.Id);
                        if (stillExistsPlaylist != null)
                        {
                            // Ранее выбранный плейлист еще существует - восстанавливаем его
                            PlaylistsListBox.SelectedItem = stillExistsPlaylist;
                        }
                        else
                        {
                            // Ранее выбранный был удален - выбираем первый доступный
                            if (MusicLibrary.Instance.Playlists.Count > 0)
                            {
                                PlaylistsListBox.SelectedItem = MusicLibrary.Instance.Playlists.FirstOrDefault();
                            }
                            else
                            {
                                // Нет других плейлистов, очищаем UI
                                MusicLibrary.Instance.CurrentPlaylist = null;
                                // CurrentPlaylistNameText.Text = "";
                                // CurrentPlaylistDescriptionText.Text = "";
                                // CurrentPlaylistCover.Source = null;
                                // CurrentTracksCountText.Text = "0 треков";
                            }
                        }
                    }
                    else
                    {
                        // Раньше ничего не было выбрано - выбираем первый доступный
                        if (MusicLibrary.Instance.Playlists.Count > 0)
                        {
                            PlaylistsListBox.SelectedItem = MusicLibrary.Instance.Playlists.FirstOrDefault();
                        }
                    }

                    NotificationWindow.Show("Плейлист удален", this);
                }
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (Player != null)
                {
                    double volume = VolumeSlider.Value / 100.0;
                    Player.Volume = volume;

                    string volumeStr = volume.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _ = DatabaseService.SaveSetting("Volume", volumeStr);

                    if (VolumeSlider.Value == 0)
                    {
                        VolumeImage.Data = (Geometry)Application.Current.Resources["volume_offGeometry"];
                    }
                    else
                    {
                        VolumeImage.Data = (Geometry)Application.Current.Resources["volumeGeometry"];
                    }

                    if (VolumePercentage != null)
                    {
                        VolumePercentage.Text = $"{VolumeSlider.Value:F0}%";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in VolumeSlider_ValueChanged: {ex.Message}");
            }
        }

        private void VolumeButton_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (VolumeSlider.Value > 0)
            {
                _lastVolume = VolumeSlider.Value;
                VolumeSlider.Value = 0;
            }
            else
            {
                VolumeSlider.Value = _lastVolume;
            }
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            var resources = Application.Current.Resources;

            switch (Player.RepeatMode)
            {
                case RepeatMode.NoRepeat:
                    Player.RepeatMode = RepeatMode.RepeatAll;
                    RepeatIcon.Data = (Geometry)resources["repeat_onGeometry"];
                    break;

                case RepeatMode.RepeatAll:
                    Player.RepeatMode = RepeatMode.RepeatOne;
                    RepeatIcon.Data = (Geometry)resources["repeat_one_onGeometry"];
                    break;

                case RepeatMode.RepeatOne:
                    Player.RepeatMode = RepeatMode.NoRepeat;
                    RepeatIcon.Data = (Geometry)resources["repeatGeometry"];
                    break;
            }
            UpdateNextTrackUI();
        }

        private void DeletePlaylist_Click(object sender, RoutedEventArgs e) => RemovePlaylist_Click(sender, e);


        private void PinPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Playlist selectedPlaylist)
            {
                bool newPinnedState = !selectedPlaylist.IsPinned;
                DatabaseService.UpdatePlaylistPinnedState(selectedPlaylist.Id, newPinnedState);
                selectedPlaylist.IsPinned = newPinnedState;

                // Используем RefreshSinglePlaylist для оптимизации вместо RefreshPlaylists
                MusicLibrary.Instance.RefreshSinglePlaylist(selectedPlaylist.Id);
                PlaylistsListBox.SelectedItem = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == selectedPlaylist.Id);
                var message = newPinnedState ? "Плейлист закреплен" : "Плейлист откреплен";
                NotificationWindow.Show(message, this);
            }
        }

        private void Playlist_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is ListBoxItem item)
            {
                DragDrop.DoDragDrop(item, item.DataContext, DragDropEffects.Move);
            }
        }

        private void Playlist_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(Playlist)) is Playlist droppedPlaylist && sender is ListBoxItem { DataContext: Playlist targetPlaylist } && droppedPlaylist != targetPlaylist)
            {
                var playlists = MusicLibrary.Instance.Playlists;
                int oldIndex = playlists.IndexOf(droppedPlaylist);
                int newIndex = playlists.IndexOf(targetPlaylist);

                if (oldIndex != -1 && newIndex != -1)
                {
                    playlists.Move(oldIndex, newIndex);
                    DatabaseService.SavePlaylistsOrder();
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
                    // Вместо RefreshPlaylists() используем RefreshSinglePlaylist для оптимизации
                    MusicLibrary.Instance.RefreshSinglePlaylist(selectedPlaylist.Id);

                    // Восстанавливаем выбор плейлиста в списке
                    PlaylistsListBox.SelectedItem = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == selectedPlaylist.Id);
                }
            }
        }

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
                    infoWindow.ShowDialog();
                }
            }
            else
            {
                NotificationWindow.Show("Пожалуйста, сначала выделите трек.", this);
            }
        }

        private void PlayTrackMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (TracksDataGrid.SelectedItem is Track selectedTrack)
            {
                App.LogInfo($"PlayTrack (ContextMenu): {selectedTrack.Executor} - {selectedTrack.Name}");
                _ = Player.PlayTrack(selectedTrack);
                UpdateNextTrackUI();
            }
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Player.CurrentTrack == null)
            {
                NotificationWindow.Show("Нет трека для добавления в Избранное", this);
                return;
            }

            var favoritePlaylist = Library.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);
            if (favoritePlaylist == null)
            {
                byte[]? favCover = null;
                try
                {
                    var geometry = (Geometry)Application.Current.Resources["favoriteGeometry"];
                    var accentBrush = (Brush)Application.Current.Resources["AccentBrush"];
                    var drawing = new GeometryDrawing(accentBrush, null, geometry);
                    favCover = RenderGeometryToPngConverter.RenderGeometryToPng(geometry, accentBrush);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка создания обложки для избранного: {ex.Message}");
                }

                long newId = DatabaseService.CreatePlaylist(MusicLibrary.FavoritesName, "Ваши любимые треки", favCover);
                var newPlaylist = new Playlist
                {
                    Id = (int)newId,
                    Name = MusicLibrary.FavoritesName,
                    Description = "Ваши любимые треки",
                    CoverImage = favCover ?? []
                };

                var tracks = DatabaseService.GetTracksForPlaylist((int)newId);
                foreach (var track in tracks)
                {
                    newPlaylist.Tracks.Add(track);
                }

                Library.Playlists.Add(newPlaylist);
                favoritePlaylist = newPlaylist;
            }

            if (favoritePlaylist == null) return;

            bool isAlreadyFavorite = favoritePlaylist.Tracks.Any(t => t.Path == Player.CurrentTrack.Path);
            if (!isAlreadyFavorite)
            {
                DatabaseService.SaveTrackToPlaylist(favoritePlaylist.Id, Player.CurrentTrack);
                favoritePlaylist.Tracks.Add(Player.CurrentTrack);
                UpdateFavoriteIcon(Player.CurrentTrack, true);
                NotificationWindow.Show("Добавлено в Избранное", this);
            }
            else
            {
                DatabaseService.RemoveTrackFromPlaylist(favoritePlaylist.Id, Player.CurrentTrack.Id);
                var trackToRemove = favoritePlaylist.Tracks.FirstOrDefault(t => t.Id == Player.CurrentTrack.Id);
                if (trackToRemove != null)
                {
                    favoritePlaylist.Tracks.Remove(trackToRemove);
                }
                UpdateFavoriteIcon(Player.CurrentTrack, false);
                NotificationWindow.Show("Удалено из Избранного", this);
            }

            if (Library.CurrentPlaylist?.Id == favoritePlaylist.Id)
            {
                TracksDataGrid.ItemsSource = null;
                TracksDataGrid.ItemsSource = favoritePlaylist.Tracks;
                // CurrentTracksCountText.Text = $"{favoritePlaylist.Tracks.Count} треков";
            }

            UpdateFavoriteIcon(Player.CurrentTrack);
        }

        private void UpdateFavoriteIcon(Track track, bool? forceState = null)
        {
            if (FavoriteIcon == null) return;

            bool isFavorite;
            if (forceState.HasValue)
            {
                isFavorite = forceState.Value;
            }
            else if (track != null)
            {
                var favoritePlaylist = Library.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);
                // Проверяем по ID вместо Contains(), так как Contains() сравнивает по ссылкам объектов
                // Если трек загружен из разных источников - это разные объекты, хоть ID одинаковые
                isFavorite = favoritePlaylist?.Tracks.Any(t => t.Id == track.Id) ?? false;
            }
            else
            {
                isFavorite = false;
            }

            FavoriteIcon.Data = isFavorite
                ? (Geometry)Application.Current.Resources["favorites_addedGeometry"]
                : (Geometry)Application.Current.Resources["add_favoritesGeometry"];

            FavoriteButton.ToolTip = isFavorite ? "Удалить из избранного" : "Добавить в избранное";
            FavoriteIcon.Fill = (Brush)Application.Current.Resources["AccentBrush"];
        }

        private static void LoadPlaylists()
        {
            System.Diagnostics.Debug.WriteLine("=== НАЧАЛО ЗАГРУЗКИ ПЛЕЙЛИСТОВ ===");
            MusicLibrary.Instance.RefreshPlaylists();
            System.Diagnostics.Debug.WriteLine("=== КОНЕЦ ЗАГРУЗКИ ПЛЕЙЛИСТОВ ===");
        }

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

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            if (PlaylistsListBox.SelectedItem is Playlist currentPlaylist)
            {
                TracksDataGrid.ItemsSource = currentPlaylist.Tracks;
                // CurrentPlaylistNameText.Text = currentPlaylist.Name;
                // CurrentPlaylistDescriptionText.Text = currentPlaylist.Description;
                // CurrentTracksCountText.Text = $"{currentPlaylist.Tracks.Count} треков";
            }
        }

        private void PerformSearch()
        {
            string searchQuery = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                NotificationWindow.Show("Введите текст для поиска", this);
                return;
            }

            var searchResults = new List<Track>();
            string searchLower = searchQuery.ToLower();

            foreach (var playlist in MusicLibrary.Instance.Playlists)
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

        private void ShowSearchResults(List<Track> results, string searchQuery)
        {
            if (results.Count == 0)
            {
                NotificationWindow.Show($"По запросу \"{searchQuery}\" ничего не найдено", this);
                return;
            }

            var searchPlaylist = new Playlist
            {
                Name = "Результаты поиска",
                Description = $"По запросу: \"{searchQuery}\" найдено {results.Count} треков",
                Tracks = new ObservableCollection<Track>(results),
                CoverImage = null
            };

            MusicLibrary.Instance.CurrentPlaylist = searchPlaylist;

            TracksDataGrid.ItemsSource = searchPlaylist.Tracks;
        }

        private void AddToPlaylistSubMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem subMenu) return;
            subMenu.Items.Clear();
            if (MusicLibrary.Instance.Playlists.Count == 0)
            {
                subMenu.Items.Add(new MenuItem { Header = "Нет плейлистов", IsEnabled = false });
                return;
            }

            foreach (var playlist in MusicLibrary.Instance.Playlists)
            {
                MenuItem item = new() { Header = playlist.Name, DataContext = playlist, Tag = playlist.Id };
                item.Click += AddToSpecificPlaylist_Click;
                subMenu.Items.Add(item);
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;
            var subMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header is string header && header.Contains("Добавить в плейлист"));
            if (subMenu == null) return;

            subMenu.ItemsSource = null;
            subMenu.Items.Clear();

            var playlists = MusicLibrary.Instance.Playlists;
            if (playlists == null || playlists.Count == 0)
            {
                subMenu.Items.Add(new MenuItem { Header = "Нет доступных плейлистов", IsEnabled = false });
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

        private void AddToSpecificPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Playlist targetPlaylist)
            {
                if (TracksDataGrid.SelectedItem is Track selectedTrack)
                {
                    if (string.IsNullOrEmpty(selectedTrack.Path))
                    {
                        NotificationWindow.Show("Ошибка: путь трека не определен", this);
                        return;
                    }

                    if (IsTrackInPlaylist(targetPlaylist.Id, selectedTrack.Id))
                    {
                        NotificationWindow.Show($"Трек уже есть в плейлисте \"{targetPlaylist.Name}\"!", this);
                        return;
                    }

                    DatabaseService.AddTrackToPlaylist(targetPlaylist.Id, selectedTrack.Path);
                    var tracksFromDb = DatabaseService.GetTracksForPlaylist(targetPlaylist.Id);
                    targetPlaylist.Tracks = new ObservableCollection<Track>(tracksFromDb);

                    NotificationWindow.Show($"Добавлено в \"{targetPlaylist.Name}\"", this);
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

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Settings(_playService)
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
        }
    }
}
