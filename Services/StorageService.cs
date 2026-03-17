using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using MusicPlayer_by_d3solat1on.Models;
using MusicPlayer_by_d3solat1on.ViewModels;

namespace MusicPlayer_by_d3solat1on.Services
{
    public class StorageService
    {
        private static StorageService? _instance;
        public static StorageService Instance => _instance ??= new StorageService();

        private readonly string _appDataPath;
        private readonly string _libraryFilePath;
        private readonly string _settingsFilePath;

        public double Volume { get; set; } = 0.5;
        public int LastPlaylistId { get; set; } = -1;
        public string? LastTrackPath { get; set; }

        private StorageService()
        {
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MusicPlayer_by_d3solat1on");

            if (!Directory.Exists(_appDataPath))
                Directory.CreateDirectory(_appDataPath);

            _libraryFilePath = Path.Combine(_appDataPath, "library.json");
            _settingsFilePath = Path.Combine(_appDataPath, "settings.json");

            // Загружаем настройки при создании
            LoadSettings();
        }

        // Сохраняем библиотеку и настройки
        public void SaveLibrary()
        {
            try
            {
                // Сохраняем настройки
                SaveSettings();

                // Сохраняем библиотеку
                var data = new LibraryData
                {
                    Tracks = [.. MusicLibrary.Instance.AllTracks],
                    Playlists = [.. MusicLibrary.Instance.Playlists]
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    });

                File.WriteAllText(_libraryFilePath, json);

                System.Diagnostics.Debug.WriteLine($"Библиотека сохранена: {_libraryFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения: {ex.Message}");
            }
        }

        // Загружаем библиотеку
        public void LoadLibrary()
        {
            try
            {
                if (File.Exists(_libraryFilePath))
                {
                    var json = File.ReadAllText(_libraryFilePath);
                    var data = JsonConvert.DeserializeObject<LibraryData>(json);

                    if (data != null)
                    {
                        // 1. Сначала наполняем AllTracks
                        MusicLibrary.Instance.AllTracks.Clear();
                        if (data.Tracks != null)
                        {
                            foreach (var track in data.Tracks)
                                MusicLibrary.Instance.AllTracks.Add(track);
                        }

                        // 2. Теперь вызываем метод загрузки плейлистов в MusicLibrary
                        // Передаем туда данные из JSON
                        MusicLibrary.Instance.LoadPlaylists(data.Playlists, LastPlaylistId, LastTrackPath);
                    }
                }
                else { EnsureDefaultPlaylists(); }
            }
            catch (Exception) { /* ... */ }
        }
        private void RestorePlaylistTracks()
        {
            // Проходим по всем плейлистам и проверяем, что треки в них существуют в AllTracks
            foreach (var playlist in MusicLibrary.Instance.Playlists)
            {
                var validTracks = new List<Track>();
                foreach (var track in playlist.Tracks.ToList())
                {
                    // Ищем трек по пути в AllTracks
                    var existingTrack = MusicLibrary.Instance.AllTracks.FirstOrDefault(t => t.Path == track.Path);
                    if (existingTrack != null)
                    {
                        validTracks.Add(existingTrack);
                    }
                }

                // Заменяем треки в плейлисте на валидные
                playlist.Tracks.Clear();
                foreach (var track in validTracks)
                {
                    playlist.Tracks.Add(track);
                }
            }
        }
        private static void EnsureDefaultPlaylists()
        {
            if (!MusicLibrary.Instance.Playlists.Any(p => p.Name == "Избранное"))
            {
                MusicLibrary.Instance.CreatePlaylist("Избранное", "Ваши любимые треки");
            }

            if (MusicLibrary.Instance.CurrentPlaylist == null && MusicLibrary.Instance.Playlists.Count > 0)
            {
                MusicLibrary.Instance.CurrentPlaylist = MusicLibrary.Instance.Playlists[0];
            }
        }

        // Сохраняем настройки отдельно
        private void SaveSettings()
        {
            try
            {
                var settings = new SettingsData
                {
                    Volume = PlayerService.Instance.Volume,
                    LastPlaylistId = MusicLibrary.Instance.CurrentPlaylist?.Id ?? -1,
                    LastTrackPath = MusicLibrary.Instance.LastPlayedTrack?.Path
                };

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
            }
        }

        // Загружаем настройки
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonConvert.DeserializeObject<SettingsData>(json);

                    if (settings != null)
                    {
                        Volume = settings.Volume;
                        LastPlaylistId = settings.LastPlaylistId;
                        LastTrackPath = settings.LastTrackPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
            }
        }

        // Вспомогательные методы для сохранения отдельных значений
        public void SaveLastPlaylistId(int id)
        {
            LastPlaylistId = id;
            SaveSettings(); // Сохраняем сразу
        }

        public void SaveLastTrackPath(string? path)
        {
            LastTrackPath = path;
            SaveSettings(); // Сохраняем сразу
        }

        [Serializable]
        private class LibraryData
        {
            public List<Track>? Tracks { get; set; }
            public List<Playlist>? Playlists { get; set; }
        }

        [Serializable]
        private class SettingsData
        {
            public double Volume { get; set; } = 0.5;
            public int LastPlaylistId { get; set; } = -1;
            public string? LastTrackPath { get; set; }
        }
    }
}