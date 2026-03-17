using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using TagLib;
using NAudio.Flac;
using MusicPlayer_by_d3solat1on.Models;
using MusicPlayer_by_d3solat1on.ViewModels;
using MusicPlayer_by_d3solat1on.Dialogs;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MusicPlayer_by_d3solat1on.Services
{
    public class PlayerService : IDisposable
    {
        private static PlayerService? _instance;
        public static PlayerService Instance => _instance ??= new PlayerService();

        // NAudio компоненты
        private WaveStream _audioFileReader;
        private WaveOutEvent _waveOutEvent;
        private DispatcherTimer? _positionTimer;

        // События
        public event Action<Track> TrackChanged;
        public event Action<double> PositionChanged;
        public event Action<bool> PlaybackPaused;
        public event Action<double> VolumeChanged;
        public event Action DurationChanged;

        // Свойства
        public Track CurrentTrack { get; private set; }
        public bool IsPlaying { get; private set; }

        private double _position;
        public double Position
        {
            get => _position;
            private set
            {
                _position = value;
                PositionChanged?.Invoke(_position);
            }
        }

        private double _duration;
        public double Duration
        {
            get => _duration;
            private set
            {
                if (_duration != value)
                {
                    _duration = value;
                    DurationChanged?.Invoke();
                }
            }
        }

        private double _volume = 0.5;
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Max(0, Math.Min(1, value));
                if (_waveOutEvent != null)
                {
                    _waveOutEvent.Volume = (float)_volume;
                }
                VolumeChanged?.Invoke(_volume);
            }
        }

        // Режимы воспроизведения
        private RepeatMode _repeatMode = RepeatMode.NoRepeat;
        public RepeatMode RepeatMode
        {
            get => _repeatMode;
            set
            {
                _repeatMode = value;
                RepeatModeChanged?.Invoke(value);
            }
        }
        public event Action<RepeatMode> RepeatModeChanged;

        private bool _isShuffle = false;
        public bool IsShuffle
        {
            get => _isShuffle;
            set
            {
                _isShuffle = value;
                ShuffleChanged?.Invoke(value);
            }
        }
        public event Action<bool> ShuffleChanged;

        private readonly Random _random = new();

        private string? _tempFilePath;
        private PlayerService()
        {
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _positionTimer.Tick += PositionTimer_Tick;
        }

        public void PlayTrack(Track track)
        {
            try
            {
                Stop();
                CurrentTrack = track;

                string extension = Path.GetExtension(track.Path).ToLowerInvariant();

                if (extension == ".flac")
                {
                    // Используем тот самый рабочий ридер
                    _audioFileReader = new FlacReader(track.Path);
                }
                else
                {
                    // Для MP3 и прочего используем стандартный путь
                    _audioFileReader = new AudioFileReader(track.Path);
                }

                _waveOutEvent = new WaveOutEvent();
                _waveOutEvent.Init(_audioFileReader);
                _waveOutEvent.Volume = (float)Volume;
                _waveOutEvent.Play();

                Duration = _audioFileReader.TotalTime.TotalSeconds;
                IsPlaying = true;
                _positionTimer.Start();

                _waveOutEvent.PlaybackStopped += OnPlaybackStopped;
                TrackChanged?.Invoke(track);
            }
            catch (Exception ex)
            {
                NotificationWindow.Show($"Ошибка: {ex.Message}", Application.Current.MainWindow);
                Stop();
            }
        }


        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Если трек прервался раньше времени (например, на 3-й секунде), 
                // это ошибка буфера, а не конец песни.
                if (_audioFileReader != null && _audioFileReader.CurrentTime < _audioFileReader.TotalTime - TimeSpan.FromSeconds(1))
                {
                    // Игнорируем «ложную» остановку
                    System.Diagnostics.Debug.WriteLine("Ложная остановка проигнорирована.");
                    return;
                }

                if (e.Exception == null)
                {
                    PlayNextTrack();
                }
            });
        }
        private void PositionTimer_Tick(object sender, EventArgs e)
        {
            if (_audioFileReader != null && IsPlaying)
            {
                Position = _audioFileReader.CurrentTime.TotalSeconds;
                PositionChanged?.Invoke(Position);

                // ПРОВЕРКА: Переключаем, только если трек играет больше 5 секунд 
                // И до конца осталось меньше 0.5 сек.
                if (Position > 5 && Position >= _audioFileReader.TotalTime.TotalSeconds - 0.5)
                {
                    _positionTimer.Stop(); // Сначала стопим таймер, чтобы не вызвало дважды
                    PlayNextTrack();
                }
            }
        }

        public void Pause()
        {
            if (_waveOutEvent != null && IsPlaying)
            {
                _waveOutEvent.Pause();
                IsPlaying = false;
                _positionTimer.Stop();
                PlaybackPaused?.Invoke(true);
            }
        }

        public void Resume()
        {
            if (_waveOutEvent != null && !IsPlaying && CurrentTrack != null)
            {
                _waveOutEvent.Play();
                IsPlaying = true;
                _positionTimer.Start();
                PlaybackPaused?.Invoke(false);
            }
        }

        public void Stop()
        {
            _positionTimer?.Stop();

            if (_waveOutEvent != null)
            {
                _waveOutEvent.Stop();
                _waveOutEvent.Dispose();
                _waveOutEvent = null;
            }

            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose(); // Освобождает файл и память FLAC-ридера
                _audioFileReader = null;
            }

            // Удаляем временный файл, если он создавался ранее
            if (!string.IsNullOrEmpty(_tempFilePath) && System.IO.File.Exists(_tempFilePath))
            {
                try { System.IO.File.Delete(_tempFilePath); } catch { }
                _tempFilePath = null;
            }

            // Самый важный момент для очистки после тяжелых треков
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
        }
        public void Seek(double seconds)
        {
            if (_audioFileReader != null && CurrentTrack != null)
            {
                seconds = Math.Max(0, Math.Min(seconds, Duration));
                _audioFileReader.CurrentTime = TimeSpan.FromSeconds(seconds);
                Position = seconds;
            }
        }

        private void PlayNextTrack()
        {
            System.Diagnostics.Debug.WriteLine($"=== PlayNextTrack ===");

            var library = MusicLibrary.Instance;
            if (library == null)
            {
                System.Diagnostics.Debug.WriteLine("✗ library == null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"CurrentTracks.Count: {library.CurrentTracks.Count}");
            System.Diagnostics.Debug.WriteLine($"CurrentTrack: {CurrentTrack?.Name}");

            if (library.CurrentTracks.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("✗ Нет треков в текущем плейлисте");
                return;
            }

            var currentIndex = library.CurrentTracks.IndexOf(CurrentTrack);
            System.Diagnostics.Debug.WriteLine($"currentIndex: {currentIndex}");

            // Режим RepeatOne
            if (RepeatMode == RepeatMode.RepeatOne)
            {
                System.Diagnostics.Debug.WriteLine("Режим RepeatOne - повторяем текущий трек");
                PlayTrack(CurrentTrack);
                return;
            }

            // Режим Shuffle
            if (IsShuffle)
            {
                System.Diagnostics.Debug.WriteLine("Режим Shuffle");
                PlayNextShuffleTrack();
                return;
            }

            // Обычный режим
            if (currentIndex >= 0 && currentIndex < library.CurrentTracks.Count - 1)
            {
                var nextTrack = library.CurrentTracks[currentIndex + 1];
                System.Diagnostics.Debug.WriteLine($"Переключаем на следующий трек: {nextTrack.Name}");
                PlayTrack(nextTrack);
            }
            else if (RepeatMode == RepeatMode.RepeatAll && currentIndex == library.CurrentTracks.Count - 1)
            {
                System.Diagnostics.Debug.WriteLine("Режим RepeatAll - начинаем сначала");
                PlayTrack(library.CurrentTracks[0]);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Конец плейлиста, останавливаемся");
            }
        }
        private void PlayNextShuffleTrack()
        {
            var library = MusicLibrary.Instance;
            if (library == null || library.CurrentTracks.Count == 0) return;

            int nextIndex;
            do
            {
                nextIndex = _random.Next(library.CurrentTracks.Count);
            } while (library.CurrentTracks.Count > 1 && nextIndex == library.CurrentTracks.IndexOf(CurrentTrack));

            PlayTrack(library.CurrentTracks[nextIndex]);
        }

        public void Dispose()
        {
            Stop();
            _positionTimer?.Stop();
            _positionTimer = null;
        }
        private static string? CreateOptimizedFlac(string originalPath)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".flac");
                System.IO.File.Copy(originalPath, tempFile, true);

                using (var file = TagLib.File.Create(tempFile))
                {
                    var pic = file.Tag.Pictures.FirstOrDefault();

                    // Если обложка больше 2 МБ — сжимаем её
                    if (pic != null && pic.Data.Data.Length > 2 * 1024 * 1024)
                    {
                        byte[] smallData = ResizeImageBytes(pic.Data.Data, 600);

                        // Заменяем тяжелые байты на легкие
                        pic.Data = [.. smallData];
                        file.Tag.Pictures = [pic];
                        file.Save();
                        System.Diagnostics.Debug.WriteLine("✓ Обложка оптимизирована для стабильного чтения.");
                    }
                }
                return tempFile;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Ошибка оптимизации: {ex.Message}");
                return null;
            }
        }
        private static byte[] ResizeImageBytes(byte[] imageData, int maxWidth)
        {
            using var ms = new MemoryStream(imageData);
            using var original = Image.FromStream(ms);
            // Рассчитываем пропорции, чтобы картинка не сплющилась
            double ratio = (double)original.Width / original.Height;
            int newWidth = Math.Min(original.Width, maxWidth);
            int newHeight = (int)(newWidth / ratio);

            // Используем Bitmap вместо Image
            using var resized = new Bitmap(newWidth, newHeight);

            using var graphics = Graphics.FromImage(resized);
            // Настройки для высокого качества сжатия
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;

            graphics.DrawImage(original, 0, 0, newWidth, newHeight);

            using var outMs = new MemoryStream();
            // Сохраняем как JPEG (он весит меньше всего)
            resized.Save(outMs, ImageFormat.Jpeg);
            return outMs.ToArray();
        }
    }

    public enum RepeatMode
    {
        NoRepeat,
        RepeatAll,
        RepeatOne
    }

}