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
        public const string FavoritesName = "Favorites";
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
                        _ = DatabaseService.SaveLastPlaylistId(value.Id);
                    }
                    OnPropertyChanged(nameof(CurrentPlaylist));
                }
            }
        }
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
        public ObservableCollection<PlaylistFolder> PlaylistFolders { get; } = [];
        public ObservableCollection<object> PlaylistSidebarItems { get; } = [];
        public event PropertyChangedEventHandler? PropertyChanged;

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

        private IEnumerable<Playlist> OrderPlaylistsForSidebar()
        {
            IOrderedEnumerable<Playlist> ordered = Playlists
                .OrderByDescending(playlist => playlist.IsPinned);

            return SettingsManager.Instance.Config.CurrentPlaylistSort switch
            {
                PlaylistSortOrder.NameAZ => ordered.ThenBy(playlist => playlist.Name),
                PlaylistSortOrder.NameZA => ordered.ThenByDescending(playlist => playlist.Name),
                PlaylistSortOrder.CreatedDateNewest => ordered.ThenByDescending(playlist => playlist.Id),
                PlaylistSortOrder.CreatedDateOldest => ordered.ThenBy(playlist => playlist.Id),
                PlaylistSortOrder.Custom or PlaylistSortOrder.Manual =>
                    ordered.ThenBy(playlist => playlist.SortOrder).ThenBy(playlist => playlist.Name),
                _ => ordered.ThenBy(playlist => playlist.SortOrder).ThenBy(playlist => playlist.Name)
            };
        }


        public void RefreshPlaylistSidebarItems()
        {
            foreach (var folder in PlaylistFolders.OrderByDescending(folder => folder.IsPinned).ThenBy(folder => folder.Id))
            {
                folder.PlaylistInFolder.Clear();
            }

            var unfiledPlaylists = new List<Playlist>();
            var orderedPlaylists = OrderPlaylistsForSidebar();
            foreach (var playlist in orderedPlaylists)
            {
                var folder = PlaylistFolders.FirstOrDefault(folder => folder.Id == playlist.FolderId);
                if (folder == null)
                {
                    unfiledPlaylists.Add(playlist);
                }
                else
                {
                    folder.PlaylistInFolder.Add(playlist);
                }
            }

            PlaylistSidebarItems.Clear();
            foreach (var folder in PlaylistFolders.OrderByDescending(folder => folder.IsPinned).ThenBy(folder => folder.Id))
            {
                PlaylistSidebarItems.Add(folder);
            }

            foreach (var playlist in unfiledPlaylists)
            {
                PlaylistSidebarItems.Add(playlist);
            }
        }

        /// <summary>
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
            var existingPlaylist = Playlists.FirstOrDefault(p => p.Id == updatedPlaylist.Id);
            if (existingPlaylist != null)
            {
                existingPlaylist.Name = updatedPlaylist.Name;
                existingPlaylist.Description = updatedPlaylist.Description;
                existingPlaylist.CoverImage = updatedPlaylist.CoverImage;
                existingPlaylist.FolderId = updatedPlaylist.FolderId;

                existingPlaylist.Tracks.Clear();
                foreach (var track in updatedPlaylist.Tracks)
                {
                    existingPlaylist.Tracks.Add(track);
                }

                if (CurrentPlaylist?.Id == updatedPlaylist.Id)
                {
                    OnPropertyChanged(nameof(CurrentPlaylist));
                }
            }
            else
            {
                Playlists.Add(updatedPlaylist);
            }
            RefreshPlaylistSidebarItems();
        }

        public void RefreshSinglePlaylist(int playlistId)
        {
            var freshPlaylist = DatabaseService.GetPlaylistById(playlistId);
            if (freshPlaylist != null)
            {
                UpdatePlaylist(freshPlaylist);

                var updatedPlaylist = Playlists.FirstOrDefault(p => p.Id == playlistId);
                if (updatedPlaylist != null && CurrentPlaylist?.Id == playlistId)
                {
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

            Playlists.Add(newPlaylist);
            RefreshPlaylistSidebarItems();

            System.Diagnostics.Debug.WriteLine($"Playlist '{newPlaylist.Name}' added. Total playlists: {Playlists.Count}");
        }

        /// <summary>
        /// Асинхронно загружает плейлисты БЕЗ треков
        /// </summary>
        public async Task RefreshPlaylistsAsync()
        {
            System.Diagnostics.Debug.WriteLine("Async loading playlists from DB...");

            var list = await DatabaseService.GetPlaylistsAsync();
            var folders = DatabaseService.GetAllPlaylistFolders();
            PlaylistFolders.Clear();
            foreach (var folder in folders)
            {
                PlaylistFolders.Add(folder);
            }
            System.Diagnostics.Debug.WriteLine($"Loaded playlists from DB: {list.Count}");
            foreach (var p in list)
            {
                System.Diagnostics.Debug.WriteLine($"  - {p.Name} (ID={p.Id})");
            }

            var favoritesPlaylist = list.FirstOrDefault(p => p.Name == FavoritesName);
            if (favoritesPlaylist == null)
            {
                System.Diagnostics.Debug.WriteLine($"Playlist '{FavoritesName}' not found, creating it...");

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
            }
            else
            {
                favoritesPlaylist.IsSystemPlaylist = true;
            }

            var previousPlaylistId = CurrentPlaylist?.Id;
            System.Diagnostics.Debug.WriteLine($"Current playlist BEFORE clearing: ID={previousPlaylistId}");

            Playlists.Clear();

            foreach (var p in list)
            {
                System.Diagnostics.Debug.WriteLine($"Adding playlist: '{p.Name}'");
                Playlists.Add(p);
            }
            RefreshPlaylistSidebarItems();

            if (CurrentPlaylist != null)
            {
                var restoredPlaylist = Playlists.FirstOrDefault(p => p.Id == CurrentPlaylist.Id);
                if (restoredPlaylist != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Restoring playlist: '{restoredPlaylist.Name}' (ID={restoredPlaylist.Id})");
                    CurrentPlaylist = restoredPlaylist;
                }
            }

            System.Diagnostics.Debug.WriteLine($"END ASYNC PLAYLIST LOADING");
            OnPropertyChanged(nameof(Playlists));
        }

        /// <summary>
        /// Асинхронно загружает треки для плейлиста
        /// </summary>
        public async Task LoadPlaylistTracksAsync(Playlist playlist)
        {
            if (playlist == null) return;


            var tracks = await DatabaseService.GetTracksForPlaylistAsync(playlist.Id, playlist.SortType);

            playlist.Tracks.Clear();

            foreach (var track in tracks)
            {
                playlist.Tracks.Add(track);
            }


            OnPropertyChanged(nameof(Playlists));
        }

        /// <summary>
        /// Асинхронно загружает все плейлисты с треками постепенно
        /// </summary>
        public async Task LoadAllPlaylistsTracksAsync(Action<int, int>? onProgress = null)
        {

            int totalPlaylists = Playlists.Count;
            for (int i = 0; i < totalPlaylists; i++)
            {
                var playlist = Playlists[i];
                await LoadPlaylistTracksAsync(playlist);

                onProgress?.Invoke(i + 1, totalPlaylists);
            }

        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}