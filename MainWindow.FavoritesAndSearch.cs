using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

                var confirmMessage = (Application.Current.Resources["LngRemoveFromPlaylistConfirm"] as string ?? "Удалить {0} из плейлиста \"{1}\"?")
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
                        DatabaseService.RemoveTrackFromPlaylist(selectedPlaylist.Id, track.Id);
                        selectedPlaylist.Tracks.Remove(track);
                    }
                    TracksDataGrid.ItemsSource = null;
                    TracksDataGrid.ItemsSource = selectedPlaylist.Tracks;
                    UpdateNextTrackUI();
                }
            }
        }

        private async void RemovePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
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

                    string message = Application.Current.Resources["LngPlaylistDeleted"] as string ?? "Плейлист удален";
                    await MyToast.ShowAsync(message);
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

                    VolumePercentage?.Text = $"{VolumeSlider.Value:F0}%";
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

        private void RepeatButton_Click(object? sender, RoutedEventArgs? e)
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

        private async void PinPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Playlist selectedPlaylist)
            {
                bool newPinnedState = !selectedPlaylist.IsPinned;
                DatabaseService.UpdatePlaylistPinnedState(selectedPlaylist.Id, newPinnedState);
                selectedPlaylist.IsPinned = newPinnedState;

                // Вместо полной перезагрузки просто обновляем наш View!
                ICollectionView view = CollectionViewSource.GetDefaultView(MusicLibrary.Instance.Playlists);
                view?.Refresh();

                // Возвращаем фокус на измененный плейлист
                PlaylistsListBox.SelectedItem = selectedPlaylist;

                var message = newPinnedState ? (Application.Current.Resources["LngPinnedPlaylist"] as string ?? "Плейсит закреплен")
                                            : (Application.Current.Resources["LngUnpinnedPlaylist"] as string ?? "Плейсит откреплен");
                await MyToast.ShowAsync(message);
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
            // 1. Проверяем, что сейчас включен именно кастомный/ручной режим сортировки.
            // Если включен "По алфавиту", таскать элементы UI запрещено, иначе начнется хаос.
            var currentSort = AppSettings.CurrentPlaylistSort;
            if (currentSort != PlaylistSortOrder.Custom && currentSort != PlaylistSortOrder.Manual)
            {
                return;
            }

            if (e.Data.GetData(typeof(Playlist)) is Playlist droppedPlaylist &&
                sender is ListBoxItem { DataContext: Playlist targetPlaylist } &&
                droppedPlaylist != targetPlaylist)
            {
                if (droppedPlaylist.IsPinned != targetPlaylist.IsPinned)
                {
                    return;
                }

                var playlists = MusicLibrary.Instance.Playlists;
                int oldIndex = playlists.IndexOf(droppedPlaylist);
                int newIndex = playlists.IndexOf(targetPlaylist);

                if (oldIndex != -1 && newIndex != -1)
                {
                    // Перемещаем элемент в основной коллекции
                    playlists.Move(oldIndex, newIndex);

                    // Сохраняем новый порядок в базу данных SQLite (метод перепишем ниже)
                    DatabaseService.SavePlaylistsOrder(playlists);

                    // Обновляем представление на экране, чтобы зафиксировать изменения
                    ICollectionView view = CollectionViewSource.GetDefaultView(playlists);
                    view?.Refresh();
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
                    PlaylistsListBox.SelectionChanged -= PlaylistsListBox_SelectionChanged;
                    var updatedPlaylist = MusicLibrary.Instance.Playlists.FirstOrDefault(p => p.Id == selectedPlaylist.Id);
                    PlaylistsListBox.SelectedItem = updatedPlaylist;
                    TracksDataGrid.ItemsSource = updatedPlaylist?.Tracks;
                    PlaylistsListBox.SelectionChanged += PlaylistsListBox_SelectionChanged;
                }
            }
        }

        private async void ShowTrackInfo_Click(object sender, RoutedEventArgs e)
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
                await MyToast.ShowAsync(message);
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

        private async void FavoriteButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (Player.CurrentTrack == null)
            {
                string message = Application.Current.Resources["LngSelectTrackFirst"] as string ?? "Please select a track first.";
                NotificationWindow.Show(message, this);
                return;
            }

            var favoritePlaylist = Library.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);
            if (favoritePlaylist == null)
            {

                string message = Application.Current.Resources["LngErrorFavoritesNotFound"] as string ?? "Error: Favorites playlist not found";
                NotificationWindow.Show(message, this);
                return;
            }

            bool isAlreadyFavorite = favoritePlaylist.Tracks.Any(t => t.Path == Player.CurrentTrack.Path);
            if (!isAlreadyFavorite)
            {
                var trackToSave = TagReader.GetFullTrackInfo(Player.CurrentTrack.Path) ?? Player.CurrentTrack;

                trackToSave.AddedDate = DateTime.Now;
                trackToSave.PlayCount = Player.CurrentTrack.PlayCount;

                DatabaseService.SaveTrackToPlaylist(favoritePlaylist.Id, trackToSave);
                favoritePlaylist.Tracks.Add(trackToSave);
                UpdateFavoriteIcon(Player.CurrentTrack, true);
                string successMessage = Application.Current.Resources["LngTrackAddedToFavorites"] as string ?? "Successfully added to favorites";
                await MyToast.ShowAsync(successMessage);
                System.Diagnostics.Debug.WriteLine($"DEBUG: Трек \"{trackToSave.Name}\" добавлен в Избранное. Путь: {trackToSave.Path}");
            }
            else
            {
                string successMessage = Application.Current.Resources["LngTrackRemovedFromFavorites"] as string ?? "Successfully removed from favorites";
                await MyToast.ShowAsync(successMessage);
                System.Diagnostics.Debug.WriteLine($"DEBUG: Трек \"{Player.CurrentTrack.Name}\" удален из Избранного. Путь: {Player.CurrentTrack.Path}");
                DatabaseService.RemoveTrackFromPlaylist(favoritePlaylist.Id, Player.CurrentTrack.Id);
                var trackToRemove = favoritePlaylist.Tracks.FirstOrDefault(t => t.Id == Player.CurrentTrack.Id);
                if (trackToRemove != null)
                {
                    favoritePlaylist.Tracks.Remove(trackToRemove);
                }
                UpdateFavoriteIcon(Player.CurrentTrack, false);
            }

            if (Library.CurrentPlaylist?.Id == favoritePlaylist.Id)
            {
                TracksDataGrid.ItemsSource = null;
                TracksDataGrid.ItemsSource = favoritePlaylist.Tracks;
            }
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
                isFavorite = favoritePlaylist?.Tracks.Any(t => t.Path == track.Path) ?? false;
            }
            else
            {
                isFavorite = false;
            }

            FavoriteIcon.Data = isFavorite
                ? (Geometry)Application.Current.Resources["favorites_addedGeometry"]
                : (Geometry)Application.Current.Resources["add_favoritesGeometry"];

            FavoriteButton.ToolTip = isFavorite ? Application.Current.Resources["LngRemoveFromFavorites"] : Application.Current.Resources["LngAddToFavorites"];
            FavoriteIcon.Fill = (Brush)Application.Current.Resources["AccentBrush"];
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
                                         ?? "По запросу: \"{0}\" найдено {1} треков";

            string formattedDescription = string.Format(descriptionTemplate, searchQuery, results.Count);

            var searchPlaylist = new Playlist
            {
                Name = namePL,
                Description = formattedDescription,
                Tracks = new ObservableCollection<Track>(results),
                CoverImage = null
            };

            MusicLibrary.Instance.CurrentPlaylist = searchPlaylist;
            TracksDataGrid.ItemsSource = searchPlaylist.Tracks;
        }

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

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Settings(_playService)
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
        }
        private void ShowPlaylistInfo_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
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
        private void EqualizerButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsAudioWindow = new SettingsAudio()
            {
                Owner = this
            };
            settingsAudioWindow.Show();
        }
    }
}
