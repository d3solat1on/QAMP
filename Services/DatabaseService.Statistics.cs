using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using QAMP.Models;

namespace QAMP.Services;

public partial class DatabaseService
{
    public static string GetLastListenedTrack()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Tracks.Name, Executor
            FROM TrackPlayHistory
            INNER JOIN Tracks ON Tracks.Id = TrackPlayHistory.TrackId
            ORDER BY TrackPlayHistory.PlayedAtUtc DESC, TrackPlayHistory.Id DESC
            LIMIT 1";

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            string name = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
            string executor = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
            return $"{name}  by {executor}";
        }
        return "Unknown";
    }

    public static List<(string Genre, int TrackCount, double Percentage)> GetGenreStatistics()
    {
        var genreStatistics = new List<(string Genre, int TrackCount, double Percentage)>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TRIM(Genre), COUNT(*) AS TrackCount,
                   COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Tracks) AS Percentage
            FROM Tracks
            WHERE Genre IS NOT NULL AND TRIM(Genre) != ''
            GROUP BY TRIM(Genre)
            ORDER BY TrackCount DESC, TRIM(Genre) COLLATE NOCASE";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            genreStatistics.Add((
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetDouble(2)));
        }

        return genreStatistics;
    }

    public static string GetMostPopularGenre()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Genre, COUNT(*) as TrackCount
            FROM Tracks
            WHERE Genre IS NOT NULL AND Genre != ''
            GROUP BY Genre
            ORDER BY TrackCount DESC
            LIMIT 1";

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
        }
        return "Unknown";
    }

    public static string GetMostPopularAlbum()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Album, COUNT(*) as TrackCount
            FROM Tracks
            WHERE Album IS NOT NULL AND Album != ''
            GROUP BY Album
            ORDER BY TrackCount DESC
            LIMIT 1";

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
        }
        return "Unknown";
    }

    private static List<Track> GetMostListenedTracksList(int limit)
    {
        var tracks = new List<Track>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Path, Name, Executor, Album, Duration, Genre, Bitrate, SampleRate, Year, TrackNumber, PlayCount
            FROM Tracks
            WHERE PlayCount IS NOT NULL AND PlayCount > 0
            ORDER BY PlayCount DESC
            LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
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
                PlayCount = reader.IsDBNull(11) ? 0 : reader.GetInt32(11)
            };
            tracks.Add(track);
        }

        return tracks;
    }

    private static List<Track> GetTracksWithoutListeningButton()
    {
        var tracks = new List<Track>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Path, Name, Executor, Album, Duration, Genre, Bitrate, SampleRate, Year, TrackNumber, PlayCount
            FROM Tracks
            WHERE PlayCount IS NULL OR PlayCount = 0
            ORDER BY PlayCount DESC
            ";

        using var reader = cmd.ExecuteReader();
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
                PlayCount = reader.IsDBNull(11) ? 0 : reader.GetInt32(11)
            };
            tracks.Add(track);
        }

        return tracks;
    }

    public static string GetFirstTrackInLibrary()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT Name, Executor 
        FROM Tracks 
        ORDER BY Id ASC 
        LIMIT 1";

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            string name = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
            string executor = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
            return $"{name}  by {executor}";
        }

        return LocalizationService.GetString("LngNoTracksInLibrary");
    }

    public static string GetLastTrackInLibrary()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT Name, Executor 
        FROM Tracks 
        ORDER BY Id DESC 
        LIMIT 1";

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            string name = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
            string executor = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
            return $"{name}  by {executor}";
        }

        return LocalizationService.GetString("LngNoTracksInLibrary");
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
            return "No data.";
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
                    System.Diagnostics.Debug.WriteLine($"Error occurred while getting file size {path}: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Error occurred while calculating library weight: {ex.Message}");
            return "Error occurred while calculating library size.";
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
