using System.Collections.ObjectModel;
using System.ComponentModel;
using QAMP.Models;
using QAMP.Services;

namespace QAMP.ViewModels
{
    public class MusicLibrary : INotifyPropertyChanged
    {
        private static MusicLibrary? _instance;
        public static MusicLibrary Instance => _instance ??= new MusicLibrary();

        // Коллекция плейлистов
        private ObservableCollection<Playlist> _playlists = [];
        public ObservableCollection<Playlist> Playlists
        {
            get => _playlists;
            set
            {
                _playlists = value;
                OnPropertyChanged(nameof(Playlists));
            }
        }

        // public const string FavoritesName = "Избранное"; //Favorites
        public const string FavoritesName = "Favorites";

        // Текущий выбранный плейлист
        private Playlist? _currentPlaylist;
        public Playlist? CurrentPlaylist
        {
            get => _currentPlaylist;
            set
            {
                if (_currentPlaylist != value)
                {
                    _currentPlaylist = value;
                    if (value != null)
                    {
                        _ = DatabaseService.SaveSetting("LastPlaylistId", value.Id.ToString());
                    }
                    OnPropertyChanged(nameof(CurrentPlaylist));
                }
            }
        }
        // Плейлист из которого воспроизводится музыка (отличается от CurrentPlaylist который для просмотра)
        private Playlist? _playingPlaylist;
        public Playlist? PlayingPlaylist
        {
            get => _playingPlaylist;
            set
            {
                if (_playingPlaylist != value)
                {
                    _playingPlaylist = value;
                    OnPropertyChanged(nameof(PlayingPlaylist));
                }
            }
        }
        private ObservableCollection<Track> _playbackQueue = [];
        public ObservableCollection<Track> PlaybackQueue
        {
            get => _playbackQueue;
            set
            {
                _playbackQueue = value;
                OnPropertyChanged(nameof(PlaybackQueue));
            }
        }
        public Track? CurrentTrack { get; set; }

        // НОВЫЙ МЕТОД: для воспроизведения плейлиста
        // public void PlayPlaylist(Playlist playlist)
        // {
        //     if (playlist == null) return;

        //     System.Diagnostics.Debug.WriteLine($"=== ВОСПРОИЗВЕДЕНИЕ ПЛЕЙЛИСТА: {playlist.Name} ===");

        //     // Устанавливаем плейлист из которого воспроизводится музыка
        //     PlayingPlaylist = playlist;

        //     // Обновляем очередь воспроизведения
        //     PlaybackQueue.Clear();
        //     foreach (var track in playlist.Tracks)
        //     {
        //         PlaybackQueue.Add(track);
        //     }

        //     // Начинаем с первого трека
        //     if (PlaybackQueue.Count > 0)
        //     {
        //         _ = PlayerService.Instance.PlayTrack(PlaybackQueue[0]);
        //     }
        // }

        //// <summary>
        /// Воспроизводит трек из плейлиста с учетом отображаемого порядка (например, при сортировке)
        /// </summary>
        public void PlayTrackFromPlaylist(Track track, Playlist playlist, IEnumerable<Track>? displayOrder = null, bool bypassShuffle = false)
        {
            if (track == null || playlist == null) return;

            PlayingPlaylist = playlist;

            var tracksForQueue = displayOrder ?? playlist.Tracks;

            PlaybackQueue.Clear();
            foreach (var t in tracksForQueue)
            {
                PlaybackQueue.Add(t);
            }

            if (PlayerService.Instance.IsShuffleEnabled && !bypassShuffle)
            {
                var remainingTracks = PlaybackQueue.Where(t => t != track).OrderBy(x => Guid.NewGuid()).ToList();
                PlayerService.Instance.ShuffledQueue = [track, .. remainingTracks];
            }
            else if (PlayerService.Instance.IsShuffleEnabled && bypassShuffle)
            {
                PlayerService.Instance.ShuffledQueue = [.. PlaybackQueue];
            }
            _ = PlayerService.Instance.PlayTrack(track, true);
        }
        // public void AddTrackToPlayNext(Track track)
        // {
        //     if (track == null) return;

        //     Track? currentTrack = PlayerService.Instance.CurrentTrack;

        //     int targetIndex = 0;

        //     if (PlayerService.Instance.IsShuffleEnabled)
        //     {
        //         if (currentTrack != null)
        //         {
        //             targetIndex = PlayerService.Instance.ShuffledQueue.IndexOf(currentTrack) + 1;
        //         }
        //         targetIndex = Math.Clamp(targetIndex, 0, PlayerService.Instance.ShuffledQueue.Count);
        //         PlayerService.Instance.ShuffledQueue.Insert(targetIndex, track);
        //     }
        //     else
        //     {
        //         if (currentTrack != null)
        //         {
        //             targetIndex = PlayerService.Instance._actualPlayingQueue.IndexOf(currentTrack) + 1;
        //         }
        //         targetIndex = Math.Clamp(targetIndex, 0, PlayerService.Instance._actualPlayingQueue.Count); 
        //         PlayerService.Instance._actualPlayingQueue.Insert(targetIndex, track);
        //     }

