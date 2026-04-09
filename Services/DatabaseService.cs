using System.IO;
using Microsoft.Data.Sqlite;
using QAMP.Models;
using QAMP.ViewModels;

namespace QAMP.Services;

public class DatabaseService
{
    // Используем AppData для надежности
    private static readonly string _appDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QAMP");

    private static readonly string _dbPath = Path.Combine(_appDataPath, "library.db");
    private static readonly string _connectionString = $"Data Source={_dbPath}";

    // Добавляем публичное свойство для доступа к пути (для отладки)
    public static string DatabasePath => _dbPath;

    public static void EnsureDatabaseCreated()
    {
        // Создаем папку, если её нет
        if (!Directory.Exists(_appDataPath))
        {
            Directory.CreateDirectory(_appDataPath);
            System.Diagnostics.Debug.WriteLine($"Создана папка: {_appDataPath}");
        }

        if (!File.Exists(_dbPath))
        {
            System.Diagnostics.Debug.WriteLine($"База данных не найдена, создаем новую: {_dbPath}");
            InitializeDatabase();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"База данных существует: {_dbPath}");
            // Проверяем и добавляем недостающие колонки
            MigrateDatabase();
        }
    }

    /// <summary>
    /// Проверяет наличие недостающих колонок и добавляет их при необходимости
    /// </summary>
    private static void MigrateDatabase()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Проверяем колонки в таблице Playlists
            var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(Playlists)";

            var playlistColumns = new HashSet<string>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    playlistColumns.Add(reader.GetString(1));
                }
            }

            // Проверяем и добавляем недостающие колонки в Playlists
            var columnsToAdd = new Dictionary<string, string>
            {
                { "IsPinned", "INTEGER DEFAULT 0" },
                { "SortOrder", "INTEGER DEFAULT 0" },
                { "SortType", "INTEGER DEFAULT 0" },  // 0 = AddedDate, 1 = AlbumAZ, 2 = ExecutorAZ, 3 = NameAZ
                { "CreatedDate", "TEXT" }  // Дата создания плейлиста
            };

            foreach (var column in columnsToAdd)
            {
                if (!playlistColumns.Contains(column.Key))
                {
                    System.Diagnostics.Debug.WriteLine($"Добавляем колонку {column.Key} в таблицу Playlists...");
                    var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE Playlists ADD COLUMN {column.Key} {column.Value}";
                    alterCmd.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine($"Колонка {column.Key} успешно добавлена");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Колонка {column.Key} уже существует");
                }
            }

            // Проверяем колонки в таблице PlaylistTracks
            var ptCmd = connection.CreateCommand();
            ptCmd.CommandText = "PRAGMA table_info(PlaylistTracks)";

            var playlistTracksColumns = new HashSet<string>();
            using (var reader = ptCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    playlistTracksColumns.Add(reader.GetString(1));
                }
            }

            // Проверяем и добавляем недостающие колонки в PlaylistTracks
            if (!playlistTracksColumns.Contains("AddedDate"))
            {
                System.Diagnostics.Debug.WriteLine("Добавляем колонку AddedDate в таблицу PlaylistTracks...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE PlaylistTracks ADD COLUMN AddedDate TEXT";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Колонка AddedDate успешно добавлена");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Колонка AddedDate уже существует в PlaylistTracks");
            }

            // Проверяем колонки в таблице Tracks
            var tracksCmd = connection.CreateCommand();
            tracksCmd.CommandText = "PRAGMA table_info(Tracks)";

            var tracksColumns = new HashSet<string>();
            using (var reader = tracksCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tracksColumns.Add(reader.GetString(1));
                }
            }

            // Проверяем и добавляем недостающие колонки в Tracks
            if (!tracksColumns.Contains("TrackNumber"))
            {
                System.Diagnostics.Debug.WriteLine("Добавляем колонку TrackNumber в таблицу Tracks...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Tracks ADD COLUMN TrackNumber INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Колонка TrackNumber успешно добавлена");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Колонка TrackNumber уже существует в Tracks");
            }
            if (!tracksColumns.Contains("PlayCount"))
            {
                System.Diagnostics.Debug.WriteLine("Добавляем колонку PlayCount в таблицу Tracks...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Tracks ADD COLUMN PlayCount INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Колонка PlayCount успешно добавлена");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Колонка PlayCount уже существует в Tracks");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при миграции БД: {ex.Message}");
        }
    }
    public static void IncrementTrackPlayCount(int trackId)
    {
        System.Diagnostics.Debug.WriteLine($"[IncrementTrackPlayCount] BEFORE - Attempting to increment PlayCount for Track ID={trackId}");
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Сначала читаем текущее значение
            var readCmd = connection.CreateCommand();
            readCmd.CommandText = "SELECT PlayCount FROM Tracks WHERE Id = $id";
            readCmd.Parameters.AddWithValue("$id", trackId);
            var currentCount = readCmd.ExecuteScalar();
            System.Diagnostics.Debug.WriteLine($"[IncrementTrackPlayCount] Current PlayCount: {(currentCount != null ? currentCount.ToString() : "NULL")}");

            // Теперь инкрементируем
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Tracks SET PlayCount = PlayCount + 1 WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", trackId);
            int rowsAffected = cmd.ExecuteNonQuery();
            System.Diagnostics.Debug.WriteLine($"[IncrementTrackPlayCount] Rows affected: {rowsAffected}");

            // Проверяем новое значение
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT PlayCount FROM Tracks WHERE Id = $id";
            checkCmd.Parameters.AddWithValue("$id", trackId);
            var newCount = checkCmd.ExecuteScalar();
            System.Diagnostics.Debug.WriteLine($"[IncrementTrackPlayCount] AFTER - New PlayCount: {(newCount != null ? newCount.ToString() : "NULL")}");

            App.LogInfo($"Statistics: Track ID {trackId} play count incremented.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IncrementTrackPlayCount] ERROR: {ex.Message}");
            App.LogException(ex, "IncrementPlayCount");
        }
    }
    public static void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();

        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Settings (
            Key TEXT PRIMARY KEY, 
            Value TEXT
        );
        CREATE TABLE IF NOT EXISTS Playlists (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            CoverImage BLOB, 
            CreatedDate TEXT,
            IsPinned INTEGER DEFAULT 0,
            SortOrder INTEGER DEFAULT 0,
            SortType INTEGER DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS Tracks (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Path TEXT UNIQUE,
            Name TEXT,
            Executor TEXT,
            Duration TEXT,
            Album TEXT,
            Genre TEXT,
            Bitrate INTEGER,
            SampleRate INTEGER,
            Year INTEGER,
            TrackNumber INTEGER DEFAULT 0,
            PlayCount INTEGER DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS PlaylistTracks (
            PlaylistId INTEGER,
            TrackId INTEGER,
            AddedDate TEXT,
            FOREIGN KEY(PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
            FOREIGN KEY(TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE
        );";
        cmd.ExecuteNonQuery();

        System.Diagnostics.Debug.WriteLine($"База данных инициализирована: {_dbPath}");
    }

    public static void AddPlaylist(string name, string description, byte[]? cover)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();

        command.CommandText = @"
        INSERT INTO Playlists (Name, Description, CoverImage, CreatedDate) 
        VALUES ($name, $description, $coverimage, $date)";

        command.Parameters.AddWithValue("$name", name ?? "");
        command.Parameters.AddWithValue("$description",
            string.IsNullOrEmpty(description) ? DBNull.Value : description);
        command.Parameters.AddWithValue("$coverimage",
            cover ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        command.ExecuteNonQuery();
    }

    public static void UpdatePlaylist(int id, string name, string description, byte[]? cover)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE Playlists 
        SET Name = $name, Description = $description, CoverImage = $cover 
        WHERE Id = $id";

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", name ?? "");
        command.Parameters.AddWithValue("$description",
            string.IsNullOrEmpty(description) ? DBNull.Value : description);
        command.Parameters.AddWithValue("$cover",
            cover ?? (object)DBNull.Value);

        command.ExecuteNonQuery();
    }

    public static void AddTrackToPlaylist(int playlistId, string filePath)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var trackCommand = connection.CreateCommand();
        trackCommand.CommandText = "INSERT OR IGNORE INTO Tracks (Path) VALUES ($path)";
        trackCommand.Parameters.AddWithValue("$path", filePath);
        trackCommand.ExecuteNonQuery();

        var getIdCommand = connection.CreateCommand();
        getIdCommand.CommandText = "SELECT Id FROM Tracks WHERE Path = $path";
        getIdCommand.Parameters.AddWithValue("$path", filePath);
        long? trackId = (long?)getIdCommand.ExecuteScalar();

        var linkCommand = connection.CreateCommand();
        linkCommand.CommandText = "INSERT INTO PlaylistTracks (PlaylistId, TrackId, AddedDate) VALUES ($pId, $tId, $date)";
        linkCommand.Parameters.AddWithValue("$pId", playlistId);
        linkCommand.Parameters.AddWithValue("$tId", trackId);
        linkCommand.Parameters.AddWithValue("$date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        linkCommand.ExecuteNonQuery();
    }

    public static bool IsTrackInPlaylist(int playlistId, int trackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM PlaylistTracks
            WHERE PlaylistId = $playlistId AND TrackId = $trackId";
        command.Parameters.AddWithValue("$playlistId", playlistId);
        command.Parameters.AddWithValue("$trackId", trackId);

        long? count = (long?)command.ExecuteScalar();
        return count > 0;
    }

    public static void AddFolderToPlaylist(int playlistId, string folderPath)
    {
        var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".flac"))
            .ToArray();

        var tracks = TagReader.ReadTracksFromFiles(files);

        foreach (var track in tracks)
        {
            if (track != null)
            {
                SaveTrackToPlaylist(playlistId, track);
            }
        }
    }

    public static bool PlaylistExists(string name, int excludeId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Playlists WHERE Name = $name AND Id != $id";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$id", excludeId);

        long? count = (long?)command.ExecuteScalar();
        return count > 0;
    }

    public static List<Playlist> GetPlaylists()
    {
        var playlists = new List<Playlist>();

        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();

            try
            {
                // Правильный порядок колонок: Id, Name, Description, CoverImage, IsPinned, SortOrder, CreatedDate, SortType
                command.CommandText = "SELECT Id, Name, Description, CoverImage, IsPinned, SortOrder, CreatedDate, SortType FROM Playlists ORDER BY IsPinned DESC, SortOrder ASC, Name ASC";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var playlist = new Playlist
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        CoverImage = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3),
                        IsPinned = !reader.IsDBNull(4) && reader.GetInt32(4) != 0
                    };

                    // Сортировка (SortOrder)
                    if (!reader.IsDBNull(5))
                    {
                        playlist.SortOrder = reader.GetInt32(5);
                    }

                    // Дата создания (CreatedDate)
                    if (!reader.IsDBNull(6))
                    {
                        string dateString = reader.GetString(6);
                        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime parsedDate))
                        {
                            playlist.CreatedDate = parsedDate;
                        }
                        else if (DateTime.TryParse(dateString, out DateTime secondTry))
                        {
                            playlist.CreatedDate = secondTry;
                        }
                        else
                        {
                            playlist.CreatedDate = DateTime.Now;
                        }
                    }
                    else
                    {
                        playlist.CreatedDate = DateTime.Now;
                    }

                    // Тип сортировки (SortType)
                    if (!reader.IsDBNull(7))
                    {
                        playlist.SortType = (TrackSortType)reader.GetInt32(7);
                    }

                    // Загружаем треки для этого плейлиста
                    var tracks = GetTracksForPlaylist(playlist.Id);
                    System.Diagnostics.Debug.WriteLine($"Загружаем треки для плейлиста '{playlist.Name}' (ID={playlist.Id}): {tracks.Count} треков, pinned={playlist.IsPinned}");

                    foreach (var track in tracks)
                    {
                        playlist.Tracks.Add(track);
                    }

                    playlists.Add(playlist);
                }
            }
            catch (SqliteException ex) when (ex.Message.Contains("IsPinned"))
            {
                // Если колонки нет, используем старый запрос
                System.Diagnostics.Debug.WriteLine($"Колонка IsPinned не найдена, выполняем миграцию: {ex.Message}");
                MigrateDatabase();

                // Повторяем запрос уже с колонкой
                command.CommandText = "SELECT Id, Name, Description, CoverImage, IsPinned, SortOrder, CreatedDate, SortType FROM Playlists ORDER BY IsPinned DESC, SortOrder ASC, Name ASC";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var playlist = new Playlist
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        CoverImage = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3),
                        IsPinned = reader.IsDBNull(4) ? false : reader.GetInt32(4) != 0,
                        SortOrder = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                    };

                    // Дата создания
                    if (!reader.IsDBNull(6))
                    {
                        string dateString = reader.GetString(6);
                        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime parsedDate))
                        {
                            playlist.CreatedDate = parsedDate;
                        }
                        else if (DateTime.TryParse(dateString, out DateTime secondTry))
                        {
                            playlist.CreatedDate = secondTry;
                        }
                        else
                        {
                            playlist.CreatedDate = DateTime.Now;
                        }
                    }
                    else
                    {
                        playlist.CreatedDate = DateTime.Now;
                    }

                    // Тип сортировки (SortType)
                    if (!reader.IsDBNull(7))
                    {
                        playlist.SortType = (TrackSortType)reader.GetInt32(7);
                    }

                    var tracks = GetTracksForPlaylist(playlist.Id);
                    foreach (var track in tracks)
                    {
                        playlist.Tracks.Add(track);
                    }

                    playlists.Add(playlist);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке плейлистов: {ex.Message}");
                throw;
            }
        }

        System.Diagnostics.Debug.WriteLine($"Всего загружено плейлистов: {playlists.Count}");
        return playlists;
    }

    /// <summary>
    /// Загружает только один плейлист по ID (оптимизация для частичного обновления)
    /// </summary>
    public static Playlist? GetPlaylistById(int playlistId)
    {
        System.Diagnostics.Debug.WriteLine($"=== ЗАГРУЗКА ПЛЕЙЛИСТА ПО ID: {playlistId} ===");

        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();

            try
            {
                command.CommandText = "SELECT Id, Name, Description, CoverImage, IsPinned, SortOrder, CreatedDate, SortType FROM Playlists WHERE Id = $id";
                command.Parameters.AddWithValue("$id", playlistId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var playlist = new Playlist
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        CoverImage = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3),
                        IsPinned = !reader.IsDBNull(4) && reader.GetInt32(4) != 0
                    };

                    // Сортировка (SortOrder)
                    if (!reader.IsDBNull(5))
                    {
                        playlist.SortOrder = reader.GetInt32(5);
                    }

                    // Дата создания (CreatedDate)
                    if (!reader.IsDBNull(6))
                    {
                        string dateString = reader.GetString(6);
                        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime parsedDate))
                        {
                            playlist.CreatedDate = parsedDate;
                        }
                        else if (DateTime.TryParse(dateString, out DateTime secondTry))
                        {
                            playlist.CreatedDate = secondTry;
                        }
                        else
                        {
                            playlist.CreatedDate = DateTime.Now;
                        }
                    }
                    else
                    {
                        playlist.CreatedDate = DateTime.Now;
                    }

                    // Тип сортировки (SortType)
                    if (!reader.IsDBNull(7))
                    {
                        playlist.SortType = (TrackSortType)reader.GetInt32(7);
                    }

                    // Загружаем треки для этого плейлиста
                    var tracks = GetTracksForPlaylist(playlist.Id);
                    System.Diagnostics.Debug.WriteLine($"Загружен плейлист '{playlist.Name}' (ID={playlist.Id}): {tracks.Count} треков");

                    foreach (var track in tracks)
                    {
                        playlist.Tracks.Add(track);
                    }

                    return playlist;
                }

                System.Diagnostics.Debug.WriteLine($"Плейлист с ID={playlistId} не найден в БД");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке плейлиста ID={playlistId}: {ex.Message}");
                throw;
            }
        }
    }

    public static void SaveTrackToPlaylist(int playlistId, Track track)
    {
        System.Diagnostics.Debug.WriteLine($"=== СОХРАНЕНИЕ ТРЕКА В ПЛЕЙЛИСТ {playlistId} ===");
        System.Diagnostics.Debug.WriteLine($"Путь: {track.Path}");
        System.Diagnostics.Debug.WriteLine($"Имя: {track.Name}");
        System.Diagnostics.Debug.WriteLine($"TrackNumber: {track.TrackNumber}");
        System.Diagnostics.Debug.WriteLine($"TrackId из объекта: {track.Id}");

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Сначала проверяем, существует ли трек с таким Path
        var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT Id FROM Tracks WHERE Path = $path";
        checkCmd.Parameters.AddWithValue("$path", track.Path);
        var existingId = checkCmd.ExecuteScalar();

        long trackId;

        if (existingId != null)
        {
            // Трек уже существует, обновляем его данные
            trackId = (long)existingId;
            System.Diagnostics.Debug.WriteLine($"Трек уже существует с ID={trackId}, обновляем данные");

            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Tracks 
                SET Name = $name, Executor = $exec, Album = $album, Duration = $dur, 
                    Genre = $genre, Bitrate = $bit, SampleRate = $samplerate, Year = $year, TrackNumber = $trackNum
                WHERE Id = $id";

            updateCmd.Parameters.AddWithValue("$id", trackId);
            updateCmd.Parameters.AddWithValue("$name", (object)track.Name ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("$exec", (object)track.Executor ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("$album", (object)track.Album ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("$dur", (object)track.Duration ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("$genre", (object)track.Genre ?? "Неизвестно");
            updateCmd.Parameters.AddWithValue("$bit", track.Bitrate > 0 ? track.Bitrate : DBNull.Value);
            updateCmd.Parameters.AddWithValue("$samplerate", track.SampleRate > 0 ? track.SampleRate : DBNull.Value);
            updateCmd.Parameters.AddWithValue("$year", track.Year > 0 ? track.Year : DBNull.Value);
            updateCmd.Parameters.AddWithValue("$trackNum", track.TrackNumber > 0 ? track.TrackNumber : 0);

            int rowsAffected = updateCmd.ExecuteNonQuery();
            System.Diagnostics.Debug.WriteLine($"Трек обновлен, affected rows: {rowsAffected}");
        }
        else
        {
            // Трека нет, создаем новый
            System.Diagnostics.Debug.WriteLine($"Трек не существует, создаем новый");

            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Tracks (Path, Name, Executor, Album, Duration, Genre, Bitrate, SampleRate, Year, TrackNumber)
                VALUES ($path, $name, $exec, $album, $dur, $genre, $bit, $samplerate, $year, $trackNum)";

            insertCmd.Parameters.AddWithValue("$path", track.Path);
            insertCmd.Parameters.AddWithValue("$name", (object)track.Name ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("$exec", (object)track.Executor ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("$album", (object)track.Album ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("$dur", (object)track.Duration ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("$genre", (object)track.Genre ?? "Неизвестно");
            insertCmd.Parameters.AddWithValue("$bit", track.Bitrate > 0 ? track.Bitrate : DBNull.Value);
            insertCmd.Parameters.AddWithValue("$samplerate", track.SampleRate > 0 ? track.SampleRate : DBNull.Value);
            insertCmd.Parameters.AddWithValue("$year", track.Year > 0 ? track.Year : DBNull.Value);
            insertCmd.Parameters.AddWithValue("$trackNum", track.TrackNumber > 0 ? track.TrackNumber : 0);

            int rowsAffected = insertCmd.ExecuteNonQuery();
            System.Diagnostics.Debug.WriteLine($"Трек вставлен, affected rows: {rowsAffected}");

            // Получаем ID новосозданного трека
            var getIdCmd = connection.CreateCommand();
            getIdCmd.CommandText = "SELECT Id FROM Tracks WHERE Path = $path";
            getIdCmd.Parameters.AddWithValue("$path", track.Path);
            var result = getIdCmd.ExecuteScalar();
            if (result != null)
            {
                trackId = (long)result;
                System.Diagnostics.Debug.WriteLine($"Новый трек ID: {trackId}");
            }
            else
            {
                throw new Exception("Не удалось получить ID для нового трека после вставки");
            }

        }

        System.Diagnostics.Debug.WriteLine($"Финальный ID трека в БД: {trackId}");

        // Добавляем связь с плейлистом
        var checkLinkCmd = connection.CreateCommand();
        checkLinkCmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId = $pId AND TrackId = $tId";
        checkLinkCmd.Parameters.AddWithValue("$pId", playlistId);
        checkLinkCmd.Parameters.AddWithValue("$tId", trackId);
        long? existingLink = (long?)checkLinkCmd.ExecuteScalar();

        if (existingLink == 0)
        {
            // Новая связь - добавляем с текущей датой
            var linkCmd = connection.CreateCommand();
            linkCmd.CommandText = "INSERT INTO PlaylistTracks (PlaylistId, TrackId, AddedDate) VALUES ($pId, $tId, $date)";
            linkCmd.Parameters.AddWithValue("$pId", playlistId);
            linkCmd.Parameters.AddWithValue("$tId", trackId);
            linkCmd.Parameters.AddWithValue("$date", track.AddedDate.ToString("o")); // ISO 8601 формат
            int linkResult = linkCmd.ExecuteNonQuery();
            System.Diagnostics.Debug.WriteLine($"Новая связь добавлена, результат: {linkResult}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Связь уже существует, пропускаем добавление");
        }

        // Проверяем, сколько плейлистов содержат этот трек
        var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE TrackId = $trackId";
        countCmd.Parameters.AddWithValue("$trackId", trackId);
        long? playlistCount = (long?)countCmd.ExecuteScalar();
        System.Diagnostics.Debug.WriteLine($"ТРЕК СОДЕРЖИТСЯ В {playlistCount} ПЛЕЙЛИСТАХ");
    }

    public static void RemoveTrackFromPlaylist(int playlistId, int trackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();

        command.CommandText = @"
            DELETE FROM PlaylistTracks 
            WHERE PlaylistId = $pId AND TrackId = $tId";

        command.Parameters.AddWithValue("$pId", playlistId);
        command.Parameters.AddWithValue("$tId", trackId);

        command.ExecuteNonQuery();
    }

    public static List<Track> GetTracksForPlaylist(int playlistId)
    {
        var tracks = new List<Track>();
        System.Diagnostics.Debug.WriteLine($"=== GetTracksForPlaylist для ID={playlistId} ===");

        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();

            // Сначала проверим, есть ли связи в PlaylistTracks
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId = $pId";
            checkCmd.Parameters.AddWithValue("$pId", playlistId);
            long? linksCount = (long?)checkCmd.ExecuteScalar();
            System.Diagnostics.Debug.WriteLine($"Связей в PlaylistTracks: {linksCount}");

            if (linksCount == 0)
            {
                System.Diagnostics.Debug.WriteLine("Нет связей для этого плейлиста!");
                return tracks;
            }

            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT t.Id, t.Path, t.Name, t.Executor, t.Album, t.Duration, t.Genre, t.Bitrate, t.SampleRate, t.Year, t.TrackNumber, pt.AddedDate, COALESCE(t.PlayCount, 0) as PlayCount
                FROM Tracks t
                INNER JOIN PlaylistTracks pt ON t.Id = pt.TrackId
                WHERE pt.PlaylistId = $pId";

                command.Parameters.AddWithValue("$pId", playlistId);

                using var reader = command.ExecuteReader();
                int count = 0;
                while (reader.Read())
                {
                    var track = new Track
                    {
                        Id = reader.GetInt32(0),
                        Path = reader.GetString(1),
                        Name = reader.IsDBNull(2) ? "Без названия" : reader.GetString(2),
                        Executor = reader.IsDBNull(3) ? "Неизвестен" : reader.GetString(3),
                        Album = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Duration = reader.IsDBNull(5) ? "00:00" : reader.GetString(5),
                        Genre = reader.IsDBNull(6) ? "Неизвестно" : reader.GetString(6),
                        Bitrate = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                        SampleRate = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                        Year = reader.IsDBNull(9) ? 0 : reader.GetInt16(9),
                        TrackNumber = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                        AddedDate = reader.IsDBNull(11) ? DateTime.Now : DateTime.Parse(reader.GetString(11)),
                        PlayCount = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                    };

                    System.Diagnostics.Debug.WriteLine($"  Трек {++count}: ID={track.Id}, Name={track.Name}, PlayCount={track.PlayCount}, Executor={track.Executor}, TrackNumber={track.TrackNumber}, AddedDate={track.AddedDate:dd.MM.yyyy HH:mm}");
                    tracks.Add(track);
                }

                System.Diagnostics.Debug.WriteLine($"Всего треков загружено: {tracks.Count}");
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such column: t.TrackNumber"))
            {
                // Если колонки TrackNumber нет (старая БД), выполняем миграцию
                System.Diagnostics.Debug.WriteLine($"Колонка TrackNumber не найдена, выполняем миграцию: {ex.Message}");
                MigrateDatabase();

                // Пытаемся еще раз с правильным SELECT
                var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT t.Id, t.Path, t.Name, t.Executor, t.Album, t.Duration, t.Genre, t.Bitrate, t.SampleRate, t.Year, t.TrackNumber, pt.AddedDate, COALESCE(t.PlayCount, 0) as PlayCount
                FROM Tracks t
                INNER JOIN PlaylistTracks pt ON t.Id = pt.TrackId
                WHERE pt.PlaylistId = $pId";

                command.Parameters.AddWithValue("$pId", playlistId);

                using var reader = command.ExecuteReader();
                int count = 0;
                while (reader.Read())
                {
                    var track = new Track
                    {
                        Id = reader.GetInt32(0),
                        Path = reader.GetString(1),
                        Name = reader.IsDBNull(2) ? "Без названия" : reader.GetString(2),
                        Executor = reader.IsDBNull(3) ? "Неизвестен" : reader.GetString(3),
                        Album = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Duration = reader.IsDBNull(5) ? "00:00" : reader.GetString(5),
                        Genre = reader.IsDBNull(6) ? "Неизвестно" : reader.GetString(6),
                        Bitrate = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                        SampleRate = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                        Year = reader.IsDBNull(9) ? 0 : reader.GetInt16(9),
                        TrackNumber = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                        AddedDate = reader.IsDBNull(11) ? DateTime.Now : DateTime.Parse(reader.GetString(11)),
                        PlayCount = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                    };

                    System.Diagnostics.Debug.WriteLine($"  Трек {++count}: ID={track.Id}, Name={track.Name}, PlayCount={track.PlayCount}, Executor={track.Executor}, TrackNumber={track.TrackNumber}, AddedDate={track.AddedDate:dd.MM.yyyy HH:mm}");
                    tracks.Add(track);
                }

                System.Diagnostics.Debug.WriteLine($"Всего треков загружено: {tracks.Count}");
            }
        }
        return tracks;
    }

    public static long CreatePlaylist(string name, string description = "", byte[]? coverImage = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
        INSERT INTO Playlists (Name, Description, CoverImage, CreatedDate)
        VALUES ($name, $desc, $cover, $date);
        SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("$name", name ?? "");
        command.Parameters.AddWithValue("$desc",
            string.IsNullOrEmpty(description) ? DBNull.Value : description);
        command.Parameters.AddWithValue("$cover",
            coverImage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        try
        {
            return (long?)command.ExecuteScalar() ?? 0;
        }
        catch (SqliteException ex)
        {
            System.Diagnostics.Debug.WriteLine($"SQL Error: {ex.Message}");
            throw;
        }
    }

    public static void DeletePlaylist(int playlistId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Playlists WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", playlistId);
        cmd.ExecuteNonQuery();
    }
    public static void SavePlaylistsOrder()
    {
        var playlists = MusicLibrary.Instance.Playlists;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Playlists SET SortOrder = @order WHERE Id = @id";

        var orderParam = command.Parameters.Add("@order", SqliteType.Integer);
        var idParam = command.Parameters.Add("@id", SqliteType.Integer);

        for (int i = 0; i < playlists.Count; i++)
        {
            orderParam.Value = i;
            idParam.Value = playlists[i].Id;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public static void SaveSettingSync(string key, string value)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ($key, $val)";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$val", value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Логируем в файл без MessageBox при закрытии приложения (чтобы избежать StackOverflow)
            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            string message = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] [SaveSettingSync]\n{ex}\n";
            message += "----------------------------------------------------------------\n";
            try
            {
                File.AppendAllText(logPath, message);
            }
            catch
            {
                // Игнорируем ошибку логирования
            }
        }
    }

    public static async Task SaveSetting(string key, string value)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ($key, $val)";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$val", value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // Логируем в файл без MessageBox при закрытии приложения (чтобы избежать StackOverflow)
            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            string message = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] [SaveSetting]\n{ex}\n";
            message += "----------------------------------------------------------------\n";
            try
            {
                System.IO.File.AppendAllText(logPath, message);
            }
            catch
            {
                // Игнорируем ошибку логирования
            }
        }
    }

    public static string GetSetting(string key, string defaultValue = "")
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    /// <summary>
    /// Обновляет состояние закрепления плейлиста
    /// </summary>
    public static void UpdatePlaylistPinnedState(int playlistId, bool isPinned)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Playlists SET IsPinned = $pinned WHERE Id = $id";
            cmd.Parameters.AddWithValue("$pinned", isPinned ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", playlistId);
            cmd.ExecuteNonQuery();
            string status = isPinned ? "закреплен" : "откреплен";
            System.Diagnostics.Debug.WriteLine($"Плейлист {playlistId} {status}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении pin состояния: {ex.Message}");
        }
    }

    public static void DebugDirectDatabaseCheck()
    {
        System.Diagnostics.Debug.WriteLine("=== ПРЯМАЯ ПРОВЕРКА БД ===");

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Проверяем все треки в Tracks
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Tracks";
        long? totalTracks = (long?)cmd.ExecuteScalar();
        System.Diagnostics.Debug.WriteLine($"Всего треков в Tracks: {totalTracks ?? 0}");

        // Выводим первые 5 треков
        cmd.CommandText = "SELECT Id, Path, Name FROM Tracks LIMIT 5";
        using var reader = cmd.ExecuteReader();
        System.Diagnostics.Debug.WriteLine("Первые 5 треков в Tracks:");
        while (reader.Read())
        {
            System.Diagnostics.Debug.WriteLine($"  ID={reader.GetInt32(0)}, Path={reader.GetString(1)}, Name={(reader.IsDBNull(2) ? "NULL" : reader.GetString(2))}");
        }

        // Проверяем все плейлисты
        cmd.CommandText = "SELECT Id, Name FROM Playlists";
        using var playlistReader = cmd.ExecuteReader();
        System.Diagnostics.Debug.WriteLine("Плейлисты:");
        while (playlistReader.Read())
        {
            int playlistId = playlistReader.GetInt32(0);
            string playlistName = playlistReader.GetString(1);

            // Считаем связи для этого плейлиста
            var linkCmd = connection.CreateCommand();
            linkCmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId = $id";
            linkCmd.Parameters.AddWithValue("$id", playlistId);
            long? linkCount = (long?)linkCmd.ExecuteScalar();

            System.Diagnostics.Debug.WriteLine($"  {playlistName} (ID={playlistId}): {linkCount ?? 0} связей");

            // Выводим треки для этого плейлиста
            if (linkCount > 0)
            {
                var trackCmd = connection.CreateCommand();
                trackCmd.CommandText = @"
                SELECT t.Name, t.Executor 
                FROM Tracks t
                JOIN PlaylistTracks pt ON t.Id = pt.TrackId
                WHERE pt.PlaylistId = $id";
                trackCmd.Parameters.AddWithValue("$id", playlistId);

                using var trackReader = trackCmd.ExecuteReader();
                while (trackReader.Read())
                {
                    System.Diagnostics.Debug.WriteLine($"    - {trackReader.GetString(0)} by {trackReader.GetString(1)}");
                }
            }
        }
    }

    /// <summary>
    /// Обновляет тип сортировки для плейлиста
    /// </summary>
    public static void UpdatePlaylistSortType(int playlistId, TrackSortType sortType)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Playlists SET SortType = $sortType WHERE Id = $id";
            cmd.Parameters.AddWithValue("$sortType", (int)sortType);
            cmd.Parameters.AddWithValue("$id", playlistId);
            cmd.ExecuteNonQuery();
            System.Diagnostics.Debug.WriteLine($"Плейлист {playlistId}: тип сортировки изменен на {sortType}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении типа сортировки: {ex.Message}");
        }
    }
}