using System.IO;
using Microsoft.Data.Sqlite;

namespace QAMP.Services;

public partial class DatabaseService
{
    public static async Task SaveVolume(double volume)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();

            cmd.CommandText = "UPDATE Settings_V2 SET Volume = $Volume";
            cmd.Parameters.AddWithValue("$Volume", volume);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            App.LogException(ex, "SaveVolume");
        }
    }
    public static void SavePosition(double lastTrackPosition) 
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();

            cmd.CommandText = "UPDATE Settings_V2 SET LastTrackPosition = $LastTrackPosition";
            cmd.Parameters.AddWithValue("$LastTrackPosition", lastTrackPosition);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            App.LogException(ex, "SavePosition");
        }
    }
    public static async Task SaveLastTrackPath(string path)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();

            cmd.CommandText = "UPDATE Settings_V2 SET LastTrackPath = $LastTrackPath";
            cmd.Parameters.AddWithValue("$LastTrackPath", path);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            App.LogException(ex, "SaveLastTrackPath");
        }
    }
    public static string GetLastTrackPath()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT LastTrackPath FROM Settings_V2 LIMIT 1";

            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return string.Empty;

            return result.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            App.LogException(ex, "GetLastTrackPath");
            return string.Empty;
        }
    }
    public static int GetLastPlaylistId()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT LastPlaylistId FROM Settings_V2 LIMIT 1";

            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return -1;

            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            App.LogException(ex, "GetLastPlaylistId");
            return -1;
        }
    }
    public static async Task SaveLastPlaylistId(int playlistId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();

            cmd.CommandText = "UPDATE Settings_V2 SET LastPlaylistId = $LastPlaylistId";
            cmd.Parameters.AddWithValue("$LastPlaylistId", playlistId);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            App.LogException(ex, "SaveLastPlaylistId");
        }
    }
    public static double GetVolume()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Volume FROM Settings_V2 LIMIT 1";

            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return 0.5;

            return Convert.ToDouble(result);
        }
        catch (Exception ex)
        {
            App.LogException(ex, "GetVolume");
            return 0.5;
        }
    }
    public static double GetLastTrackPosition()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT LastTrackPosition FROM Settings_V2";
            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return 0.5;

            return Convert.ToDouble(result);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error GetLastTrackPosition: {ex.Message}");
            return 0;
        }
    }
}
