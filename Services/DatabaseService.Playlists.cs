using Microsoft.Data.Sqlite;
using QAMP.Models;

namespace QAMP.Services;

public partial class DatabaseService
{
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
        System.Diagnostics.Debug.WriteLine($"Loading playlist by ID: {playlistId}");

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();

        try
        {
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

                if (!reader.IsDBNull(7))
                {
                    playlist.SortType = (TrackSortType)reader.GetInt32(7);
                }

                var tracks = GetTracksForPlaylist(playlist.Id, playlist.SortType);
                System.Diagnostics.Debug.WriteLine($"Loaded '{playlist.Name}' (ID={playlist.Id}): {tracks.Count} tracks");


                foreach (var track in tracks)
                {
                    playlist.Tracks.Add(track);
                }

                return playlist;
            }

            System.Diagnostics.Debug.WriteLine($"Playlist with ID={playlistId} not found in DB");

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error while loading playlist ID={playlistId}: {ex.Message}");

            throw;
        }
    }

    public static void SaveTrackToPlaylist(int playlistId, Track track)
    {
        System.Diagnostics.Debug.WriteLine($"SAVING TRACK TO PLAYLIST {playlistId}");
        System.Diagnostics.Debug.WriteLine($"Path: {track.Path}");
        System.Diagnostics.Debug.WriteLine($"Name: {track.Name}");
        System.Diagnostics.Debug.WriteLine($"TrackNumber: {track.TrackNumber}");
        System.Diagnostics.Debug.WriteLine($"TrackId from object: {track.Id}");

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT Id FROM Tracks WHERE Path = $path";
        checkCmd.Parameters.AddWithValue("$path", track.Path);
        var existingId = checkCmd.ExecuteScalar();

        long trackId;

        if (existingId != null)
        {
            trackId = (long)existingId;
            System.Diagnostics.Debug.WriteLine($"Track already exists with ID={trackId}, updating data");


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
            updateCmd.Parameters.AddWithValue("$genre", (object)track.Genre ?? "Unknown");
            updateCmd.Parameters.AddWithValue("$bit", track.Bitrate > 0 ? track.Bitrate : DBNull.Value);
            updateCmd.Parameters.AddWithValue("$samplerate", track.SampleRate > 0 ? track.SampleRate : DBNull.Value);
            updateCmd.Parameters.AddWithValue("$year", track.Year > 0 ? track.Year : DBNull.Value);
            updateCmd.Parameters.AddWithValue("$trackNum", track.TrackNumber > 0 ? track.TrackNumber : 0);

            int rowsAffected = updateCmd.ExecuteNonQuery();
            System.Diagnostics.Debug.WriteLine($"Track updated, affected rows: {rowsAffected}");

        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Track does not exist, creating new");


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
            System.Diagnostics.Debug.WriteLine($"Track inserted, affected rows: {rowsAffected}");


            var getIdCmd = connection.CreateCommand();
            getIdCmd.CommandText = "SELECT Id FROM Tracks WHERE Path = $path";
            getIdCmd.Parameters.AddWithValue("$path", track.Path);
            var result = getIdCmd.ExecuteScalar();
            if (result != null)
            {
                trackId = (long)result;
                System.Diagnostics.Debug.WriteLine($"New track ID: {trackId}");

            }
            else
            {
                throw new Exception("Failed to get ID for new track after insertion");
            }

        }

        System.Diagnostics.Debug.WriteLine($"Final track ID in DB: {trackId}");

        var checkLinkCmd = connection.CreateCommand();
        checkLinkCmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId = $pId AND TrackId = $tId";
        checkLinkCmd.Parameters.AddWithValue("$pId", playlistId);
        checkLinkCmd.Parameters.AddWithValue("$tId", trackId);
        long? existingLink = (long?)checkLinkCmd.ExecuteScalar();

        if (existingLink == 0)
        {
            var linkCmd = connection.CreateCommand();
            linkCmd.CommandText = "INSERT INTO PlaylistTracks (PlaylistId, TrackId, AddedDate, OrderIndex) VALUES ($pId, $tId, $date, $order)";
            linkCmd.Parameters.AddWithValue("$pId", playlistId);
            linkCmd.Parameters.AddWithValue("$tId", trackId);
            linkCmd.Parameters.AddWithValue("$date", track.AddedDate.ToString("o")); // ISO 8601 формат
            linkCmd.Parameters.AddWithValue("$order", GetNextPlaylistTrackOrder(connection, playlistId));
            int linkResult = linkCmd.ExecuteNonQuery();
            System.Diagnostics.Debug.WriteLine($"New link added, result: {linkResult}");

        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Link already exists, skipping addition");
        }

        var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE TrackId = $trackId";
        countCmd.Parameters.AddWithValue("$trackId", trackId);
        long? playlistCount = (long?)countCmd.ExecuteScalar();
        System.Diagnostics.Debug.WriteLine($"Track is in {playlistCount} playlists");

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

        ReindexPlaylistTrackOrder(connection, playlistId);
    }

    public static List<Track> GetTracksForPlaylist(int playlistId, TrackSortType? sortType = null)
    {
        var tracks = new List<Track>();
        var effectiveSortType = sortType ?? TrackSortType.AddedDate;
        System.Diagnostics.Debug.WriteLine($"GetTracksForPlaylist for ID={playlistId} ");

        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId = $pId";
            checkCmd.Parameters.AddWithValue("$pId", playlistId);
            long? linksCount = (long?)checkCmd.ExecuteScalar();
            System.Diagnostics.Debug.WriteLine($"Links in PlaylistTracks: {linksCount}");


            if (linksCount == 0)
            {
                System.Diagnostics.Debug.WriteLine("No links for this playlist!");
                return tracks;
            }

            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT t.Id, t.Path, t.Name, t.Executor, t.Album, t.Duration, t.Genre, t.Bitrate, t.SampleRate, t.Year, t.TrackNumber, pt.AddedDate, COALESCE(t.PlayCount, 0) as PlayCount
                FROM Tracks t
                INNER JOIN PlaylistTracks pt ON t.Id = pt.TrackId
                WHERE pt.PlaylistId = $pId
                ORDER BY pt.OrderIndex ASC, pt.AddedDate ASC, t.Id ASC";

                command.Parameters.AddWithValue("$pId", playlistId);

                using var reader = command.ExecuteReader();
                int count = 0;
                while (reader.Read())
                {
                    var track = new Track
                    {
                        Id = reader.GetInt32(0),
                        Path = reader.GetString(1),
                        Name = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                        Executor = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3),
                        Album = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Duration = reader.IsDBNull(5) ? "00:00" : reader.GetString(5),
                        Genre = reader.IsDBNull(6) ? "Unknown" : reader.GetString(6),
                        Bitrate = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                        SampleRate = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                        Year = reader.IsDBNull(9) ? 0 : reader.GetInt16(9),
                        TrackNumber = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                        AddedDate = reader.IsDBNull(11) ? DateTime.Now : DateTime.Parse(reader.GetString(11)),
                        PlayCount = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                    };

                    System.Diagnostics.Debug.WriteLine($"Track {++count}: ID={track.Id}, Name={track.Name}, PlayCount={track.PlayCount}, Executor={track.Executor}, TrackNumber={track.TrackNumber}, AddedDate={track.AddedDate:dd.MM.yyyy HH:mm}");
                    tracks.Add(track);
                }
                System.Diagnostics.Debug.WriteLine($"Total tracks loaded: {tracks.Count}");
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such column: t.TrackNumber"))
            {
                System.Diagnostics.Debug.WriteLine($"TrackNumber column not found, performing migration: {ex.Message}");

                MigrateDatabase();

                var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT t.Id, t.Path, t.Name, t.Executor, t.Album, t.Duration, t.Genre, t.Bitrate, t.SampleRate, t.Year, t.TrackNumber, pt.AddedDate, COALESCE(t.PlayCount, 0) as PlayCount
                FROM Tracks t
                INNER JOIN PlaylistTracks pt ON t.Id = pt.TrackId
                WHERE pt.PlaylistId = $pId
                ORDER BY pt.OrderIndex ASC, pt.AddedDate ASC, t.Id ASC";

                command.Parameters.AddWithValue("$pId", playlistId);

                using var reader = command.ExecuteReader();
                int count = 0;
                while (reader.Read())
                {
                    var track = new Track
                    {
                        Id = reader.GetInt32(0),
                        Path = reader.GetString(1),
                        Name = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                        Executor = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3),
                        Album = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Duration = reader.IsDBNull(5) ? "00:00" : reader.GetString(5),
                        Genre = reader.IsDBNull(6) ? "Unknown" : reader.GetString(6),
                        Bitrate = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                        SampleRate = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                        Year = reader.IsDBNull(9) ? 0 : reader.GetInt16(9),
                        TrackNumber = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                        AddedDate = reader.IsDBNull(11) ? DateTime.Now : DateTime.Parse(reader.GetString(11)),
                        PlayCount = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                    };

                    System.Diagnostics.Debug.WriteLine($"Track {++count}: ID={track.Id}, Name={track.Name}, PlayCount={track.PlayCount}, Executor={track.Executor}, TrackNumber={track.TrackNumber}, AddedDate={track.AddedDate:dd.MM.yyyy HH:mm}");
                    tracks.Add(track);
                }
                System.Diagnostics.Debug.WriteLine($"Total tracks loaded: {tracks.Count}");
            }
        }
        return ApplyTrackSort(tracks, effectiveSortType);
    }

    private static List<Track> ApplyTrackSort(List<Track> tracks, TrackSortType sortType)
    {
        return sortType switch
        {
            TrackSortType.AlbumAZ => [.. tracks.OrderBy(t => t.Album ?? "").ThenBy(t => t.TrackNumber)],
            TrackSortType.ExecutorAZ => [.. tracks.OrderBy(t => t.Executor ?? "").ThenBy(t => t.Album ?? "").ThenBy(t => t.TrackNumber)],
            TrackSortType.NameAZ => [.. tracks.OrderBy(t => t.Name ?? "")],
            TrackSortType.CustomOrder => tracks,
            _ => [.. tracks.OrderBy(t => t.AddedDate)]
        };
    }

    private static int GetNextPlaylistTrackOrder(SqliteConnection connection, int playlistId)
    {
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COALESCE(MAX(OrderIndex), -1) + 1 FROM PlaylistTracks WHERE PlaylistId = $pId";
        countCommand.Parameters.AddWithValue("$pId", playlistId);
        var result = countCommand.ExecuteScalar();
        return result is null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    private static void ReindexPlaylistTrackOrder(SqliteConnection connection, int? playlistId = null)
    {
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = playlistId.HasValue
                ? @"SELECT TrackId FROM PlaylistTracks WHERE PlaylistId = $pId ORDER BY OrderIndex ASC, AddedDate ASC, TrackId ASC"
                : @"SELECT TrackId FROM PlaylistTracks ORDER BY PlaylistId ASC, OrderIndex ASC, AddedDate ASC, TrackId ASC";
            command.Parameters.AddWithValue("$pId", playlistId ?? 0);

            var trackIds = new List<int>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                trackIds.Add(reader.GetInt32(0));
            }

            if (trackIds.Count == 0)
            {
                return;
            }

            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "UPDATE PlaylistTracks SET OrderIndex = $order WHERE PlaylistId = $pId AND TrackId = $tId";
            var orderParam = updateCommand.Parameters.Add("$order", SqliteType.Integer);
            var playlistParam = updateCommand.Parameters.Add("$pId", SqliteType.Integer);
            var trackParam = updateCommand.Parameters.Add("$tId", SqliteType.Integer);

            for (int i = 0; i < trackIds.Count; i++)
            {
                orderParam.Value = i;
                playlistParam.Value = playlistId ?? 0;
                trackParam.Value = trackIds[i];
                updateCommand.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ReindexPlaylistTrackOrder: {ex.Message}");
        }
    }

    public static void SavePlaylistTrackOrder(int playlistId, IEnumerable<Track> tracks)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "UPDATE PlaylistTracks SET OrderIndex = $order WHERE PlaylistId = $pId AND TrackId = $tId";
            var orderParam = updateCommand.Parameters.Add("$order", SqliteType.Integer);
            var playlistParam = updateCommand.Parameters.Add("$pId", SqliteType.Integer);
            var trackParam = updateCommand.Parameters.Add("$tId", SqliteType.Integer);

            int index = 0;
            foreach (var track in tracks)
            {
                orderParam.Value = index++;
                playlistParam.Value = playlistId;
                trackParam.Value = track.Id;
                updateCommand.ExecuteNonQuery();
            }

            ReindexPlaylistTrackOrder(connection, playlistId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SavePlaylistTrackOrder: {ex.Message}");
        }
    }

    public static long CreatePlaylist(string name, string? description = "", byte[]? coverImage = null, bool isSystemPlaylist = false)
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
                playlists[i].SortOrder = i;

                orderParam.Value = i;
                idParam.Value = playlists[i].Id;
                command.ExecuteNonQuery();
            }
            transaction.Commit();
            System.Diagnostics.Debug.WriteLine("Playlists order saved successfully.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SavePlaylistsOrder: {ex.Message}");
        }
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
            string status = isPinned ? "pinned" : "unpinned";
            System.Diagnostics.Debug.WriteLine($"Playlist {playlistId} {status}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating playlist pin state: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Playlist {playlistId}: sort type changed to {sortType}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating playlist sort type: {ex.Message}");
        }
    }

    public static void CreatePlaylistWithTop50Tracks()
    {
        long newPlaylistId = CreatePlaylist("Top50Tracks", "Top 50 tracks by number of listens", null, true);
        var newPlaylist = GetPlaylistById((int)newPlaylistId);

        var topTracks = GetMostListenedTracksList(50);
        var dateAdded = newPlaylist?.CreatedDate ?? DateTime.Now;

        foreach (var track in topTracks)
        {
            track.AddedDate = dateAdded;
            SaveTrackToPlaylist((int)newPlaylistId, track);
        }
        if (newPlaylist != null)
        {
            Windows.Statistics.RefreshRightPanel(newPlaylist);
        }
        System.Diagnostics.Debug.WriteLine($"Created 'Top 50 Tracks' with ID={newPlaylistId} and {topTracks.Count} tracks added.");
    }

    public static void CreatePlaylistTracksWithoutListening()
    {
        long newPlaylistId = CreatePlaylist("TracksWithoutListening", "Tracks that haven't been listened", null, true);
        var newPlaylist = GetPlaylistById((int)newPlaylistId);

        var tracks = GetTracksWithoutListeningButton();
        var dateAdded = newPlaylist?.CreatedDate ?? DateTime.Now;

        foreach (var track in tracks)
        {
            track.AddedDate = dateAdded;
            SaveTrackToPlaylist((int)newPlaylistId, track);
        }
        if (newPlaylist != null)
        {
            Windows.Statistics.RefreshRightPanel(newPlaylist);
        }
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

                        playlists.Add(playlist);
                    }
                }
                catch (SqliteException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetPlaylistsAsync: {ex.Message}");
                }
            }
            System.Diagnostics.Debug.WriteLine($"Loaded playlists: {playlists.Count}");
            return playlists;
        });
    }

    /// <summary>
    /// Асинхронная загрузка треков для плейлиста
    /// </summary>
    public static async Task<List<Track>> GetTracksForPlaylistAsync(int playlistId, TrackSortType? sortType = null)
    {
        return await Task.Run(() => GetTracksForPlaylist(playlistId, sortType));
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
}
