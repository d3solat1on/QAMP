using System;
using System.ComponentModel;
using System.Data;
using Newtonsoft.Json;
using TagLib; // для INotifyPropertyChanged

namespace MusicPlayer_by_d3solat1on.Models
{
    public class Track : INotifyPropertyChanged
    {
        public string? Extension { get; set; }
        public string? Path { get; set; }
        public string? Name { get; set; }
        public string? Executor { get; set; }
        public string? Album { get; set; }
        public string? Duration { get; set; }
        public string Genre { get; set; } = "Неизвестно";
        public int Bitrate { get; set; }
        public int SampleRate { get; set; }
        
        [JsonIgnore]
        private byte[]? _coverImage;

        // Форматированные свойства для отображения
        public string ExtensionDisplay => !string.IsNullOrEmpty(Extension) ? Extension.ToUpper() : "Неизвестно";
        public string BitrateDisplay => Bitrate > 0 ? $"{Bitrate} kbps" : "Неизвестно";
        public string SampleRateDisplay => SampleRate > 0 ? $"{SampleRate / 1000} kHz" : "Неизвестно";
        public string DurationDisplay => Duration ?? "00:00";
        public string AlbumDisplay => !string.IsNullOrEmpty(Album) ? Album : "Неизвестно";


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