using System.Globalization;
using Microsoft.Data.Sqlite;
using QAMP.Models;

namespace QAMP.Services;

public partial class DatabaseService
{
    public static bool IncrementTrackPlayCount(int trackId)
    {
        System.Diagnostics.Debug.WriteLine($"[IncrementTrackPlayCount] BEFORE - Attempting to increment PlayCount for Track ID={trackId}");
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "UPDATE Tracks SET PlayCount = COALESCE(PlayCount, 0) + 1 WHERE Id = $id";
            updateCommand.Parameters.AddWithValue("$id", trackId);
            if (updateCommand.ExecuteNonQuery() == 0)
            {
                transaction.Rollback();
                return false;
            }

            using var historyCommand = connection.CreateCommand();
            historyCommand.Transaction = transaction;
            historyCommand.CommandText = "INSERT INTO TrackPlayHistory (TrackId, PlayedAtUtc) VALUES ($id, $playedAt)";
            historyCommand.Parameters.AddWithValue("$id", trackId);
            historyCommand.Parameters.AddWithValue("$playedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            historyCommand.ExecuteNonQuery();

            transaction.Commit();
            StatisticsChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IncrementTrackPlayCount] ERROR: {ex.Message}");
            App.LogException(ex, "IncrementPlayCount");
            return false;
        }
    }

    public static List<DateTimeOffset> GetTrackPlayHistory(int trackId)
    {
        var playHistory = new List<DateTimeOffset>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT PlayedAtUtc FROM TrackPlayHistory WHERE TrackId = $id ORDER BY PlayedAtUtc DESC";
        command.Parameters.AddWithValue("$id", trackId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (DateTimeOffset.TryParse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.None, out var playedAt))
            {
                playHistory.Add(playedAt);
            }
        }

        return playHistory;
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

    public static void DeleteTrackCompletely(int trackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM TrackPlayHistory WHERE TrackId = $trackId";
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.ExecuteNonQuery();

        cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM PlaylistTracks WHERE TrackId = $trackId";
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.ExecuteNonQuery();

        cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Tracks WHERE Id = $trackId";
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.ExecuteNonQuery();
    }
}
