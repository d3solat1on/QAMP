using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
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
                        DatabaseService.SaveSetting("LastPlaylistId", value.Id.ToString());
                        // При смене плейлиста обновляем очередь воспроизведения
                        // UpdatePlaybackQueue();
                    }
                    OnPropertyChanged(nameof(CurrentPlaylist));
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

            // Обновляем очередь воспроизведения
            PlaybackQueue.Clear();
            foreach (var track in playlist.Tracks)
            {
                PlaybackQueue.Add(track);
            }

            // Начинаем с первого трека
            if (PlaybackQueue.Count > 0)
            {
                PlayerService.Instance.PlayTrack(PlaybackQueue[0]);
            }
        }

        public void PlayTrackFromPlaylist(Track track, Playlist playlist)
        {
            if (track == null || playlist == null) return;

            System.Diagnostics.Debug.WriteLine($"=== ВОСПРОИЗВЕДЕНИЕ ТРЕКА: {track.Name} из плейлиста: {playlist.Name} ===");

            // Обновляем очередь воспроизведения этим плейлистом
            PlaybackQueue.Clear();
            foreach (var t in playlist.Tracks)
            {
                PlaybackQueue.Add(t);
            }

            // Если включен Shuffle, обновляем перемешанную очередь
            if (PlayerService.Instance.IsShuffleEnabled)
            {
                var remainingTracks = PlaybackQueue.Where(t => t != track).OrderBy(x => Guid.NewGuid()).ToList();
                PlayerService.Instance.ShuffledQueue = [track, .. remainingTracks];
                System.Diagnostics.Debug.WriteLine($"ShuffledQueue обновлена, Count: {PlayerService.Instance.ShuffledQueue.Count}");
            }

            // Начинаем с выбранного трека
            PlayerService.Instance.PlayTrack(track);
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
                    System.Diagnostics.Debug.WriteLine($"Восстанавливаем плейлист: '{restoredPlaylist.Name}', треков: {restoredPlaylist.Tracks.Count}");
                    CurrentPlaylist = restoredPlaylist;
                }
            }

            OnPropertyChanged(nameof(Playlists));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}