        //     int uiTargetIndex = 0;
        //     if (currentTrack != null)
        //     {
        //         uiTargetIndex = PlaybackQueue.IndexOf(currentTrack) + 1;
        //     }
        //     uiTargetIndex = Math.Clamp(uiTargetIndex, 0, PlaybackQueue.Count);

        //     PlaybackQueue.Insert(uiTargetIndex, track);

        //     System.Diagnostics.Debug.WriteLine($"Трек '{track.Name}' добавлен как следующий. Позиция в UI: {uiTargetIndex}");
        // }
        public MusicLibrary()
        {
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CurrentPlaylist) && CurrentPlaylist != null)
                {
                    CurrentPlaylist.Tracks.CollectionChanged += (sender, args) =>
                    {
                        UpdatePlaybackQueue();
                        OnPropertyChanged(nameof(PlaybackQueue));
                    };
                }
            };
        }

        private void UpdatePlaybackQueue()
        {
            PlaybackQueue.Clear();
            if (CurrentPlaylist?.Tracks != null)
            {
                foreach (var track in CurrentPlaylist.Tracks)
                {
                    PlaybackQueue.Add(track);
                }
            }
        }

        public void UpdatePlaylist(Playlist updatedPlaylist)
        {
            // Находим существующий плейлист в коллекции
            var existingPlaylist = Playlists.FirstOrDefault(p => p.Id == updatedPlaylist.Id);
            if (existingPlaylist != null)
            {
                // Обновляем свойства
                existingPlaylist.Name = updatedPlaylist.Name;
                existingPlaylist.Description = updatedPlaylist.Description;
                existingPlaylist.CoverImage = updatedPlaylist.CoverImage;

                // Обновляем коллекцию треков
                existingPlaylist.Tracks.Clear();
                foreach (var track in updatedPlaylist.Tracks)
                {
                    existingPlaylist.Tracks.Add(track);
                }

                // Если это текущий плейлист, обновляем отображение
                if (CurrentPlaylist?.Id == updatedPlaylist.Id)
                {
                    OnPropertyChanged(nameof(CurrentPlaylist));
                }
            }
            else
            {
                // Если плейлиста нет, добавляем новый
                Playlists.Add(updatedPlaylist);
            }
        }

        public void RefreshSinglePlaylist(int playlistId)
        {
            var freshPlaylist = DatabaseService.GetPlaylistById(playlistId);
            if (freshPlaylist != null)
            {
                UpdatePlaylist(freshPlaylist); // Обновляем данные в общем списке

                // Вместо жесткого CurrentPlaylist = updatedPlaylist, 
                // просто уведомляем UI, что свойства изменились
                var updatedPlaylist = Playlists.FirstOrDefault(p => p.Id == playlistId);
                if (updatedPlaylist != null && CurrentPlaylist?.Id == playlistId)
                {
                    // Мы НЕ ПЕРЕПРИВЯЗЫВАЕМ объект, мы просто говорим UI обновиться
                    OnPropertyChanged(nameof(CurrentPlaylist));

                }
            }
        }

        /// <summary>
        /// Добавляет новый плейлист в коллекцию (для оптимизации при создании нового)
        /// </summary>
        public void AddNewPlaylist(Playlist newPlaylist)
        {
            System.Diagnostics.Debug.WriteLine($"Adding new playlist: {newPlaylist.Name} (ID={newPlaylist.Id})");
            AppConsole.Log($"Adding new playlist: {newPlaylist.Name} (ID={newPlaylist.Id})");

            // Добавляем в коллекцию
            Playlists.Add(newPlaylist);

            System.Diagnostics.Debug.WriteLine($"Playlist '{newPlaylist.Name}' added. Total playlists: {Playlists.Count}");
            AppConsole.Log($"Playlist '{newPlaylist.Name}' added. Total playlists: {Playlists.Count}");
        }

        /// <summary>
        /// Асинхронно загружает плейлисты БЕЗ треков
        /// </summary>
        public async Task RefreshPlaylistsAsync()
        {
            System.Diagnostics.Debug.WriteLine("Async loading playlists from DB...");
            AppConsole.Log("Async loading playlists from DB...");

            var list = await DatabaseService.GetPlaylistsAsync();
            System.Diagnostics.Debug.WriteLine($"Loaded playlists from DB: {list.Count}");
            AppConsole.Log($"Loaded playlists from DB: {list.Count}");
            foreach (var p in list)
            {
                System.Diagnostics.Debug.WriteLine($"  - {p.Name} (ID={p.Id})");
                AppConsole.Log($"  - {p.Name} (ID={p.Id})");
            }

            // Проверяем, существует ли плейлист "Избранное"
            var favoritesPlaylist = list.FirstOrDefault(p => p.Name == FavoritesName);
            if (favoritesPlaylist == null)
            {
                System.Diagnostics.Debug.WriteLine($"Playlist '{FavoritesName}' not found, creating it...");
                AppConsole.Log($"Playlist '{FavoritesName}' not found, creating it...");

                long newId = DatabaseService.CreatePlaylist(FavoritesName, "Your favorite tracks", null, isSystemPlaylist: true);
                favoritesPlaylist = new Playlist
                {
                    Id = (int)newId,
                    Name = FavoritesName,
                    Description = "Your favorite tracks",
                    IsSystemPlaylist = true,
                    CreatedDate = DateTime.Now
                };

                list.Add(favoritesPlaylist);
                System.Diagnostics.Debug.WriteLine($"Playlist '{FavoritesName}' successfully created (ID={newId})");
                AppConsole.Log($"Playlist '{FavoritesName}' successfully created (ID={newId})");
            }
            else
            {
                favoritesPlaylist.IsSystemPlaylist = true;
            }

            var previousPlaylistId = CurrentPlaylist?.Id;
            System.Diagnostics.Debug.WriteLine($"Current playlist BEFORE clearing: ID={previousPlaylistId}");
            AppConsole.Log($"Current playlist BEFORE clearing: ID={previousPlaylistId}");

            Playlists.Clear();

            foreach (var p in list)
            {
                System.Diagnostics.Debug.WriteLine($"Adding playlist: '{p.Name}'");
                AppConsole.Log($"Adding playlist: '{p.Name}'");
                Playlists.Add(p);
            }

            // Восстанавливаем текущий плейлист, если он был выбран
            if (CurrentPlaylist != null)
            {
                var restoredPlaylist = Playlists.FirstOrDefault(p => p.Id == CurrentPlaylist.Id);
                if (restoredPlaylist != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Restoring playlist: '{restoredPlaylist.Name}' (ID={restoredPlaylist.Id})");
                    AppConsole.Log($"Restoring playlist: '{restoredPlaylist.Name}' (ID={restoredPlaylist.Id})");
                    CurrentPlaylist = restoredPlaylist;
                }
            }

            System.Diagnostics.Debug.WriteLine($"END ASYNC PLAYLIST LOADING");
            AppConsole.Log($"END ASYNC PLAYLIST LOADING");
            OnPropertyChanged(nameof(Playlists));
        }

        /// <summary>
        /// Асинхронно загружает треки для плейлиста
        /// </summary>
        public async Task LoadPlaylistTracksAsync(Playlist playlist)
        {
            if (playlist == null) return;

            System.Diagnostics.Debug.WriteLine($"ASYNC LOADING TRACKS FOR PLAYLIST: {playlist.Name} (ID={playlist.Id})");
            AppConsole.Log($"ASYNC LOADING TRACKS FOR PLAYLIST: {playlist.Name} (ID={playlist.Id})");

            var tracks = await DatabaseService.GetTracksForPlaylistAsync(playlist.Id, playlist.SortType);

            // Очищаем старые треки
            playlist.Tracks.Clear();

            // Добавляем новые треки
            foreach (var track in tracks)
            {
                playlist.Tracks.Add(track);
            }

            System.Diagnostics.Debug.WriteLine($"Loaded {tracks.Count} tracks for playlist '{playlist.Name}' (ID={playlist.Id})");
            AppConsole.Log($"Loaded {tracks.Count} tracks for playlist '{playlist.Name}' (ID={playlist.Id})");

            // Уведомляем об изменении
            OnPropertyChanged(nameof(Playlists));
        }

        /// <summary>
        /// Асинхронно загружает все плейлисты с треками постепенно
        /// </summary>
        public async Task LoadAllPlaylistsTracksAsync(Action<int, int>? onProgress = null)
        {
            System.Diagnostics.Debug.WriteLine("ASYNC LOADING ALL TRACKS");
            AppConsole.Log("ASYNC LOADING ALL TRACKS");

            int totalPlaylists = Playlists.Count;
            for (int i = 0; i < totalPlaylists; i++)
            {
                var playlist = Playlists[i];
                await LoadPlaylistTracksAsync(playlist);

                System.Diagnostics.Debug.WriteLine($"Progress: {i + 1}/{totalPlaylists}");
                AppConsole.Log($"Progress: {i + 1}/{totalPlaylists}");
                onProgress?.Invoke(i + 1, totalPlaylists);
            }

            System.Diagnostics.Debug.WriteLine($"END ASYNC TRACK LOADING");
            AppConsole.Log($"END ASYNC TRACK LOADING");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}