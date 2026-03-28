using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Newtonsoft.Json;

namespace QAMP.Models
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
        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set { _isPinned = value; OnPropertyChanged(nameof(IsPinned)); }
        }

        public int SortOrder { get; set; }
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
        public DateTime CreatedDate { get; set; } 
        public string CreatedDateDisplay => $"Дата создания: {CreatedDate:dd.MM.yyyy}";

        public string TrackCountDisplay => $"Треков: {Tracks.Count}";

        // Количество треков (для отображения)
        public int TrackCount => Tracks.Count;

        // Общая длительность
        public TimeSpan TotalDuration
        {
            get
            {
                TimeSpan total = TimeSpan.Zero;
                foreach (var track in Tracks)
                {
                    if (string.IsNullOrWhiteSpace(track.Duration)) continue;

                    if (TimeSpan.TryParseExact(track.Duration, @"m\:s", null, out TimeSpan duration) ||
                        TimeSpan.TryParseExact(track.Duration, @"mm\:ss", null, out duration))
                    {
                        total += duration;
                    }
                    else if (TimeSpan.TryParse(track.Duration, out duration))
                    {
                        total += duration;
                    }
                }
                return total;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Playlist()
        {
            Tracks = [];
            Tracks.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TrackCount));
                OnPropertyChanged(nameof(TrackCountDisplay));
            };
        }

    }
}