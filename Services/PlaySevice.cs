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
using QAMP.Windows;

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
        private double _lastGoodPosition;
        private int _stuckCounter;
        private PlayerService()
        {
            _spectrumViewModel = new SpectrumViewModel();
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _positionTimer.Tick += PositionTimer_Tick;

            // Инициализируем массив EqGains
            EqGains = new float[10];

            // Загружаем сохраненные настройки
            var config = SettingsManager.Instance.Config;
            if (config.EqualizerGains != null)
            {
                for (int i = 0; i < config.EqualizerGains.Length && i < EqGains.Length; i++)
                {
                    EqGains[i] = (float)config.EqualizerGains[i];
                }
            }
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

                float[] frequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

                // Создаем новый эквалайзер
                CurrentEqualizer = new EqualizerFilter(sampleProvider, frequencies);

                // ВАЖНО: Применяем сохраненные настройки к новому эквалайзеру
                ApplySavedEqualizerSettings();

                var aggregator = new SampleAggregator(CurrentEqualizer, 256);

                aggregator.FftCalculated += (s, fftData) =>
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            SpectrumViewModel?.Update(fftData);
                        }),
                        DispatcherPriority.Background);
                };

                _waveOutEvent = new WaveOutEvent();
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
                System.Diagnostics.Debug.WriteLine($"Ошибка в PlayTrack: {ex.Message}");
                Stop();
            }
        }

        /// <summary>
        /// Загружает трек без воспроизведения (для восстановления состояния при запуске)
        /// </summary>
        public void LoadTrack(Track track)
        {
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

                float[] frequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

                // Создаем новый эквалайзер
                CurrentEqualizer = new EqualizerFilter(sampleProvider, frequencies);

                // ВАЖНО: Применяем сохраненные настройки к новому эквалайзеру
                ApplySavedEqualizerSettings();

                var aggregator = new SampleAggregator(CurrentEqualizer, 256);

                aggregator.FftCalculated += (s, fftData) =>
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            SpectrumViewModel?.Update(fftData);
                        }),
                        DispatcherPriority.Background);
                };

                _waveOutEvent = new WaveOutEvent();
                _waveOutEvent.Init(aggregator);
                _waveOutEvent.Volume = (float)Volume;
                // НЕ вызываем Play() - трек будет загружен, но на паузе

                Duration = _audioFileReader.TotalTime.TotalSeconds;
                IsPlaying = false;
                _positionTimer.Stop();

                _waveOutEvent.PlaybackStopped += OnPlaybackStopped;
                TrackChanged?.Invoke(track);
            }
            catch (Exception ex)
            {
                NotificationWindow.Show($"Ошибка: {ex.Message}", Application.Current.MainWindow);
                System.Diagnostics.Debug.WriteLine($"Ошибка в LoadTrack: {ex.Message}");
                Stop();
            }
        }

        private void ApplySavedEqualizerSettings()
        {
            if (CurrentEqualizer == null) return;

            var config = SettingsManager.Instance.Config;
            if (config.EqualizerGains == null) return;

            // Применяем сохраненные значения
            for (int i = 0; i < config.EqualizerGains.Length && i < EqGains.Length; i++)
            {
                float gain = (float)config.EqualizerGains[i];
                CurrentEqualizer.SetGain(i, gain);
                EqGains[i] = gain;
            }
        }
        public void UpdateEqualizerGains(float[] gains)
        {
            if (CurrentEqualizer == null) return;

            for (int i = 0; i < gains.Length && i < EqGains.Length; i++)
            {
                CurrentEqualizer.SetGain(i, gains[i]);
                EqGains[i] = gains[i];
            }

            // Сохраняем в конфиг
            var config = SettingsManager.Instance.Config;
            for (int i = 0; i < gains.Length; i++)
            {
                config.EqualizerGains[i] = gains[i];
            }
            SettingsManager.Instance.Save();
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
                try
                {
                    double newPosition = _audioFileReader.CurrentTime.TotalSeconds;

                    if (Math.Abs(newPosition - _lastGoodPosition) < 0.01)
                    {
                        _stuckCounter++;
                        if (_stuckCounter > 3)
                        {
                            newPosition = _lastGoodPosition + _positionTimer.Interval.TotalSeconds;
                            _stuckCounter = 0;
                        }
                    }
                    else
                    {
                        _stuckCounter = 0;
                        _lastGoodPosition = newPosition;
                    }

                    Position = newPosition;

                    if (Position >= _audioFileReader.TotalTime.TotalSeconds - 0.1)
                    {
                        _positionTimer.Stop();
                        PlayNextTrack();
                    }
                }
                catch { }
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
        public void SeekRelative(double deltaSeconds)
        {
            if (_audioFileReader != null)
            {
                double newPosition = _audioFileReader.CurrentTime.TotalSeconds + deltaSeconds;
                Seek(newPosition);
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
            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.UpdateLyricsView();
            }
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
            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.UpdateLyricsView();
            }
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