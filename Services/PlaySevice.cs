using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using NAudio.Flac;
using QAMP.Models;
using QAMP.ViewModels;
using QAMP.Dialogs;
using System.IO;
using System.Collections.ObjectModel;
using QAMP.Visualization;
using QAMP.Audio;

namespace QAMP.Services
{
    public class PlayerService : IDisposable
    {
        private static PlayerService? _instance;
        public static PlayerService Instance => _instance ??= new PlayerService();
       public float[] EqGains { get; set; } = new float[10]; 
        public EqualizerFilter CurrentEqualizer { get; private set; }
        private readonly SpectrumViewModel _spectrumViewModel;
        public SpectrumViewModel SpectrumViewModel => _spectrumViewModel;

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
        public Track CurrentTrack { get; set; }
        public bool IsPlaying { get; private set; }

        public bool IsShuffleEnabled { get; set; } = false;
        public List<Track> ShuffledQueue { get; set; } = [];
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
            _spectrumViewModel = new SpectrumViewModel();
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _positionTimer.Tick += PositionTimer_Tick;
        }

        public void PlayTrack(Track track)
        {
            MusicLibrary.Instance.PlaybackQueue = new ObservableCollection<Track>(MusicLibrary.Instance.PlaybackQueue);
            try
            {
                Stop();
                CurrentTrack = track;

                string extension = Path.GetExtension(track.Path).ToLowerInvariant();
                ISampleProvider sampleProvider;

                if (extension == ".flac")
                {
                    var flacReader = new FlacReader(track.Path);
                    _audioFileReader = flacReader;
                    sampleProvider = flacReader.ToSampleProvider();
                }
                else
                {
                    var reader = new AudioFileReader(track.Path);
                    _audioFileReader = reader;
                    sampleProvider = reader;
                }

                // --- ВСТАВКА ЭКВАЛАЙЗЕРА ---
                // Частоты для 10-полосного эквалайзера (стандарт)
                float[] frequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

                // Создаем фильтр. Он встает ПОСЛЕ ридера
                CurrentEqualizer = new EqualizerFilter(sampleProvider, frequencies);

                // Если у тебя есть сохраненные настройки Gain в памяти, 
                // здесь их можно применить циклом: CurrentEqualizer.SetGain(i, savedGain);
                // ---------------------------

                // --- ВСТАВКА ДЛЯ СПЕКТРА ---
                // ВАЖНО: передаем в агрегатор CurrentEqualizer, чтобы видеть эффект на спектре
                var aggregator = new SampleAggregator(CurrentEqualizer, 512);

                aggregator.FftCalculated += (s, fftData) =>
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            SpectrumViewModel?.Update(fftData);
                        }),
                        DispatcherPriority.Background);
                };
                // ---------------------------

                _waveOutEvent = new WaveOutEvent();

                // Инициализируем через aggregator
                _waveOutEvent.Init(aggregator);

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

            _audioFileReader?.Dispose(); // Освобождает файл и память FLAC-ридера
            _audioFileReader = null;

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

        public Track? GetNextTrack()
        {
            // Если включен Shuffle
            if (IsShuffleEnabled)
            {
                if (ShuffledQueue.Count > 0)
                {
                    int currentIndex = ShuffledQueue.IndexOf(CurrentTrack);
                    System.Diagnostics.Debug.WriteLine($"GetNextTrack (Shuffle): currentIndex = {currentIndex}, Count = {ShuffledQueue.Count}");

                    if (currentIndex != -1 && currentIndex < ShuffledQueue.Count - 1)
                    {
                        return ShuffledQueue[currentIndex + 1];
                    }
                    else if (currentIndex == ShuffledQueue.Count - 1 && RepeatMode == RepeatMode.RepeatAll)
                    {
                        return ShuffledQueue[0];
                    }
                }
                return null;
            }

            // Обычный режим
            var activeList = MusicLibrary.Instance.PlaybackQueue.ToList();
            if (activeList.Count == 0) return null;

            int index = activeList.IndexOf(CurrentTrack);

            if (index != -1 && index < activeList.Count - 1)
            {
                return activeList[index + 1];
            }
            else if (index == activeList.Count - 1 && RepeatMode == RepeatMode.RepeatAll)
            {
                return activeList[0];
            }

            return null;
        }

        public void PlayPreviousTrack()
        {
            var queue = MusicLibrary.Instance.PlaybackQueue.ToList();

            if (queue == null || queue.Count == 0) return;

            int currentIndex = queue.IndexOf(CurrentTrack);
            int prevIndex;

            if (currentIndex > 0)
            {
                prevIndex = currentIndex - 1;
            }
            else if (RepeatMode == RepeatMode.RepeatAll)
            {
                prevIndex = queue.Count - 1; // зацикливание
            }
            else
            {
                return; // нет предыдущего трека
            }

            PlayTrack(queue[prevIndex]);
            MainWindow.UpdateOSD();
        }
        public void PlayNextTrack()
        {
            if (CurrentTrack == null)
            {
                System.Diagnostics.Debug.WriteLine("PlayNextTrack: CurrentTrack == null");
                return;
            }

            Track? nextTrack = null;

            // Режим перемешивания
            if (IsShuffleEnabled)
            {
                System.Diagnostics.Debug.WriteLine($"PlayNextTrack: Shuffle mode, ShuffledQueue.Count = {ShuffledQueue.Count}");

                if (ShuffledQueue.Count > 0)
                {
                    // Находим индекс текущего трека в перемешанной очереди
                    int currentIndex = ShuffledQueue.IndexOf(CurrentTrack);
                    System.Diagnostics.Debug.WriteLine($"CurrentTrack index in ShuffledQueue: {currentIndex}");

                    if (currentIndex != -1 && currentIndex < ShuffledQueue.Count - 1)
                    {
                        nextTrack = ShuffledQueue[currentIndex + 1];
                        System.Diagnostics.Debug.WriteLine($"Next track: {nextTrack?.Name}");
                    }
                    else if (currentIndex == ShuffledQueue.Count - 1)
                    {
                        // Если мы в конце очереди
                        if (RepeatMode == RepeatMode.RepeatAll)
                        {
                            // Начинаем сначала
                            nextTrack = ShuffledQueue[0];
                            System.Diagnostics.Debug.WriteLine($"RepeatAll: starting from beginning: {nextTrack?.Name}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("End of shuffled queue, no repeat");
                        }
                    }
                    else if (currentIndex == -1)
                    {
                        // Если текущий трек не найден в ShuffledQueue (например, после ручного выбора)
                        // Создаем новую очередь, начиная с текущего трека
                        System.Diagnostics.Debug.WriteLine("Current track not in ShuffledQueue, recreating...");

                        var remainingTracks = MusicLibrary.Instance.PlaybackQueue
                            .Where(t => t != CurrentTrack)
                            .OrderBy(x => Guid.NewGuid())
                            .ToList();

                        ShuffledQueue = [CurrentTrack, .. remainingTracks];

                        if (ShuffledQueue.Count > 1)
                        {
                            nextTrack = ShuffledQueue[1];
                        }
                    }
                }
                else if (RepeatMode == RepeatMode.RepeatAll && MusicLibrary.Instance.PlaybackQueue.Count > 0)
                {
                    // Создаем новую перемешанную очередь
                    System.Diagnostics.Debug.WriteLine("ShuffledQueue empty, creating new...");
                    var shuffledList = MusicLibrary.Instance.PlaybackQueue
                        .Where(t => t != CurrentTrack)
                        .OrderBy(x => Guid.NewGuid())
                        .ToList();

                    ShuffledQueue = [CurrentTrack, .. shuffledList];

                    if (ShuffledQueue.Count > 1)
                    {
                        nextTrack = ShuffledQueue[1];
                    }
                }
            }
            // Режим повтора одного трека
            else if (RepeatMode == RepeatMode.RepeatOne)
            {
                nextTrack = CurrentTrack;
                System.Diagnostics.Debug.WriteLine("RepeatOne: playing same track");
            }
            // Обычный режим
            else
            {
                var currentIndex = MusicLibrary.Instance.PlaybackQueue.IndexOf(CurrentTrack);
                System.Diagnostics.Debug.WriteLine($"Normal mode, currentIndex: {currentIndex}, PlaybackQueue.Count: {MusicLibrary.Instance.PlaybackQueue.Count}");

                if (currentIndex >= 0 && currentIndex < MusicLibrary.Instance.PlaybackQueue.Count - 1)
                {
                    nextTrack = MusicLibrary.Instance.PlaybackQueue[currentIndex + 1];
                    System.Diagnostics.Debug.WriteLine($"Next track: {nextTrack?.Name}");
                }
                else if (RepeatMode == RepeatMode.RepeatAll && MusicLibrary.Instance.PlaybackQueue.Count > 0 &&
                         currentIndex == MusicLibrary.Instance.PlaybackQueue.Count - 1)
                {
                    nextTrack = MusicLibrary.Instance.PlaybackQueue[0];
                    System.Diagnostics.Debug.WriteLine($"RepeatAll: starting from beginning: {nextTrack?.Name}");
                }
            }

            if (nextTrack != null)
            {
                System.Diagnostics.Debug.WriteLine($"Playing next track: {nextTrack.Name}");
                PlayTrack(nextTrack);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No next track available");
            }
            MainWindow.UpdateOSD();
        }
        public void Dispose()
        {
            Stop();
            _positionTimer?.Stop();
            _positionTimer = null;
        }
    }

    public enum RepeatMode
    {
        NoRepeat,
        RepeatAll,
        RepeatOne
    }

}