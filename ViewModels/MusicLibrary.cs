using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using MusicPlayer_by_d3solat1on.Dialogs;
using MusicPlayer_by_d3solat1on.Models;
using MusicPlayer_by_d3solat1on.Services;

namespace MusicPlayer_by_d3solat1on.ViewModels
{
    public class MusicLibrary : INotifyPropertyChanged
    {
        private static MusicLibrary? _instance;
        public static MusicLibrary Instance => _instance ??= new MusicLibrary();

        // Все треки
        public ObservableCollection<Track> AllTracks { get; set; } = [];

        // Все плейлисты
        public ObservableCollection<Playlist> Playlists { get; set; } = [];
        public const string FavoritesName = "Избранное";

        // Сохраняем ID последнего выбранного плейлиста
        private int _lastPlaylistId = -1;

        private MusicLibrary()
        {
            Playlists ??= [];

            // Убеждаемся что "Избранное" существует
            EnsureFavoritesPlaylist();

            // Не выбираем плейлист здесь! Это будет сделано после загрузки
            OnPropertyChanged(nameof(Playlists));
        }

        private void EnsureFavoritesPlaylist()
        {
            if (!Playlists.Any(p => p.Name == FavoritesName))
            {
                byte[]? defaultImageData = null;
                try
                {
                    var uri = new Uri("pack://application:,,,/Resources/favorites_cover.png");
                    var info = Application.GetResourceStream(uri);
                    if (info != null)
                    {
                        using var ms = new System.IO.MemoryStream();
                        info.Stream.CopyTo(ms);
                        defaultImageData = ms.ToArray();
                    }
                }
                catch { /* Если файл не найден, останется null */ }

                Playlists.Add(new Playlist
                {
                    Id = 0,
                    Name = FavoritesName,
                    Description = "Ваши любимые треки",
                    CreatedDate = DateTime.Now,
                    CoverImage = defaultImageData,
                    Tracks = []
                });
            }
        }

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

                    // Сохраняем ID выбранного плейлиста
                    if (value != null)
                    {
                        _lastPlaylistId = value.Id;
                        // Можно сразу сохранять в StorageService
                        StorageService.Instance.SaveLastPlaylistId(value.Id);
                    }

                    OnPropertyChanged(nameof(CurrentPlaylist));
                    OnPropertyChanged(nameof(CurrentTracks));
                }
            }
        }

        // Треки текущего плейлиста
        public ObservableCollection<Track> CurrentTracks
        {
            get => CurrentPlaylist?.Tracks ?? AllTracks;
        }

        // Последний проигранный трек
        private Track? _lastPlayedTrack;
        public Track? LastPlayedTrack
        {
            get => _lastPlayedTrack;
            set
            {
                _lastPlayedTrack = value;
                OnPropertyChanged(nameof(LastPlayedTrack));

                // Сохраняем путь последнего трека
                if (value != null)
                {
                    StorageService.Instance.SaveLastTrackPath(value.Path);
                }
            }
        }

        // Текущий проигрываемый трек
        private Track? _currentTrack;
        public Track? CurrentTrack
        {
            get => _currentTrack;
            set
            {
                _currentTrack = value;
                OnPropertyChanged(nameof(CurrentTrack));
            }
        }

        public void LoadPlaylists(IEnumerable<Playlist> loadedPlaylists, int lastPlaylistId = -1, string? lastTrackPath = null)
        {
            // Очищаем и заполняем
            Playlists.Clear();
            if (loadedPlaylists != null)
            {
                foreach (var playlist in loadedPlaylists)
                {
                    Playlists.Add(playlist);
                }
            }

            // Проверяем "Избранное"
            EnsureFavoritesPlaylist();

            // Восстанавливаем выбор
            if (lastPlaylistId >= 0)
                CurrentPlaylist = Playlists.FirstOrDefault(p => p.Id == lastPlaylistId);

            CurrentPlaylist ??= Playlists.FirstOrDefault(p => p.Name == FavoritesName);

            // КРИТИЧНО: Уведомляем UI, что списки обновились
            OnPropertyChanged(nameof(Playlists));
            OnPropertyChanged(nameof(CurrentTracks));
        }
        public void AddTracks(string[] filePaths)
        {
            var tracks = TagReader.ReadTracksFromFiles(filePaths);
            foreach (var track in tracks)
            {
                if (track != null && !AllTracks.Any(t => t.Path == track.Path))
                    AllTracks.Add(track);
            }
        }

        public void AddTracksFromFolder(string folderPath)
        {
            var tracks = TagReader.ReadTracksFromFolder(folderPath);
            foreach (var track in tracks)
            {
                if (track != null && !AllTracks.Any(t => t.Path == track.Path))
                    AllTracks.Add(track);
            }
        }

        public Playlist CreatePlaylist(string name, string description = "", byte[]? coverImage = null)
        {
            // Генерируем уникальный ID
            int newId = Playlists.Count > 0 ? Playlists.Max(p => p.Id) + 1 : 1;

            var playlist = new Playlist
            {
                Id = newId,
                Name = name,
                Description = description,
                CoverImage = coverImage,
                CreatedDate = DateTime.Now,
                Tracks = []
            };

            Playlists.Add(playlist);
            return playlist;
        }

        public void AddTracksToCurrentPlaylist(string[] filePaths)
        {
            if (CurrentPlaylist == null)
            {
                NotificationWindow.Show("Сначала выберите плейлист", Application.Current.MainWindow);
                return;
            }

            var tracks = TagReader.ReadTracksFromFiles(filePaths);
            foreach (var track in tracks)
            {
                if (track != null)
                {
                    // Добавляем в общую библиотеку, если там ещё нет
                    if (!AllTracks.Any(t => t.Path == track.Path))
                    {
                        AllTracks.Add(track);
                    }
                    // Добавляем в текущий плейлист (проверяем дубликаты)
                    if (!CurrentPlaylist.Tracks.Any(t => t.Path == track.Path))
                    {
                        CurrentPlaylist.Tracks.Add(track);
                    }
                }
            }

            OnPropertyChanged(nameof(CurrentTracks));
        }

        public void AddTracksFromFolderToCurrentPlaylist(string folderPath)
        {
            if (CurrentPlaylist == null)
            {
                NotificationWindow.Show("Сначала выберите плейлист", Application.Current.MainWindow);
                return;
            }

            var tracks = TagReader.ReadTracksFromFolder(folderPath);
            foreach (var track in tracks)
            {
                if (track != null)
                {
                    if (!AllTracks.Any(t => t.Path == track.Path))
                    {
                        AllTracks.Add(track);
                    }
                    if (!CurrentPlaylist.Tracks.Any(t => t.Path == track.Path))
                    {
                        CurrentPlaylist.Tracks.Add(track);
                    }
                }
            }

            OnPropertyChanged(nameof(CurrentTracks));
        }

        public void UpdatePlaylistView()
        {
            OnPropertyChanged(nameof(CurrentPlaylist));
            OnPropertyChanged(nameof(CurrentTracks));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}