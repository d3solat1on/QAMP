using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

        private async void AddFolderToCurrentPlaylist() //Вроде норм (не, не норм xd)
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
                    await MyToast.ShowAsync($"Добавлено {addedCount} треков");
                }
                finally
                {
                    Cursor = Cursors.Arrow;
                }
            }
        }
        private async void AddFilesToCurrentPlaylist()
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
                await MyToast.ShowAsync($"Добавлено {addedCount} треков");
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

                // Вместо RefreshPlaylists() загружаем только новый плейлист
                var newPlaylist = DatabaseService.GetPlaylistById((int)newId);

                if (newPlaylist != null)
                {
                    // Добавляем новый плейлист в коллекцию (оптимизировано)
                    MusicLibrary.Instance.AddNewPlaylist(newPlaylist);

                    PlaylistsListBox.SelectedItem = newPlaylist;

                    TracksDataGrid.ItemsSource = newPlaylist.Tracks;

                    await MyToast.ShowAsync($"Плейлист \"{dialog.PlaylistName}\" создан");
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

                brush.GradientStops.Add(new GradientStop(Color.FromRgb(25, 25, 25), 1));

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
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(25, 25, 25), 1));

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
        public async void RefreshPlaylist_Click(object sender, RoutedEventArgs e)
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
                    await MyToast.ShowAsync($"Плейлист \"{updatedPlaylist.Name}\" обновлен");
                }
            }
        }
        public void SortByButton_Click(object sender, RoutedEventArgs e)
        {
            // Меню должно открываться в любом случае, даже если плейлист не выбран
            var contextMenu = new ContextMenu();

            foreach (PlaylistSortOrder sortOrder in Enum.GetValues<PlaylistSortOrder>())
            {
                // Пропускаем дефолтный Manual, если Custom делает то же самое
                if (sortOrder == PlaylistSortOrder.Manual) continue;

                var menuItem = new MenuItem
                {
                    Header = sortOrder switch
                    {
                        PlaylistSortOrder.NameAZ => "Названию (A-Z)",
                        PlaylistSortOrder.NameZA => "Названию (Z-A)",
                        PlaylistSortOrder.CreatedDateNewest => "Дате создания (новые сверху)",
                        PlaylistSortOrder.CreatedDateOldest => "Дате создания (старые сверху)",
                        PlaylistSortOrder.Custom => "Пользовательский порядок",
                        _ => "Без сортировки"
                    },
                    Tag = sortOrder
                };

                menuItem.Click += (s, args) =>
                {
                    // 1. Применяем визуальную сортировку в WPF
                    ApplyPlaylistSorting(sortOrder);

                    // 2. Сохраняем глобальный выбор сортировки (например, в менеджер библиотеки)
                    // MusicLibrary.Instance.CurrentSortOrder = sortOrder;

                    // 3. Опционально: вызываем сохранение этой настройки в БД/конфиг,
                    // чтобы при следующем запуске QAMP конфигурация восстановилась.
                    AppSettings.CurrentPlaylistSort = sortOrder;
                    SettingsManager.Instance.Save();
                    _ = MyToast.ShowAsync($"Сортировка изменена");
                };

                contextMenu.Items.Add(menuItem);
            }

            // Привязываем контекстное меню к кнопке, которая его вызвала, и открываем
            if (sender is FrameworkElement element)
            {
                contextMenu.PlacementTarget = element;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }
        private void ApplyPlaylistSorting(PlaylistSortOrder sortOrder)
        {
            // Получаем представление по умолчанию для твоей коллекции плейлистов
            ICollectionView view = CollectionViewSource.GetDefaultView(MusicLibrary.Instance.Playlists);

            if (view == null) return;

            // 1. Очищаем старые правила сортировки
            view.SortDescriptions.Clear();

            // 2. ЖЕЛЕЗНОЕ ПРАВИЛО №1: Закрепленные плейлисты ВСЕГДА первыми
            // Сортируем по IsPinned по убыванию: true (1) будет выше, чем false (0)
            view.SortDescriptions.Add(new SortDescription("IsPinned", ListSortDirection.Descending));

            // 3. ПРАВИЛО №2: Сортировка внутри групп (среди закрепов и среди обычных)
            switch (sortOrder)
            {
                case PlaylistSortOrder.NameAZ:
                    // Сортировка по имени от А до Я
                    view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                    break;

                case PlaylistSortOrder.NameZA:
                    // Сортировка по имени от Я до А
                    view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Descending));
                    break;

                case PlaylistSortOrder.CreatedDateNewest:
                    // Новые сверху. Используем Id (так как новые записи получают Id больше)
                    // Если в классе есть поле CreatedDate, замени "Id" на "CreatedDate"
                    view.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Descending));
                    break;

                case PlaylistSortOrder.CreatedDateOldest:
                    // Старые сверху (по возрастанию Id)
                    view.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Ascending));
                    break;

                case PlaylistSortOrder.Custom:
                case PlaylistSortOrder.Manual:
                    // Для ручной сортировки (Drag-and-drop). 
                    // Предполагаем, что у тебя в клаصه Playlist есть свойство OrderIndex (или Position).
                    // Если его пока нет, можно использовать "Id" как базовый вариант.
                    view.SortDescriptions.Add(new SortDescription("OrderIndex", ListSortDirection.Ascending));
                    break;
            }

            // Заставляем WPF обновить интерфейс на экране
            view.Refresh();
        }
    }
}
