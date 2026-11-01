using System.IO;
using Microsoft.Data.Sqlite;

namespace QAMP.Services;

public partial class DatabaseService
{
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
            string logPath = AppDataManager.CrashLogPath;
            string message = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] [SaveSettingSync]\n{ex}\n";
            message += "----------------------------------------------------------------\n";
            try
            {
                File.AppendAllText(logPath, message);
            }
            catch
            {
                // I
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
            string logPath = AppDataManager.CrashLogPath;
            string message = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] [SaveSetting]\n{ex}\n";
            message += "----------------------------------------------------------------\n";
            try
            {
                File.AppendAllText(logPath, message);
            }
            catch
            {
                // I
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
}
