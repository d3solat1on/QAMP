using Microsoft.Data.Sqlite;
using QAMP.Models;

namespace QAMP.Services;

public partial class DatabaseService
{
    public static int CreatePlaylistFolder(string title)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO PlaylistFolders (Title, IsExpanded)
            VALUES ($title, 1);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$title", title);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static List<PlaylistFolder> GetAllPlaylistFolders()
    {
        var folders = new List<PlaylistFolder>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, IsExpanded, IsPinned FROM PlaylistFolders ORDER BY Id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            folders.Add(new PlaylistFolder
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                IsExpanded = !reader.IsDBNull(2) && reader.GetInt32(2) != 0,
                IsPinned = !reader.IsDBNull(3) && reader.GetInt32(3) != 0
            });
        }

        return folders;
    }

    public static void RenamePlaylistFolder(int folderId, string title)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PlaylistFolders SET Title = $title WHERE Id = $id";
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$id", folderId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException($"Playlist folder {folderId} does not exist.");
        }
    }

    public static void DeletePlaylistFolder(int folderId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        using var updatePlaylists = connection.CreateCommand();
        updatePlaylists.Transaction = transaction;
        updatePlaylists.CommandText = "UPDATE Playlists SET FolderId = NULL WHERE FolderId = $id";
        updatePlaylists.Parameters.AddWithValue("$id", folderId);
        updatePlaylists.ExecuteNonQuery();

        using var deleteFolder = connection.CreateCommand();
        deleteFolder.Transaction = transaction;
        deleteFolder.CommandText = "DELETE FROM PlaylistFolders WHERE Id = $id";
        deleteFolder.Parameters.AddWithValue("$id", folderId);
        if (deleteFolder.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException($"Playlist folder {folderId} does not exist.");
        }

        transaction.Commit();
    }

    public static void SetPlaylistFolder(int playlistId, int? folderId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Playlists SET FolderId = $folderId WHERE Id = $playlistId";
        command.Parameters.AddWithValue("$folderId", folderId.HasValue ? (object)folderId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$playlistId", playlistId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException($"Playlist {playlistId} does not exist.");
        }
    }

    public static void SetPlaylistFolderExpanded(int folderId, bool isExpanded)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PlaylistFolders SET IsExpanded = $expanded WHERE Id = $id";
        command.Parameters.AddWithValue("$expanded", isExpanded ? 1 : 0);
        command.Parameters.AddWithValue("$id", folderId);
        command.ExecuteNonQuery();
    }

    public static void UpdatePlaylistFolderPinnedState(int folderId, bool isPinned)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PlaylistFolders SET IsPinned = $pinned WHERE Id = $id";
        command.Parameters.AddWithValue("$pinned", isPinned ? 1 : 0);
        command.Parameters.AddWithValue("$id", folderId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException($"Playlist folder {folderId} does not exist.");
        }
    }

    public static string GetFolderCount()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PlaylistFolders";

        // Выполняем запрос один раз и сохраняем результат
        var result = command.ExecuteScalar();

        if (result == null || result == DBNull.Value)
        {
            return "No data";
        }

        return Convert.ToString(result) ?? "0";
    }
}
