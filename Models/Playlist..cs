using System.Collections.ObjectModel;
using System.ComponentModel;
using Newtonsoft.Json;

namespace QAMP.Models
{
    public enum TrackSortType
    {
        AddedDate = 0,          // По дате добавления (по умолчанию)
        AlbumAZ = 1,            // По альбому (A-Z)
        ExecutorAZ = 2,         // По исполнителю (A-Z)
        NameAZ = 3              // По названию (A-Z)
    }

    public class Playlist : INotifyPropertyChanged
    {
        public bool IsSystemPlaylist {get; set;} //True dlya Favorite
        private string? _name;
        private string? _description;

        [JsonIgnore]
        private byte[]? _coverImage;

        private ObservableCollection<Track> _tracks = [];
        public int Id { get; set; }

        public string? Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string? Description
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

        // Тип сортировки треков в плейлисте
        private TrackSortType _sortType = TrackSortType.AddedDate;
        public TrackSortType SortType
        {
            get => _sortType;
            set
            {
                _sortType = value;
                OnPropertyChanged(nameof(SortType));
            }
        }

        public byte[]? CoverImage
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
        private DateTime _createdDate;
        public DateTime CreatedDate
        {
            get => _createdDate;
            set
            {
                _createdDate = value;
                OnPropertyChanged(nameof(CreatedDate));
            }
        }
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
        public string TotalDurationDisplay => TotalDuration.TotalHours >= 1
    ? $"{(int)TotalDuration.TotalHours}:{TotalDuration.Minutes:D2}:{TotalDuration.Seconds:D2}"
    : TotalDuration.ToString(@"mm\:ss");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Playlist()
        {
            _createdDate = DateTime.Now;
            Tracks = [];
            Tracks.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TrackCount));
                OnPropertyChanged(nameof(TrackCountDisplay));
                OnPropertyChanged(nameof(TotalDurationDisplay));
                OnPropertyChanged(nameof(Tracks));
            };
        }

    }
}