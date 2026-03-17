using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Newtonsoft.Json;

namespace MusicPlayer_by_d3solat1on.Models
{
    public class Playlist : INotifyPropertyChanged
    {
        private string? _name;
        private string? _description;
        
        [JsonIgnore]
        private byte[]? _coverImage;

        private ObservableCollection<Track> _tracks;

        public int Id { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        public byte[] CoverImage
        {
            get => _coverImage;
            set
            {
                _coverImage = value;
                OnPropertyChanged(nameof(CoverImage));
            }
        }

        // Треки в плейлисте
        public ObservableCollection<Track> Tracks
        {
            get => _tracks;
            set
            {
                _tracks = value;
                _tracks.CollectionChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(TrackCount));
                    OnPropertyChanged(nameof(TrackCountDisplay));
                };
            }
        }
        // Дата создания
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string TrackCountDisplay => $"Треков: {Tracks.Count}";
        
        // Количество треков (для отображения)
        public int TrackCount => Tracks.Count;

        // Общая длительность
        public TimeSpan TotalDuration
        {
            get
            {
                long totalSeconds = 0;
                foreach (var track in Tracks)
                {
                    if (TimeSpan.TryParse(track.Duration, out TimeSpan duration))
                    {
                        totalSeconds += (long)duration.TotalSeconds;
                    }
                }
                return TimeSpan.FromSeconds(totalSeconds);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Playlist()
        {
            Tracks = new ObservableCollection<Track>();
            Tracks.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TrackCount));
                OnPropertyChanged(nameof(TrackCountDisplay));
            };
        }

    }
}