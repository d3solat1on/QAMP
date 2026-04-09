using System.ComponentModel;
using Newtonsoft.Json;
using TagLib; // для INotifyPropertyChanged

namespace QAMP.Models
{
    public class Track : INotifyPropertyChanged
    {
        public DateTime AddedDate { get; set; }
        public int Id { get; set; }
        public string? Extension { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Executor { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Duration { get; set; } = "0:00";
        public string Genre { get; set; } = "Неизвестно";
        public int Bitrate { get; set; }
        public int SampleRate { get; set; }
        public int Year { get; set; }
        private int _playCount;
        public int PlayCount
        {
            get => _playCount;
            set
            {
                if (_playCount != value)
                {
                    _playCount = value;
                    OnPropertyChanged(nameof(PlayCount));
                }
            }
        }
        public string? Comment { get; set; }
        private string? _lyrics;
        public string Lyrics
        {
            get => _lyrics ?? string.Empty;
            set
            {
                _lyrics = value;
                OnPropertyChanged(nameof(Lyrics));
            }
        }
        public string? Channels { get; set; }
        public string? Description { get; set; }
        public int BitsPerSample { get; set; }
        public int TrackNumber { get; set; }
        public string? Composer { get; set; }
        public string? AlbumArtist { get; set; }
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
        public string DisplayExtension
        {
            get
            {
                if (!string.IsNullOrEmpty(Extension)) return Extension.Replace(".", "").ToUpper();
                if (!string.IsNullOrEmpty(Path)) return System.IO.Path.GetExtension(Path).Replace(".", "").ToUpper();
                return "N/A";
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        
        // ✅ Публичный метод для уведомления об изменении свойства (используется из других классов)
        public void NotifyPropertyChanged(string name) => OnPropertyChanged(name);
    }
}