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

        // Доступ к PlayerService для привязок в UI
        public static PlayerService PlayService => PlayerService.Instance;

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

        public const string FavoritesName = "Избранное";

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
                        // При смене плейлиста обновляем очередь воспроизведения
                        // UpdatePlaybackQueue();
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
        public void PlayPlaylist(Playlist playlist)
        {
            if (playlist == null) return;

            System.Diagnostics.Debug.WriteLine($"=== ВОСПРОИЗВЕДЕНИЕ ПЛЕЙЛИСТА: {playlist.Name} ===");

            // Устанавливаем плейлист из которого воспроизводится музыка
            PlayingPlaylist = playlist;

            // Обновляем очередь воспроизведения
            PlaybackQueue.Clear();
            foreach (var track in playlist.Tracks)
            {
                PlaybackQueue.Add(track);
            }

            // Начинаем с первого трека
            if (PlaybackQueue.Count > 0)
            {
                _ = PlayerService.Instance.PlayTrack(PlaybackQueue[0]);
            }
        }

        public void PlayTrackFromPlaylist(Track track, Playlist playlist)
        {
            if (track == null || playlist == null) return;

            System.Diagnostics.Debug.WriteLine($"=== ВОСПРОИЗВЕДЕНИЕ ТРЕКА: {track.Name} из плейлиста: {playlist.Name} ===");
            System.Diagnostics.Debug.WriteLine($"Трек для воспроизведения находится на позиции в playlist.Tracks: {playlist.Tracks.IndexOf(track)}");


            // Устанавливаем плейлист из которого воспроизводится музыка
            PlayingPlaylist = playlist;

            // Обновляем очередь воспроизведения этим плейлистом
            PlaybackQueue.Clear();
            foreach (var t in playlist.Tracks)
            {
                PlaybackQueue.Add(t);
            }

            // Отладка: выводим всю очередь
            System.Diagnostics.Debug.WriteLine($"PlaybackQueue после заполнения (всего {PlaybackQueue.Count} треков):");
            for (int i = 0; i < Math.Min(5, PlaybackQueue.Count); i++)
            {
                System.Diagnostics.Debug.WriteLine($"  {i}: {PlaybackQueue[i].Name}");
            }
            if (PlaybackQueue.Count > 5)
            {
                System.Diagnostics.Debug.WriteLine($"  ... еще {PlaybackQueue.Count - 5} треков");
            }
            // Найдем позицию текущего трека в очереди
            int trackIndexInQueue = PlaybackQueue.IndexOf(track);
            System.Diagnostics.Debug.WriteLine($"Позиция трека '{track.Name}' в PlaybackQueue: {trackIndexInQueue}");

            // Если включен Shuffle, обновляем перемешанную очередь
            if (PlayerService.Instance.IsShuffleEnabled)
            {
                var remainingTracks = PlaybackQueue.Where(t => t != track).OrderBy(x => Guid.NewGuid()).ToList();
                PlayerService.Instance.ShuffledQueue = [track, .. remainingTracks];
                System.Diagnostics.Debug.WriteLine($"ShuffledQueue обновлена, Count: {PlayerService.Instance.ShuffledQueue.Count}");
            }

            // Начинаем с выбранного трека
            _ = PlayerService.Instance.PlayTrack(track);
        }

        /// <summary>
        /// Воспроизводит трек из плейлиста с учетом отображаемого порядка (например, при сортировке)
        /// </summary>
        /// <param name="track">Трек для воспроизведения</param>
        /// <param name="playlist">Плейлист, из которого воспроизводится трек</param>
        /// <param name="displayOrder">Порядок треков для отображения в очереди (если null, использует playlist.Tracks)</param>
        public void PlayTrackFromPlaylist(Track track, Playlist playlist, IEnumerable<Track>? displayOrder = null)
        {
            if (track == null || playlist == null) return;

            System.Diagnostics.Debug.WriteLine($"=== ВОСПРОИЗВЕДЕНИЕ ТРЕКА: {track.Name} из плейлиста: {playlist.Name} ===");
            System.Diagnostics.Debug.WriteLine($"Использует displayOrder: {displayOrder != null}");

            // Устанавливаем плейлист из которого воспроизводится музыка
            PlayingPlaylist = playlist;

            // Обновляем очередь воспроизведения: используем displayOrder если предоставлен, иначе playlist.Tracks
            var tracksForQueue = displayOrder ?? playlist.Tracks;

            PlaybackQueue.Clear();
            foreach (var t in tracksForQueue)
            {
                PlaybackQueue.Add(t);
            }

            // Отладка: выводим всю очередь
            System.Diagnostics.Debug.WriteLine($"PlaybackQueue после заполнения (всего {PlaybackQueue.Count} треков):");
            for (int i = 0; i < Math.Min(5, PlaybackQueue.Count); i++)
            {
                System.Diagnostics.Debug.WriteLine($"  {i}: {PlaybackQueue[i].Name}");
            }
            if (PlaybackQueue.Count > 5)
            {
                System.Diagnostics.Debug.WriteLine($"  ... еще {PlaybackQueue.Count - 5} треков");
            }

            // Найдем позицию текущего трека в очереди
            int trackIndexInQueue = PlaybackQueue.IndexOf(track);
            System.Diagnostics.Debug.WriteLine($"Позиция трека '{track.Name}' в PlaybackQueue: {trackIndexInQueue}");

            // Если включен Shuffle, обновляем перемешанную очередь
            if (PlayerService.Instance.IsShuffleEnabled)
            {
                var remainingTracks = PlaybackQueue.Where(t => t != track).OrderBy(x => Guid.NewGuid()).ToList();
                PlayerService.Instance.ShuffledQueue = [track, .. remainingTracks];
                System.Diagnostics.Debug.WriteLine($"ShuffledQueue обновлена, Count: {PlayerService.Instance.ShuffledQueue.Count}");
            }

            // Начинаем с выбранного трека
            _ = PlayerService.Instance.PlayTrack(track, true);
        }

        public void SyncShuffledQueueWithCurrentTrack()
        {
            if (!PlayerService.Instance.IsShuffleEnabled) return;

            var currentTrack = PlayerService.Instance.CurrentTrack;
            if (currentTrack == null) return;

            // Проверяем, есть ли текущий трек в ShuffledQueue
            if (!PlayerService.Instance.ShuffledQueue.Contains(currentTrack))
            {
                System.Diagnostics.Debug.WriteLine("SyncShuffledQueue: Current track not in ShuffledQueue, rebuilding...");

                // Создаем новую очередь, начиная с текущего трека
                var remainingTracks = PlaybackQueue
                    .Where(t => t != currentTrack)
                    .OrderBy(x => Guid.NewGuid())
                    .ToList();

                PlayerService.Instance.ShuffledQueue = [currentTrack, .. remainingTracks];

                System.Diagnostics.Debug.WriteLine($"New ShuffledQueue count: {PlayerService.Instance.ShuffledQueue.Count}");
            }
        }
        public MusicLibrary()
        {
            // Подписываемся на изменения коллекции треков в текущем плейлисте
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CurrentPlaylist) && CurrentPlaylist != null)
                {
                    // Подписываемся на изменения треков в плейлисте
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

        public void RefreshPlaylists()
        {
            System.Diagnostics.Debug.WriteLine("=== REFRESH PLAYLISTS ===");

            var list = DatabaseService.GetPlaylists();
            System.Diagnostics.Debug.WriteLine($"Загружено плейлистов из БД: {list.Count}");
            foreach (var p in list)
            {
                System.Diagnostics.Debug.WriteLine($"  - {p.Name} (ID={p.Id}): {p.Tracks.Count} треков");
            }

            var previousPlaylistId = CurrentPlaylist?.Id;
            var previousTracksCount = CurrentPlaylist?.Tracks.Count ?? 0;
            System.Diagnostics.Debug.WriteLine($"Текущий плейлист ПЕРЕД очисткой: ID={previousPlaylistId}, треков={previousTracksCount}");

            Playlists.Clear();

            foreach (var p in list)
            {
                System.Diagnostics.Debug.WriteLine($"Добавляем плейлист: '{p.Name}', треков в коллекции: {p.Tracks.Count}");
                Playlists.Add(p);
            }

            // Восстанавливаем текущий плейлист, если он был выбран
            if (CurrentPlaylist != null)
            {
                var restoredPlaylist = Playlists.FirstOrDefault(p => p.Id == CurrentPlaylist.Id);
                if (restoredPlaylist != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Восстанавливаем плейлист: '{restoredPlaylist.Name}' (ID={restoredPlaylist.Id}), треков: {restoredPlaylist.Tracks.Count}");
                    CurrentPlaylist = restoredPlaylist;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ОШИБКА: Плейлист с ID={CurrentPlaylist.Id} не найден в восстановленном списке!");
                }
            }

            System.Diagnostics.Debug.WriteLine($"=== КОНЕЦ REFRESH PLAYLISTS ===");
            OnPropertyChanged(nameof(Playlists));
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

                    // Если у тебя DataGrid привязан к Tracks, вызови и для него
                    // OnPropertyChanged(nameof(CurrentPlaylist.Tracks));
                }
            }
        }

        /// <summary>
        /// Добавляет новый плейлист в коллекцию (для оптимизации при создании нового)
        /// </summary>
        public void AddNewPlaylist(Playlist newPlaylist)
        {
            System.Diagnostics.Debug.WriteLine($"=== ДОБАВЛЕНИЕ НОВОГО ПЛЕЙЛИСТА: '{newPlaylist.Name}' (ID={newPlaylist.Id}) ===");

            // Добавляем в коллекцию
            Playlists.Add(newPlaylist);

            System.Diagnostics.Debug.WriteLine($"Плейлист добавлен. Всего плейлистов: {Playlists.Count}");
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}