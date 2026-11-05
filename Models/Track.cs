using System.ComponentModel;
using System.IO;

namespace QAMP.Models
{
    public class Track : INotifyPropertyChanged
    {
        public DateTime AddedDate { get; set; }
        public string AddedDateDisplay => AddedDate.ToString("dd.MM.yyyy HH:mm:ss"); // do not delete
        public int Id { get; set; }
        public string? Extension { get; set; }
        public string Path { get; set; } = string.Empty;
        private string _name = "Unknown Title";
        public string Name
        {
            get => _name;
            set
            {
                string? interned = string.IsNullOrEmpty(value) ? value : string.Intern(value);
                if (_name != interned)
                {
                    _name = interned;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private string _executor = "Unknown Artist";
        public string Executor
        {
            get => _executor;
            set
            {
                string? interned = string.IsNullOrEmpty(value) ? value : string.Intern(value);
                if (_executor != interned)
                {
                    _executor = interned;
                    OnPropertyChanged(nameof(Executor));
                }
            }
        }

        private string _album = "Unknown Album";
        public string Album
        {
            get => _album;
            set
            {
                string? interned = string.IsNullOrEmpty(value) ? value : string.Intern(value);
                if (_album != interned)
                {
                    _album = interned;
                    OnPropertyChanged(nameof(Album));
                }
            }
        }

        private string _duration = "0:00";
        public string Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged(nameof(Duration));
                }
            }
        }

        private string _genre = "Unknown Genre";
        public string Genre
        {
            get => _genre;
            set
            {
                string? interned = string.IsNullOrEmpty(value) ? value : string.Intern(value);

                if (_genre != interned)
                {
                    _genre = interned;
                    OnPropertyChanged(nameof(Genre));
                }
            }
        }

        private int _bitrate;
        public int Bitrate
        {
            get => _bitrate;
            set
            {
                if (_bitrate != value)
                {
                    _bitrate = value;
                    OnPropertyChanged(nameof(Bitrate));
                }
            }
        }

        private int _sampleRate;
        public int SampleRate
        {
            get => _sampleRate;
            set
            {
                if (_sampleRate != value)
                {
                    _sampleRate = value;
                    OnPropertyChanged(nameof(SampleRate));
                }
            }
        }

        private int _year;
        public int Year
        {
            get => _year;
            set
            {
                if (_year != value)
                {
                    _year = value;
                    OnPropertyChanged(nameof(Year));
                }
            }
        }
        private double? _size; // do not delete
        public double Size
        {
            get
            {
                if (_size.HasValue) return _size.Value;

                try
                {
                    FileInfo fi = new(Path);
                    _size = fi.Exists ? fi.Length / (1024.0 * 1024.0) : 0;
                }
                catch
                {
                    _size = 0;
                }
                return _size.Value;
            }
        }
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

        private string? _comment;
        public string? Comment
        {
            get => _comment;
            set
            {
                if (_comment != value)
                {
                    _comment = value;
                    OnPropertyChanged(nameof(Comment));
                }
            }
        }

        private string? _lyrics;
        public string Lyrics
        {
            get => _lyrics ?? string.Empty;
            set
            {
                if (_lyrics != value)
                {
                    _lyrics = value;
                    OnPropertyChanged(nameof(Lyrics));
                }
            }
        }

        private string? _channels;
        public string? Channels
        {
            get => _channels;
            set
            {
                if (_channels != value)
                {
                    _channels = value;
                    OnPropertyChanged(nameof(Channels));
                }
            }
        }

        private string? _description;
        public string? Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        private int _bitsPerSample;
        public int BitsPerSample
        {
            get => _bitsPerSample;
            set
            {
                if (_bitsPerSample != value)
                {
                    _bitsPerSample = value;
                    OnPropertyChanged(nameof(BitsPerSample));
                }
            }
        }

        private int _trackNumber;
        public int TrackNumber
        {
            get => _trackNumber;
            set
            {
                if (_trackNumber != value)
                {
                    _trackNumber = value;
                    OnPropertyChanged(nameof(TrackNumber));
                }
            }
        }

        private string? _composer;
        public string? Composer
        {
            get => _composer;
            set
            {
                string? interned = string.IsNullOrEmpty(value) ? value : string.Intern(value);
                if (_composer != interned)
                {
                    _composer = interned;
                    OnPropertyChanged(nameof(Composer));
                }
            }
        }

        private string? _albumArtist;
        public string? AlbumArtist
        {
            get => _albumArtist;
            set
            {
                string? interned = string.IsNullOrEmpty(value) ? value : string.Intern(value);
                if (_albumArtist != interned)
                {
                    _albumArtist = interned;
                    OnPropertyChanged(nameof(AlbumArtist));
                }
            }
        }

        private int _BPM = 0;
        public int BPM
        {
            get => _BPM;
            set
            {
                if (_BPM != value)
                {
                    _BPM = value;
                    OnPropertyChanged(nameof(BPM));
                }
            }
        }

        private byte[]? _coverImage;
        public byte[]? CoverImage
        {
            get
            {
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
                using var file = TagLib.File.Create(Path);
                if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                {
                    return file.Tag.Pictures[0].Data.Data;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading cover: {ex.Message}");
                App.LogException(ex, "Error loading cover");
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

        public void NotifyPropertyChanged(string name) => OnPropertyChanged(name);
    }
}