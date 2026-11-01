using System.IO;
using Microsoft.Data.Sqlite;

namespace QAMP.Services;

public partial class DatabaseService
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
            System.Diagnostics.Debug.WriteLine($"Database not found, creating new one: {AppDataManager.DatabasePath}");
            InitializeDatabase();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Database exists: {AppDataManager.DatabasePath}");
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
            EnsureTrackPlayHistoryTable(connection);

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
                    System.Diagnostics.Debug.WriteLine($"Adding column {column.Key} to Playlists table...");

                    var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE Playlists ADD COLUMN {column.Key} {column.Value}";
                    alterCmd.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine($"Column {column.Key} added successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Column {column.Key} already exists");
                }
            }

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

            if (!playlistTracksColumns.Contains("AddedDate"))
            {
                System.Diagnostics.Debug.WriteLine("Adding column AddedDate to PlaylistTracks table...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE PlaylistTracks ADD COLUMN AddedDate TEXT";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Column AddedDate added successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Column AddedDate already exists in PlaylistTracks");
            }

            if (!playlistTracksColumns.Contains("OrderIndex"))
            {
                System.Diagnostics.Debug.WriteLine("Adding column OrderIndex to PlaylistTracks table...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE PlaylistTracks ADD COLUMN OrderIndex INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Column OrderIndex added successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Column OrderIndex already exists in PlaylistTracks");
            }

            ReindexPlaylistTrackOrder(connection);

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

            if (!tracksColumns.Contains("TrackNumber"))
            {
                System.Diagnostics.Debug.WriteLine("Adding column TrackNumber to Tracks table...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Tracks ADD COLUMN TrackNumber INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Column TrackNumber added successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Column TrackNumber already exists in Tracks");
            }
            if (!tracksColumns.Contains("PlayCount"))
            {
                System.Diagnostics.Debug.WriteLine("Adding column PlayCount to Tracks table...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Tracks ADD COLUMN PlayCount INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Column PlayCount added successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Column PlayCount already exists in Tracks");
            }

            if (!tracksColumns.Contains("BPM"))
            {
                System.Diagnostics.Debug.WriteLine("Adding column BPM to Tracks table...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Tracks ADD COLUMN BPM INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Column BPM added successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Column BPM already exists in Tracks");
            }

            var addedDateCmd = connection.CreateCommand();
            addedDateCmd.CommandText = @"
                UPDATE PlaylistTracks
                SET AddedDate = COALESCE(
                    (SELECT NULLIF(CreatedDate, '') FROM Playlists WHERE Id = PlaylistTracks.PlaylistId),
                    strftime('%Y-%m-%d %H:%M:%S', 'now'))
                WHERE AddedDate IS NULL
                    OR trim(AddedDate) = ''
                    OR AddedDate LIKE '0001-%'";
            int repairedAddedDates = addedDateCmd.ExecuteNonQuery();
            if (repairedAddedDates > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Repaired {repairedAddedDates} playlist track added dates.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during DB migration: {ex.Message}");
        }
    }
    private static void EnsureTrackPlayHistoryTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS TrackPlayHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TrackId INTEGER NOT NULL,
                PlayedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_TrackPlayHistory_TrackId_PlayedAtUtc
                ON TrackPlayHistory (TrackId, PlayedAtUtc);";
        command.ExecuteNonQuery();
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
            OrderIndex INTEGER DEFAULT 0,
            FOREIGN KEY(PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
            FOREIGN KEY(TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE
        );";
        cmd.ExecuteNonQuery();

        System.Diagnostics.Debug.WriteLine($"Database initialized: {AppDataManager.DatabasePath}");

        PerformDatabaseMigration();
    }

    private static void PerformDatabaseMigration()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        EnsureTrackPlayHistoryTable(connection);

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
                System.Diagnostics.Debug.WriteLine("Column IsSystemPlaylist not found, adding...");
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Playlists ADD COLUMN IsSystemPlaylist INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Column IsSystemPlaylist added successfully");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during migration: {ex.Message}");
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

}
