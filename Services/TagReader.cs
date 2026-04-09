using System.IO;
using QAMP.Models;
using File = System.IO.File;

namespace QAMP.Services
{
    public class TagReader
    {
        public static Track? ReadTrackFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLower() ?? "Неизвестно";
            try
            {
                using var file = TagLib.File.Create(filePath);

                return new Track
                {
                    Path = Path.GetFullPath(filePath),
                    Name = string.IsNullOrEmpty(file.Tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : file.Tag.Title,
                    Executor = file.Tag.FirstPerformer ?? "Неизвестный исполнитель",
                    Album = file.Tag.Album ?? "Неизвестный альбом",
                    Duration = file.Properties.Duration.ToString(@"mm\:ss"),
                    Bitrate = file.Properties.AudioBitrate,
                    SampleRate = file.Properties.AudioSampleRate,
                    Genre = file.Tag.FirstGenre ?? "Неизвестный жанр",
                    Extension = extension,
                    Year = (int)file.Tag.Year,
                    TrackNumber = (int)file.Tag.Track,
                    AddedDate = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения {filePath}: {ex.Message}");
                return new Track
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Path = filePath,
                    Extension = extension,
                    AddedDate = DateTime.Now
                };
            }
        }
        public static Track? GetFullTrackInfo(string filePath)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                var track = new Track
                {
                    Path = filePath,
                    Extension = Path.GetExtension(filePath).ToUpper(),
                    Name = file.Tag.Title ?? Path.GetFileNameWithoutExtension(filePath),
                    Executor = file.Tag.FirstPerformer ?? "Неизвестен",
                    Album = file.Tag.Album ?? "Нет альбома",
                    Year = (int)file.Tag.Year,
                    Genre = file.Tag.FirstGenre ?? "Неизвестно",

                    Bitrate = file.Properties.AudioBitrate,
                    SampleRate = file.Properties.AudioSampleRate,
                    Duration = file.Properties.Duration.ToString(@"mm\:ss"),
                    Channels = file.Properties.AudioChannels > 1 ? "Stereo" : "Mono",
                    Description = file.Properties.Description, //кодек
                    BitsPerSample = file.Properties.BitsPerSample,

                    Comment = file.Tag.Comment ?? "",
                    Lyrics = file.Tag.Lyrics ?? "Текст песни отсутствует",
                    TrackNumber = (int)file.Tag.Track,
                    Composer = file.Tag.FirstComposer ?? "Не указан",
                    AlbumArtist = file.Tag.FirstAlbumArtist ?? "",
                    AddedDate = DateTime.Now
                };

                if (file.Tag.Pictures.Length > 0)
                {
                    track.CoverImage = file.Tag.Pictures[0].Data.Data;
                }

                return track;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения тегов: {ex.Message}");
                return null;
            }
        }
        public static Track[] ReadTracksFromFiles(string[] filePaths)
        {
            var tracks = new List<Track>();

            foreach (string filePath in filePaths)
            {
                var track = ReadTrackFromFile(filePath);
                if (track != null)
                    tracks.Add(track);
            }

            return [.. tracks];
        }

        public static Track[] ReadTracksFromFolder(string folderPath)
        {
            // Список поддерживаемых расширений
            string[] extensions = { ".mp3", ".flac", ".wav", ".aac" };

            // Получаем все файлы (рекурсивно) и фильтруем их
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                                 .ToArray();

            return ReadTracksFromFiles(files);
        }
    }
}