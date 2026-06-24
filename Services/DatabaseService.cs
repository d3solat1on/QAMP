using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using QAMP.Models;

namespace QAMP.Services;

public class DatabaseService
{
    // Событие для уведомления об изменении статистики
    public static event Action? StatisticsChanged;

    // Используем AppDataManager для управления путями
    private static readonly string _connectionString = $"Data Source={AppDataManager.DatabasePath}";

    // Публичное свойство для доступа к пути (для отладки)
    public static string DatabasePath => AppDataManager.DatabasePath;

    public static void EnsureDatabaseCreated()
    {
        // Создаем папку, если её нет
        AppDataManager.EnsureAppDataFolderExists();

        if (!File.Exists(AppDataManager.DatabasePath))
        {
            System.Diagnostics.Debug.WriteLine($"База данных не найдена, создаем новую: {AppDataManager.DatabasePath}");
            InitializeDatabase();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"База данных существует: {AppDataManager.DatabasePath}");
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
                { "CreatedDate", "TEXT" },  // Дата создания плейлиста
                { "IsSystemPlaylist", "INTEGER DEFAULT 0" }
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

            if (!tracksColumns.Contains("BPM"))
            {
                System.Diagnostics.Debug.WriteLine("Добавляем колонку BPM в таблицу Tracks...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Tracks ADD COLUMN BPM INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Колонка BPM успешно добавлена");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Колонка BPM уже существует в Tracks");
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

            // Уведомляем об изменении статистики
            StatisticsChanged?.Invoke();
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
            SortType INTEGER DEFAULT 0,
            IsSystemPlaylist INTEGER DEFAULT 0
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

        System.Diagnostics.Debug.WriteLine($"База данных инициализирована: {AppDataManager.DatabasePath}");

        // Проверяем и добавляем недостающие колонки (миграция)
        PerformDatabaseMigration();
    }

    private static void PerformDatabaseMigration()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Проверяем, существует ли колонка IsSystemPlaylist в таблице Playlists
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(Playlists)";
            using var reader = cmd.ExecuteReader();
            bool hasIsSystemPlaylist = false;

            while (reader.Read())
            {
                string columnName = reader.GetString(1);
                if (columnName == "IsSystemPlaylist")
                {
                    hasIsSystemPlaylist = true;
                    break;
                }
            }

            if (!hasIsSystemPlaylist)
            {
                System.Diagnostics.Debug.WriteLine("Колонка IsSystemPlaylist не найдена, добавляем...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Playlists ADD COLUMN IsSystemPlaylist INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Колонка IsSystemPlaylist успешно добавлена");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при миграции: {ex.Message}");
        }
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string name = reader.GetString(1);
                if (name == columnName)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
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
                // Проверяем наличие колонки IsSystemPlaylist
                bool hasIsSystemPlaylist = ColumnExists(connection, "Playlists", "IsSystemPlaylist");

                string selectQuery = hasIsSystemPlaylist
                    ? "SELECT Id, Name, Description, CoverImage, IsPinned, SortOrder, CreatedDate, SortType, IsSystemPlaylist FROM Playlists WHERE Id = $id"
                    : "SELECT Id, Name, Description, CoverImage, IsPinned, SortOrder, CreatedDate, SortType FROM Playlists WHERE Id = $id";

                command.CommandText = selectQuery;
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

                    // Флаг системного плейлиста (IsSystemPlaylist) - только если колонка есть
                    if (hasIsSystemPlaylist && !reader.IsDBNull(8))
                    {
                        playlist.IsSystemPlaylist = reader.GetInt32(8) != 0;
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

    public static long CreatePlaylist(string name, string description = "", byte[]? coverImage = null, bool isSystemPlaylist = false)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
        INSERT INTO Playlists (Name, Description, CoverImage, CreatedDate, IsSystemPlaylist)
        VALUES ($name, $desc, $cover, $date, $isSystem);
        SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("$name", name ?? "");
        command.Parameters.AddWithValue("$desc",
            string.IsNullOrEmpty(description) ? DBNull.Value : description);
        command.Parameters.AddWithValue("$cover",
            coverImage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("$isSystem", isSystemPlaylist ? 1 : 0);

        try
        {
            return (long?)command.ExecuteScalar() ?? 0; //?
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
    public static void SavePlaylistsOrder(IList<Playlist> playlists)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Playlists SET SortOrder = @order WHERE Id = @id";

            var orderParam = command.Parameters.Add("@order", SqliteType.Integer);
            var idParam = command.Parameters.Add("@id", SqliteType.Integer);

            for (int i = 0; i < playlists.Count; i++)
            {
                // 1. Обновляем значение свойства прямо в объекте в оперативной памяти,
                // чтобы WPF ICollectionView сразу подхватил новые индексы сортировки
                playlists[i].SortOrder = i;

                // 2. Записываем в базу данных SQLite
                orderParam.Value = i;
                idParam.Value = playlists[i].Id;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            System.Diagnostics.Debug.WriteLine("Порядок плейлистов успешно сохранен в БД.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при сохранении порядка плейлистов: {ex.Message}");
        }
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
            string logPath = AppDataManager.CrashLogPath;
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
            string logPath = AppDataManager.CrashLogPath;
            string message = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] [SaveSetting]\n{ex}\n";
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
    public static string GetMostListenedTracks()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT Name, Executor, PlayCount 
        FROM Tracks 
        WHERE PlayCount IS NOT NULL AND PlayCount > 0
        ORDER BY PlayCount DESC 
        LIMIT 10";

        using var reader = cmd.ExecuteReader();

        var result = new StringBuilder();
        result.AppendLine(LocalizationService.GetString("LngTopTracksHeader"));

        int rank = 1;
        while (reader.Read())
        {
            string name = reader.IsDBNull(0) ? LocalizationService.GetString("LngUntitled") : reader.GetString(0);
            string executor = reader.IsDBNull(1) ? LocalizationService.GetString("LngUnknownArtist") : reader.GetString(1);
            int playCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

            result.AppendLine(LocalizationService.GetFormattedString("LngTrackFormat", rank, name, executor, playCount));
            rank++;
        }

        return result.ToString();
    }
    public static string GetHiResKing()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT Name, Executor, Bitrate 
        FROM Tracks 
        WHERE Bitrate IS NOT NULL AND Bitrate > 0
        ORDER BY Bitrate DESC 
        LIMIT 1";

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            string name = reader.IsDBNull(0) ? LocalizationService.GetString("LngUntitled") : reader.GetString(0);
            string executor = reader.IsDBNull(1) ? LocalizationService.GetString("LngUnknownArtist") : reader.GetString(1);
            int bitrate = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

            return LocalizationService.GetFormattedString("LngHiResKingFormat", name, executor, bitrate);
        }

        return LocalizationService.GetString("LngNoDataBitrate");
    }
    public static string GetLongestTrack()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT Name, Executor, Duration 
        FROM Tracks 
        WHERE Duration IS NOT NULL AND Duration != '00:00'
        ORDER BY 
            (CAST(SUBSTR(Duration, 1, INSTR(Duration, ':') - 1) AS INTEGER) * 60 + 
             CAST(SUBSTR(Duration, INSTR(Duration, ':') + 1) AS INTEGER)) DESC 
        LIMIT 1";

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            string name = reader.IsDBNull(0) ? LocalizationService.GetString("LngUntitled") : reader.GetString(0);
            string executor = reader.IsDBNull(1) ? LocalizationService.GetString("LngUnknownArtist") : reader.GetString(1);
            string duration = reader.IsDBNull(2) ? "00:00" : reader.GetString(2);

