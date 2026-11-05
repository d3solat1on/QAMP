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
            PerformDatabaseMigration();
        }
    }
    public static void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();

        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Settings_V2(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            LastTrackPath TEXT,
            LastTrackPosition DOUBLE,
            Volume DOUBLE,
            LastPlaylistId INTEGER
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
            IsSystemPlaylist INTEGER DEFAULT 0,
            FolderId INTEGER DEFAULT NULL
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
            EnsurePlaylistFoldersTable(connection);
            EnsureSettingsV2Table(connection);

            MigratePlaylistsTable(connection);
            MigratePlaylistTracksTable(connection);
            MigrateTracksTable(connection);

            RepairPlaylistTrackDates(connection);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during DB migration: {ex.Message}");
        }
    }
    private static void MigratePlaylistsTable(SqliteConnection connection)
    {
        var playlistColumns = GetTableColumns(connection, "Playlists");

        var columnsToAdd = new Dictionary<string, string>
    {
        { "IsPinned", "INTEGER DEFAULT 0" },
        { "SortOrder", "INTEGER DEFAULT 0" },
        { "SortType", "INTEGER DEFAULT 0" },
        { "CreatedDate", "TEXT" },
        { "IsSystemPlaylist", "INTEGER DEFAULT 0" },
        { "FolderId", "INTEGER DEFAULT NULL" }
    };

        foreach (var column in columnsToAdd)
        {
            if (!playlistColumns.Contains(column.Key))
            {
                AddColumn(connection, "Playlists", column.Key, column.Value);
            }
        }
    }

    private static void MigratePlaylistTracksTable(SqliteConnection connection)
    {
        var columns = GetTableColumns(connection, "PlaylistTracks");

        if (!columns.Contains("AddedDate"))
            AddColumn(connection, "PlaylistTracks", "AddedDate", "TEXT");

        if (!columns.Contains("OrderIndex"))
            AddColumn(connection, "PlaylistTracks", "OrderIndex", "INTEGER DEFAULT 0");

        ReindexPlaylistTrackOrder(connection);
    }

    private static void MigrateTracksTable(SqliteConnection connection)
    {
        var columns = GetTableColumns(connection, "Tracks");

        if (!columns.Contains("TrackNumber"))
            AddColumn(connection, "Tracks", "TrackNumber", "INTEGER DEFAULT 0");

        if (!columns.Contains("PlayCount"))
            AddColumn(connection, "Tracks", "PlayCount", "INTEGER DEFAULT 0");

        if (!columns.Contains("BPM"))
            AddColumn(connection, "Tracks", "BPM", "INTEGER DEFAULT 0");
    }


    private static HashSet<string> GetTableColumns(SqliteConnection connection, string tableName)
    {
        var columns = new HashSet<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }
        return columns;
    }

    private static void AddColumn(SqliteConnection connection, string tableName, string columnName, string type)
    {
        System.Diagnostics.Debug.WriteLine($"Adding column {columnName} to {tableName}...");
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {type}";
        cmd.ExecuteNonQuery();
    }

    private static void RepairPlaylistTrackDates(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        UPDATE PlaylistTracks
        SET AddedDate = COALESCE(
            (SELECT NULLIF(CreatedDate, '') FROM Playlists WHERE Id = PlaylistTracks.PlaylistId),
            strftime('%Y-%m-%d %H:%M:%S', 'now'))
        WHERE AddedDate IS NULL
            OR trim(AddedDate) = ''
            OR AddedDate LIKE '0001-%'";

        int repaired = cmd.ExecuteNonQuery();
        if (repaired > 0)
            System.Diagnostics.Debug.WriteLine($"Repaired {repaired} playlist track added dates.");
    }
    private static void EnsurePlaylistFoldersTable(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS PlaylistFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                IsExpanded INTEGER DEFAULT 1,
                IsPinned INTEGER DEFAULT 0
            );
        ";
        cmd.ExecuteNonQuery();
    }
    private static void EnsureSettingsV2Table(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Settings_V2 (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            LastTrackPath TEXT,
            LastTrackPosition DOUBLE,
            Volume DOUBLE,
            LastPlaylistId INTEGER
        );
        
        INSERT OR IGNORE INTO Settings_V2 (Id, Volume, LastTrackPosition, LastTrackPath, LastPlaylistId) 
        VALUES (1, 0.5, 0, '', -1);
    ";
        cmd.ExecuteNonQuery();
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




    private static void PerformDatabaseMigration()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        EnsureTrackPlayHistoryTable(connection);
        EnsurePlaylistFoldersTable(connection);
        EnsureSettingsV2Table(connection);

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
