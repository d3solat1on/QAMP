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
        DateTime _nextTime = DateTime.MinValue;
        public float[] EqGains { get; set; } = new float[10];
        public EqualizerFilter CurrentEqualizer { get; private set; } = null!;
        private bool _disposed = false;
        private bool _playCountIncremented = false;
        private SpectrumAnalyzer _spectrumAnalyzer = null!;
        public SpectrumControl SpectrumControl { get; set; } = null!;

        // НОВОЕ: Настройки спектра
        private SpectrumSettings _spectrumSettings = null!;

        // NAudio компоненты
        private WaveStream? _audioFileReader;
        private WaveOutEvent? _waveOutEvent;
        private readonly DispatcherTimer _positionTimer = new();
        private FadeInOutProvider _fadeProvider = null!;

        // События
        public event Action<Track>? TrackChanged;
        public event Action<double>? PositionChanged;
        public event Action<bool>? PlaybackPaused;
        public event Action<double>? VolumeChanged;
        public event Action? DurationChanged;
        public event Action<int>? PlayCountUpdated;

        // Свойства
        public Track CurrentTrack { get; set; } = null!;
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
        public event Action<RepeatMode>? RepeatModeChanged;

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
        public event Action<bool>? ShuffleChanged;

        private string? _tempFilePath;

        private PlayerService()
        {
            _positionTimer.Interval = TimeSpan.FromMilliseconds(100);
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

            InitializeSpectrumSettings();
        }

        private void InitializeSpectrumSettings()
        {
            _spectrumSettings = new SpectrumSettings
            {
                FreqPower = 1.2,      // частота
                AmplitudeGain = 25.0, // Усиление 
                AmplitudePower = 0.7, //  сжатие
                AutoNormalize = false, //  автонормализация
                MinBarValue = 0.00,
                MaxBarValue = 0.95
            };
        }
        public void SetSpectrumPreset(string presetName)
        {
            _spectrumSettings.ApplyPreset(presetName);
            _spectrumAnalyzer?.SetPreset(presetName);
            // SpectrumControl?.SetSpectrumPreset(presetName);
            App.LogInfo($"Spectrum preset changed to: {presetName}");
        }
        public void UpdateSpectrumSettings(double freqPower, double amplitudeGain, double amplitudePower,
                                           double attackSpeed, double releaseSpeed)
        {
            _spectrumSettings.FreqPower = freqPower;
            _spectrumSettings.AmplitudeGain = amplitudeGain;
            _spectrumSettings.AmplitudePower = amplitudePower;
            _spectrumSettings.AttackSpeed = attackSpeed;
            _spectrumSettings.ReleaseSpeed = releaseSpeed;

            var config = SettingsManager.Instance.Config;
            config.SpectrumFreqPower = freqPower;
            config.SpectrumAmplitudeGain = amplitudeGain;
            config.SpectrumAmplitudePower = amplitudePower;
            config.SpectrumAttackSpeed = attackSpeed;
            config.SpectrumReleaseSpeed = releaseSpeed;
            SettingsManager.Instance.Save();
        }

        public async Task PlayTrack(Track track)
        {
            MusicLibrary.Instance.PlaybackQueue = new ObservableCollection<Track>(MusicLibrary.Instance.PlaybackQueue);
            try
            {
                Stop();
                CurrentTrack = track;
                _playCountIncremented = false;
                string extension = Path.GetExtension(track.Path).ToLowerInvariant();
                ISampleProvider sampleProvider;

                await Task.Run(() =>
                {
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

                    CurrentEqualizer = new EqualizerFilter(sampleProvider, frequencies);
                    ApplySavedEqualizerSettings();

                    var aggregator = new SampleAggregator(CurrentEqualizer, 256);

                    _spectrumAnalyzer = new SpectrumAnalyzer(_spectrumSettings);
                    _spectrumAnalyzer.SpectrumUpdated += (s, result) =>
                    {

                        SpectrumControl?.UpdateSpectrum(result.Data);
                    };

                    aggregator.FftCalculated += (s, fftData) =>
                    {
                        if (fftData != null && fftData.Length > 0)
                        {
                            float maxVal = 0;
                            for (int i = 0; i < fftData.Length; i++)
                            {
                                if (fftData[i] > maxVal) maxVal = fftData[i];
                            }

                            if (DateTime.Now >= _nextTime)
                            {
                                System.Diagnostics.Debug.WriteLine($"FFT Max Value: {maxVal:F6}");
                                _nextTime = DateTime.Now.AddSeconds(2);
                            }

                            _spectrumAnalyzer?.ProcessSamples(fftData, fftData.Length);
                        }
                    };

                    _fadeProvider = new FadeInOutProvider(aggregator);
                });

                _waveOutEvent = new WaveOutEvent { DesiredLatency = 500 };
                _waveOutEvent.Init(_fadeProvider);
                _waveOutEvent.Volume = (float)Volume;
                _waveOutEvent.Play();
                _fadeProvider.BeginFadeIn(500);

                Duration = _audioFileReader!.TotalTime.TotalSeconds;
                IsPlaying = true;
                _positionTimer.Start();

                _waveOutEvent.PlaybackStopped -= OnPlaybackStopped;
                _waveOutEvent.PlaybackStopped += OnPlaybackStopped;
                TrackChanged?.Invoke(track);
                App.LogInfo($"Start track: {track.Name}");
            }
            catch (Exception ex)
            {
                _ = NotificationWindow.Show($"Ошибка: {ex.Message}", Application.Current.MainWindow);
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

                var aggregator = new SampleAggregator(CurrentEqualizer, 4096);

                aggregator.FftCalculated += (s, fftData) =>
                    {
                        if (fftData != null && fftData.Length > 0)
                        {
                            _spectrumAnalyzer?.ProcessSamples(fftData, fftData.Length);
                        }
                    };

                _waveOutEvent = new WaveOutEvent { DesiredLatency = 500 };  // ОПТИМИЗАЦИЯ: увеличено с 250 мс
                _waveOutEvent.Init(aggregator);
                _waveOutEvent.Volume = (float)Volume;
                // НЕ вызываем Play() - трек будет загружен, но на паузе

                Duration = _audioFileReader.TotalTime.TotalSeconds;
                IsPlaying = false;
                _positionTimer.Stop();

                // ИСПРАВЛЕНИЕ: отписываемся перед подпиской, чтобы избежать дублирования обработчиков
                _waveOutEvent.PlaybackStopped -= OnPlaybackStopped;
                _waveOutEvent.PlaybackStopped += OnPlaybackStopped;
                TrackChanged?.Invoke(track);
            }
            catch (Exception ex)
            {
                _ = NotificationWindow.Show($"Ошибка: {ex.Message}", Application.Current.MainWindow);
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
        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            _ = Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (e.Exception != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlaybackStopped] Error: {e.Exception.Message}");
                    App.LogException(e.Exception, "Playback Error");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PlaybackStopped] Stopped, but position timer should handle track switching");
                }
            });
        }
        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (_audioFileReader != null && IsPlaying)
            {
                try
                {
                    double newPosition = _audioFileReader.CurrentTime.TotalSeconds;
                    double totalDuration = _audioFileReader.TotalTime.TotalSeconds;

                    if (!double.IsNaN(newPosition) && !double.IsInfinity(newPosition))
                    {
                        Position = newPosition;
                        PositionChanged?.Invoke(Position);

                        // ОПРЕДЕЛЯЕМ КОНЕЦ ТРЕКА ЗДЕСЬ
                        // Если осталось меньше 0.3 секунды - считаем, что трек закончился
                        if (totalDuration > 0 && (totalDuration - newPosition) < 0.3)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PositionTimer] Track ending detected! Remaining: {totalDuration - newPosition:F2}s");

                            if (!_playCountIncremented && CurrentTrack != null)
                            {
                                _playCountIncremented = true;

                                // Увеличиваем счетчик прослушиваний
                                int trackId = CurrentTrack.Id;
                                System.Diagnostics.Debug.WriteLine($"[PlayCount] Incrementing for track: {CurrentTrack.Name}");

                                _ = Task.Run(() =>
                                {
                                    DatabaseService.IncrementTrackPlayCount(trackId);
                                    _ = Application.Current.Dispatcher.BeginInvoke(() =>
                                    {
                                        CurrentTrack.PlayCount++;
                                        System.Diagnostics.Debug.WriteLine($"[PlayCount] PlayCount now: {CurrentTrack.PlayCount}");

                                        // ✅ Уведомляем об обновлении PlayCount (например, для ShowTrackInfo)
                                        PlayCountUpdated?.Invoke(trackId);
                                    });
                                });
                            }

                            // Останавливаем таймер и переключаем трек
                            _positionTimer.Stop();
                            PlayNextTrack();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PositionTimer error: {ex.Message}");
                }
            }
        }

        public async Task PauseAsync()
        {
            if (_waveOutEvent != null && IsPlaying)
            {
                _fadeProvider.BeginFadeOut(300); // Быстрое затухание на 0.3 сек

                await Task.Delay(350);
                _waveOutEvent.Pause();
                IsPlaying = false;
                _positionTimer.Stop();
                PlaybackPaused?.Invoke(true);
            }
        }

        public void Resume()
        {
            // Проверяем, инициализирован ли фейдер и плеер
            if (_waveOutEvent != null && !IsPlaying && CurrentTrack != null)
            {
                // Добавляем ПРОВЕРКУ на null для провайдера
                if (_fadeProvider != null)
                {
                    _fadeProvider.ResetGain();
                    _fadeProvider.BeginFadeIn(300);
                }

                _waveOutEvent.Play();

                IsPlaying = true;
                _positionTimer.Start();
                PlaybackPaused?.Invoke(false);
            }
            else if (CurrentTrack != null)
            {
                // Если плеер пуст, но трек выбран — просто запускаем его нормально
                _ = PlayTrack(CurrentTrack);
            }
        }

        public void Stop()
        {
            if (_audioFileReader != null && CurrentTrack != null && !_playCountIncremented)
            {
                double currentTime = _audioFileReader.CurrentTime.TotalSeconds;
                double totalTime = _audioFileReader.TotalTime.TotalSeconds;

                if (totalTime > 0)
                {
                    double percentPlayed = currentTime / totalTime;

                    // Если прослушано больше 90% или осталось меньше 10 секунд
                    if (percentPlayed > 0.9 || (totalTime - currentTime) < 10)
                    {
                        _playCountIncremented = true;

                        int trackId = CurrentTrack.Id;
                        _ = Task.Run(() =>
                        {
                            DatabaseService.IncrementTrackPlayCount(trackId);
                            _ = Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                CurrentTrack.PlayCount++;
                            });
                        });
                    }
                }
            }
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
                try { File.Delete(_tempFilePath); } catch { }
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

            _ = PlayTrack(queue[prevIndex]);
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
                _ = PlayTrack(nextTrack);
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                    _positionTimer.Stop();
                    _positionTimer.Tick -= PositionTimer_Tick;
                }
                _disposed = true;
            }
        }
    }

    public enum RepeatMode
    {
        NoRepeat,
        RepeatAll,
        RepeatOne
    }

}