            return LocalizationService.GetFormattedString("LngDurationFormat", name, executor, duration);
        }

        return LocalizationService.GetString("LngNoDataDuration");
    }
    public static string GetTotalLibrarySize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT SUM(CAST(SUBSTR(Duration, 1, INSTR(Duration, ':') - 1) AS INTEGER) * 60 + 
                       CAST(SUBSTR(Duration, INSTR(Duration, ':') + 1) AS INTEGER)) 
            FROM Tracks 
            WHERE Duration IS NOT NULL AND Duration != '00:00'";
        var result = cmd.ExecuteScalar();
        if (result != DBNull.Value && result != null)
        {
            long totalSeconds = (long)result;
            TimeSpan totalTime = TimeSpan.FromSeconds(totalSeconds);
            return $"{totalTime:hh\\:mm\\:ss}";
        }
        else
        {
            return "Нет данных о длительности треков.";
        }
    }
    public static string GetShortestTrack()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT Name, Executor, Duration 
        FROM Tracks 
        WHERE Duration IS NOT NULL AND Duration != '00:00'
        ORDER BY 
            (CAST(SUBSTR(Duration, 1, INSTR(Duration, ':') - 1) AS INTEGER) * 60 + 
             CAST(SUBSTR(Duration, INSTR(Duration, ':') + 1) AS INTEGER)) ASC 
        LIMIT 1";

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            string name = reader.IsDBNull(0) ? LocalizationService.GetString("LngUntitled") : reader.GetString(0);
            string executor = reader.IsDBNull(1) ? LocalizationService.GetString("LngUnknownArtist") : reader.GetString(1);
            string duration = reader.IsDBNull(2) ? "00:00" : reader.GetString(2);

            return LocalizationService.GetFormattedString("LngDurationFormat", name, executor, duration);
        }

        return LocalizationService.GetString("LngNoDataDuration");
    }
    public static string GetTotalLibraryWeight()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Path FROM Tracks WHERE Path IS NOT NULL";
            using var reader = cmd.ExecuteReader();

            double totalSizeInMB = 0;
            while (reader.Read())
            {
                string path = reader.GetString(0);
                try
                {
                    FileInfo fi = new(path);
                    if (fi.Exists)
                    {
                        totalSizeInMB += fi.Length / (1024.0 * 1024.0);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при получении размера файла {path}: {ex.Message}");
                }
            }

            if (totalSizeInMB > 1024)
            {
                double totalSizeInGB = totalSizeInMB / 1024.0;
                return $"{totalSizeInGB:F2} GB";
            }
            else
            {
                return $"{totalSizeInMB:F2} MB";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при расчете веса библиотеки: {ex.Message}");
            return "Ошибка при расчете размера библиотеки.";
        }
    }
    public static int GetPlaylistCount()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Playlists";
        long? count = (long?)cmd.ExecuteScalar();
        return (int)(count ?? 0);
    }
    public static int GetTrackCount()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Tracks";
        long? count = (long?)cmd.ExecuteScalar();
        return (int)(count ?? 0);
    }
    public static string GetMostListenedArtist()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT Executor, SUM(PlayCount) as TotalPlays 
        FROM Tracks 
        WHERE Executor IS NOT NULL AND PlayCount IS NOT NULL AND PlayCount > 0
        GROUP BY Executor 
        ORDER BY TotalPlays DESC 
        LIMIT 1";

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            string executor = reader.IsDBNull(0) ? LocalizationService.GetString("LngUnknownArtist") : reader.GetString(0);
            int totalPlays = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);

            return LocalizationService.GetFormattedString("LngArtistFormat", executor, totalPlays);
        }
        return LocalizationService.GetString("LngNoDataArtist");
    }
    public static string GetTracksWithoutListening()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT Name, Executor 
        FROM Tracks 
        WHERE PlayCount IS NULL OR PlayCount = 0
        LIMIT 20";

        using var reader = cmd.ExecuteReader();

        var result = new StringBuilder();
        result.AppendLine(LocalizationService.GetString("LngTracksWithoutListeningHeader"));

        while (reader.Read())
        {
            string name = reader.IsDBNull(0) ? LocalizationService.GetString("LngUntitled") : reader.GetString(0);
            string executor = reader.IsDBNull(1) ? LocalizationService.GetString("LngUnknownArtist") : reader.GetString(1);
            result.AppendLine($"  - {name} ({executor})");
        }
        return result.ToString();
    }

    public static void UpdateTrackMetadata(Track track)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE Tracks
        SET Name = $name,
            Executor = $exec,
            Album = $album,
            Genre = $genre,
            Year = $year,
            TrackNumber = $trackNum,
            BPM = $bpm
        WHERE Path = $path";
        cmd.Parameters.AddWithValue("$name", (object)track.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$exec", (object)track.Executor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$album", (object)track.Album ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$genre", (object)track.Genre ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$year", track.Year > 0 ? track.Year : DBNull.Value);
        cmd.Parameters.AddWithValue("$trackNum", track.TrackNumber);
        cmd.Parameters.AddWithValue("$bpm", track.BPM);
        cmd.Parameters.AddWithValue("$path", track.Path);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Асинхронная загрузка плейлистов БЕЗ треков для быстрого отображения
    /// </summary>
    public static async Task<List<Playlist>> GetPlaylistsAsync()
    {
        return await Task.Run(() =>
        {
            var playlists = new List<Playlist>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();

                try
                {
                    bool hasIsSystemPlaylist = ColumnExists(connection, "Playlists", "IsSystemPlaylist");

                    string selectQuery = hasIsSystemPlaylist
                        ? "SELECT Id, Name, Description, CoverImage, IsPinned, SortOrder, CreatedDate, SortType, IsSystemPlaylist FROM Playlists ORDER BY IsPinned DESC, SortOrder ASC, Name ASC"
                        : "SELECT Id, Name, Description, CoverImage, IsPinned, SortOrder, CreatedDate, SortType FROM Playlists ORDER BY IsPinned DESC, SortOrder ASC, Name ASC";

                    command.CommandText = selectQuery;
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

                        if (!reader.IsDBNull(5))
                        {
                            playlist.SortOrder = reader.GetInt32(5);
                        }

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

                        if (!reader.IsDBNull(7))
                        {
                            playlist.SortType = (TrackSortType)reader.GetInt32(7);
                        }

                        if (hasIsSystemPlaylist && !reader.IsDBNull(8))
                        {
                            playlist.IsSystemPlaylist = reader.GetInt32(8) != 0;
                        }

                        // НЕ загружаем треки здесь - это будет сделано отдельно асинхронно
                        playlists.Add(playlist);
                    }
                }
                catch (SqliteException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке плейлистов: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Асинхронно загружено плейлистов: {playlists.Count}");
            return playlists;
        });
    }

    /// <summary>
    /// Асинхронная загрузка треков для плейлиста
    /// </summary>
    public static async Task<List<Track>> GetTracksForPlaylistAsync(int playlistId)
    {
        return await Task.Run(() => GetTracksForPlaylist(playlistId));
    }

    public static bool IsTrackInOtherPlaylists(int trackId, int excludePlaylistId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT COUNT(*) 
        FROM PlaylistTracks 
        WHERE TrackId = $trackId AND PlaylistId != $excludePlaylistId";

        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.Parameters.AddWithValue("$excludePlaylistId", excludePlaylistId);

        int count = Convert.ToInt32(cmd.ExecuteScalar());
        return count > 0;
    }

    public static void DeleteTrackCompletely(int trackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM PlaylistTracks WHERE TrackId = $trackId";
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.ExecuteNonQuery();

        cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Tracks WHERE Id = $trackId";
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.ExecuteNonQuery();
    }
    public static void OnStatisticsChanged()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Settings SET Value = $value WHERE Key = 'StatisticsChanged'";
        cmd.Parameters.AddWithValue("$value", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }
}
