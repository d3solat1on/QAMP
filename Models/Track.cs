using System;
using System.ComponentModel;
using System.Data;
using Newtonsoft.Json;
using TagLib; // для INotifyPropertyChanged

namespace QAMP.Models
{
    public class Track : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string? Extension { get; set; }
        public string? Path { get; set; }
        public string? Name { get; set; }
        public string? Executor { get; set; }
        public string? Album { get; set; }
        public string? Duration { get; set; }
        public string Genre { get; set; } = "Неизвестно";
        public int Bitrate { get; set; }
        public int SampleRate { get; set; }
        public int Year { get; set; }
        public string Comment { get; set; }
        public string Lyrics { get; set; }
        public string Channels { get; set; }
        public string Description { get; set; }
        public int BitsPerSample { get; set; }
        public int TrackNumber { get; set; }
        public string Composer { get; set; }
        public string AlbumArtist { get; set; }
        [JsonIgnore]
        private byte[]? _coverImage;

        [JsonIgnore]
        public byte[]? CoverImage
        {
            get
            {
                // Ленивая загрузка при первом обращении
                if (_coverImage == null && !string.IsNullOrEmpty(Path))
                {
                    _coverImage = LoadCoverFromFile();
                }
                return _coverImage;
            }
            set => _coverImage = value;
        }
        private byte[]? LoadCoverFromFile()
        {
            try
            {
                using var file = File.Create(Path);
                if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                {
                    return file.Tag.Pictures[0].Data.Data;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки обложки: {ex.Message}");
            }
            return null;
        }
        public void UnloadCover()
        {
            _coverImage = null;
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}