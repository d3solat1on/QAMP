using System.IO;
using System.Windows;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Flac;
using QAMP.Models;
using QAMP.ViewModels;
using QAMP.Dialogs;
using QAMP.Visualization;

namespace QAMP.Services
{
    public class PlayerService : IDisposable
    {
        private static PlayerService? _instance;
        public static PlayerService Instance => _instance ??= new PlayerService();

        // private int _streamHandle = 0;
        private int _currentStream = 0;
        private bool _isInitialized = false;

        // BASS параметры
        private readonly BASS_CHANNELINFO _channelInfo = new();
        private int _sampleRate = 44100;
        private SYNCPROC? _endSyncProc;

        public float[] EqGains { get; set; } = new float[10];
        private int _fxHandle = 0; // Handle for EQ effect

        private bool _disposed = false;
        private bool _playCountIncremented = false;

        private readonly SpectrumAnalyzer _spectrumAnalyzer = null!;
        public List<SpectrumControl> SpectrumControls { get; } = [];
        public SpectrumControl? SpectrumControl => SpectrumControls.FirstOrDefault();

        // Таймер для обновления позиции и спектра
        private readonly DispatcherTimer _positionTimer = new();
        private readonly DispatcherTimer _spectrumTimer = new();

        // Буфер для спектра
        private readonly float[] _fftBuffer = new float[1024];

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

        private double _lastNotifiedPosition = -1;
        private double _position;
        public double Position
        {
            get => _position;
            private set
            {
                _position = value;
                if (Math.Abs(_position - _lastNotifiedPosition) >= 0.1)
                {
                    _lastNotifiedPosition = _position;
                    PositionChanged?.Invoke(_position);
                }
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
        // Множитель громкости для быстрой регулировки (1.0 = без изменения)
        private double _masterGain = 3.0;
        public double MasterGain
        {
            get => _masterGain;
            set
            {
                _masterGain = Math.Max(0, value);
                if (_isInitialized && _currentStream != 0)
                {
                    float linearVolume = (float)(_volume * _masterGain);
                    Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);
                }
            }
        }
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Max(0, Math.Min(1, value));
                if (_isInitialized && _currentStream != 0)
                {
                    float linearVolume = (float)(_volume * _masterGain);
                    Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);
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
        public List<Track> _actualPlayingQueue = [];

        // Настройки спектра
        private SpectrumSettings _spectrumSettings = null!;

        private PlayerService()
        {
            InitializeBass();

            _positionTimer.Interval = TimeSpan.FromMilliseconds(100);
            _positionTimer.Tick += PositionTimer_Tick;

            _spectrumTimer.Interval = TimeSpan.FromMilliseconds(30);
            _spectrumTimer.Tick += SpectrumTimer_Tick;

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
            _endSyncProc = EndSyncCallback;
        }

        private void InitializeBass()
        {
            _isInitialized = true;
            App.LogInfo("BASS initialized successfully");
        }

        private void InitializeSpectrumSettings()
        {
            _spectrumSettings = new SpectrumSettings
            {
                FreqPower = 1.2,
                AmplitudeGain = 25.0,
                AmplitudePower = 0.7,
                AutoNormalize = false,
                MinBarValue = 0.00,
                MaxBarValue = 0.95
            };
        }

        public void AddSpectrumControl(SpectrumControl control)
        {
            if (control == null) return;
            if (!SpectrumControls.Contains(control))
            {
                SpectrumControls.Add(control);
            }
        }

        public void RemoveSpectrumControl(SpectrumControl control)
        {
            if (control == null) return;
            SpectrumControls.Remove(control);
        }

        public void RefreshSpectrumControls()
        {
            foreach (var control in SpectrumControls)
            {
                control.RefreshColors();
            }
        }

        public void SetSpectrumPreset(string presetName)
        {
            _spectrumSettings.ApplyPreset(presetName);
            _spectrumAnalyzer?.SetPreset(presetName);
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

        public async Task PlayTrack(Track track, bool isNewQueue = false)
        {
            if (isNewQueue || _actualPlayingQueue == null || _actualPlayingQueue.Count == 0)
            {
                if (MusicLibrary.Instance.CurrentPlaylist != null)
                {
                    _actualPlayingQueue = new List<Track>(MusicLibrary.Instance.PlaybackQueue);
                    System.Diagnostics.Debug.WriteLine($"[QUEUE] Очередь инициализирована: {_actualPlayingQueue.Count} треков");
                }
            }

            try
            {
                Stop();
                CurrentTrack = track;
                _playCountIncremented = false;

                await Task.Run(() =>
                {
                    // Создаем стрим для файла
                    int stream = CreateStreamFromFile(track.Path);

                    if (stream == 0)
                    {
                        int error = (int)Bass.BASS_ErrorGetCode();
                        throw new Exception($"Failed to create stream. BASS error: {error}");
                    }

                    _currentStream = stream;

                    // Получаем информацию о канале
                    Bass.BASS_ChannelGetInfo(_currentStream, _channelInfo);
                    _sampleRate = _channelInfo.freq;

                    // Устанавливаем громкость
                    float linearVolume = (float)(_volume * _masterGain);
                    Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);

                    // Применяем эквалайзер, если есть
                    ApplyEqualizerToStream();

                    // Получаем длительность
                    long length = Bass.BASS_ChannelGetLength(_currentStream, BASSMode.BASS_POS_BYTE);
                    _duration = Bass.BASS_ChannelBytes2Seconds(_currentStream, length);

                    // Устанавливаем синхронизацию для окончания трека
                    if (_endSyncProc != null)
                    {
                        Bass.BASS_ChannelSetSync(_currentStream, BASSSync.BASS_SYNC_END, 0, _endSyncProc, IntPtr.Zero);
                    }
                });

                // Запускаем воспроизведение
                if (!Bass.BASS_ChannelPlay(_currentStream, false))
                {
                    throw new Exception($"Failed to play stream. Error: {Bass.BASS_ErrorGetCode()}");
                }

                IsPlaying = true;
                _positionTimer.Start();
                _spectrumTimer.Start();

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

        private int CreateStreamFromFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            int stream = 0;

            if (extension == ".flac")
            {
                // Используем FLAC аддон для воспроизводимого потока
                stream = BassFlac.BASS_FLAC_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_DEFAULT | BASSFlag.BASS_SAMPLE_FLOAT);
                if (stream == 0)
                {
                    // Fallback на обычный BASS
                    stream = Bass.BASS_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_DEFAULT);
                }
            }
            else
            {
                // Для MP3, OGG, WAV и других форматов
                stream = Bass.BASS_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_DEFAULT);
            }

            return stream;
        }

        private void ApplyEqualizerToStream()
        {
            if (_currentStream == 0) return;

            // Удаляем старый эффект эквалайзера, если есть
            if (_fxHandle != 0)
            {
                Bass.BASS_ChannelRemoveFX(_currentStream, _fxHandle);
                _fxHandle = 0;
            }

            // Создаем параметры для 10-полосного эквалайзера
            var equalizer = new BASS_DX8_PARAMEQ[10];
            float[] frequencies = new float[] { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
            for (int i = 0; i < 10 && i < EqGains.Length; i++)
            {
                equalizer[i] = new BASS_DX8_PARAMEQ
                {
                    fGain = EqGains[i], // Gain in dB (-15 to 15)
                    fBandwidth = 18.0f, // Bandwidth (0-36, default 18)
                    fCenter = frequencies[i]
                };
            }

            // Применяем эффект для каждой полосы
            for (int i = 0; i < 10; i++)
            {
                int fx = Bass.BASS_ChannelSetFX(_currentStream, BASSFXType.BASS_FX_DX8_PARAMEQ, 1);
                if (fx != 0)
                {
                    Bass.BASS_FXSetParameters(fx, equalizer[i]);
                    if (i == 0) _fxHandle = fx;
                }
            }
        }

        public void UpdateEqualizerGains(float[] gains)
        {
            if (_currentStream == 0) return;

            for (int i = 0; i < gains.Length && i < EqGains.Length; i++)
            {
                EqGains[i] = gains[i];
            }

            // Обновляем эффект эквалайзера
            ApplyEqualizerToStream();

            // Сохраняем в конфиг
            var config = SettingsManager.Instance.Config;
            for (int i = 0; i < gains.Length; i++)
            {
                config.EqualizerGains[i] = gains[i];
            }
            SettingsManager.Instance.Save();
        }

        public void ApplyCurrentEqGains()
        {
            if (_currentStream == 0) return;
            ApplyEqualizerToStream();
        }

        private void EndSyncCallback(int handle, int channel, int data, IntPtr user)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (!_playCountIncremented && CurrentTrack != null)
                {
                    _playCountIncremented = true;
                    int trackId = CurrentTrack.Id;

                    Task.Run(() =>
                    {
                        DatabaseService.IncrementTrackPlayCount(trackId);
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            CurrentTrack.PlayCount++;
                            PlayCountUpdated?.Invoke(trackId);
                        });
                    });
                }

                PlayNextTrack();
            });
        }

        public void LoadTrack(Track track)
        {
            try
            {
                Stop();
                CurrentTrack = track;

                int stream = CreateStreamFromFile(track.Path);

                if (stream == 0)
                {
                    int error = (int)Bass.BASS_ErrorGetCode();
                    throw new Exception($"Failed to load stream. BASS error: {error}");
                }

                _currentStream = stream;

                Bass.BASS_ChannelGetInfo(_currentStream, _channelInfo);
                _sampleRate = _channelInfo.freq;

                float linearVolume = (float)(_volume * _masterGain);
                Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);

                ApplyEqualizerToStream();

                long length = Bass.BASS_ChannelGetLength(_currentStream, BASSMode.BASS_POS_BYTE);
                _duration = Bass.BASS_ChannelBytes2Seconds(_currentStream, length);

                if (_endSyncProc != null)
                {
                    Bass.BASS_ChannelSetSync(_currentStream, BASSSync.BASS_SYNC_END, 0, _endSyncProc, IntPtr.Zero);
                }

                IsPlaying = false;
                _positionTimer.Stop();
                _spectrumTimer.Stop();

                if ((_actualPlayingQueue == null || _actualPlayingQueue.Count == 0) && MusicLibrary.Instance.PlaybackQueue.Count > 0)
                {
                    _actualPlayingQueue = new List<Track>(MusicLibrary.Instance.PlaybackQueue);
                    System.Diagnostics.Debug.WriteLine($"[QUEUE] Восстановлена очередь из PlaybackQueue: {_actualPlayingQueue.Count} треков");
                }
                else if (_actualPlayingQueue == null || _actualPlayingQueue.Count == 0)
                {
                    _actualPlayingQueue = [track];
                    System.Diagnostics.Debug.WriteLine("[QUEUE] Установлен текущий трек как единственная запись очереди");
                }

                TrackChanged?.Invoke(track);
            }
            catch (Exception ex)
            {
                _ = NotificationWindow.Show($"Ошибка: {ex.Message}", Application.Current.MainWindow);
                System.Diagnostics.Debug.WriteLine($"Ошибка в LoadTrack: {ex.Message}");
                Stop();
            }
        }

        private void SpectrumTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentStream != 0 && IsPlaying)
            {
                // Получаем FFT данные для спектра
                if (Bass.BASS_ChannelGetData(_currentStream, _fftBuffer, (int)BASSData.BASS_DATA_FFT1024) > 0)
                {
                    // Конвертируем FFT данные в спектр
                    double[] spectrumData = new double[32]; // 32 bands for visualization
                    for (int i = 0; i < 32 && i < _fftBuffer.Length / 2; i++)
                    {
                        spectrumData[i] = _fftBuffer[i];
                    }

                    foreach (var control in SpectrumControls)
                    {
                        control.UpdateSpectrum(spectrumData);
                    }
                }
            }
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentStream != 0 && IsPlaying)
            {
                try
                {
                    long position = Bass.BASS_ChannelGetPosition(_currentStream, BASSMode.BASS_POS_BYTE);
                    double newPosition = Bass.BASS_ChannelBytes2Seconds(_currentStream, position);
                    double totalDuration = _duration;

                    if (!double.IsNaN(newPosition) && !double.IsInfinity(newPosition))
                    {
                        Position = newPosition;
                        PositionChanged?.Invoke(Position);

                        // Проверка на конец трека (BASS обычно сам отправляет синхронизацию)
                        if (totalDuration > 0 && (totalDuration - newPosition) < 0.3)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PositionTimer] Track ending detected! Remaining: {totalDuration - newPosition:F2}s");
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
            if (_currentStream != 0 && IsPlaying)
            {
                Bass.BASS_ChannelPause(_currentStream);
                IsPlaying = false;
                _positionTimer.Stop();
                _spectrumTimer.Stop();
                PlaybackPaused?.Invoke(true);
                await Task.CompletedTask;
            }
        }

        public void Resume()
        {
            if (_currentStream != 0 && !IsPlaying && CurrentTrack != null)
            {
                Bass.BASS_ChannelPlay(_currentStream, false);
                IsPlaying = true;
                _positionTimer.Start();
                _spectrumTimer.Start();
                PlaybackPaused?.Invoke(false);
            }
            else if (CurrentTrack != null)
            {
                _ = PlayTrack(CurrentTrack);
            }
        }

        public void Stop()
        {
            if (_currentStream != 0)
            {
                Bass.BASS_ChannelStop(_currentStream);
                Bass.BASS_StreamFree(_currentStream);
                _currentStream = 0;
            }

            _positionTimer?.Stop();
            _spectrumTimer?.Stop();

            IsPlaying = false;

            // Очищаем эффекты
            if (_fxHandle != 0)
            {
                _fxHandle = 0;
            }

            // Удаляем временный файл, если он создавался ранее
            if (!string.IsNullOrEmpty(_tempFilePath) && System.IO.File.Exists(_tempFilePath))
            {
                try { File.Delete(_tempFilePath); } catch { }
                _tempFilePath = null;
            }
        }

        public void Seek(double seconds)
        {
            if (_currentStream != 0 && CurrentTrack != null)
            {
                seconds = Math.Max(0, Math.Min(seconds, Duration));
                long position = Bass.BASS_ChannelSeconds2Bytes(_currentStream, seconds);
                Bass.BASS_ChannelSetPosition(_currentStream, position, BASSMode.BASS_POS_BYTE);
                Position = seconds;
            }
        }

        public void SeekRelative(double deltaSeconds)
        {
            if (_currentStream != 0)
            {
                long currentPos = Bass.BASS_ChannelGetPosition(_currentStream, BASSMode.BASS_POS_BYTE);
                double currentSeconds = Bass.BASS_ChannelBytes2Seconds(_currentStream, currentPos);
                Seek(currentSeconds + deltaSeconds);
            }
        }

        public Track? GetNextTrack()
        {
            var queue = IsShuffleEnabled ? ShuffledQueue : _actualPlayingQueue;

            if (queue == null || CurrentTrack == null) return null;

            int currentIndex = queue.FindIndex(t => t.Path == CurrentTrack.Path);

            if (currentIndex != -1 && currentIndex < queue.Count - 1)
            {
                return queue[currentIndex + 1];
            }

            if (RepeatMode == RepeatMode.RepeatAll && queue.Count > 0)
            {
                return queue[0];
            }

            return null;
        }

        public void PlayPreviousTrack()
        {
            var queue = IsShuffleEnabled ? ShuffledQueue : _actualPlayingQueue;
            if (queue == null || queue.Count == 0) return;

            int currentIndex = queue.IndexOf(CurrentTrack);
            int prevIndex;

            if (currentIndex > 0)
            {
                prevIndex = currentIndex - 1;
            }
            else if (RepeatMode == RepeatMode.RepeatAll)
            {
                prevIndex = queue.Count - 1;
            }
            else
            {
                return;
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

            var queue = IsShuffleEnabled ? ShuffledQueue : _actualPlayingQueue;

            if (queue == null || queue.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("PlayNextTrack: Queue is empty");
                return;
            }

            Track? nextTrack = null;

            if (RepeatMode == RepeatMode.RepeatOne)
            {
                nextTrack = CurrentTrack;
                System.Diagnostics.Debug.WriteLine("RepeatOne: playing same track");
            }
            else if (IsShuffleEnabled)
            {
                int currentIndex = ShuffledQueue.FindIndex(t => t.Path == CurrentTrack.Path);

                if (currentIndex != -1 && currentIndex < ShuffledQueue.Count - 1)
                {
                    nextTrack = ShuffledQueue[currentIndex + 1];
                }
                else if (RepeatMode == RepeatMode.RepeatAll && ShuffledQueue.Count > 0)
                {
                    nextTrack = ShuffledQueue[0];
                }
            }
            else
            {
                int currentIndex = queue.FindIndex(t => t.Path == CurrentTrack.Path);

                if (currentIndex != -1 && currentIndex < queue.Count - 1)
                {
                    nextTrack = queue[currentIndex + 1];
                }
                else if (RepeatMode == RepeatMode.RepeatAll && queue.Count > 0)
                {
                    nextTrack = queue[0];
                }
            }

            if (nextTrack != null)
            {
                System.Diagnostics.Debug.WriteLine($"Playing next: {nextTrack.Name}");
                _ = PlayTrack(nextTrack, false);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No next track available (End of playlist)");
            }

            MainWindow.UpdateOSD();
            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.UpdateLyricsView();
                mainWin.UpdateNextTrackUI();
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
                    _spectrumTimer.Stop();
                    _spectrumTimer.Tick -= SpectrumTimer_Tick;

                    if (_isInitialized)
                    {
                        Bass.BASS_Free();
                        _isInitialized = false;
                    }
                }
                _disposed = true;
            }
        }

        public void UpdateQueueOrder(List<Track> newOrder)
        {
            _actualPlayingQueue = newOrder;

            if (IsShuffleEnabled)
            {
                ShuffledQueue = new List<Track>(_actualPlayingQueue);
                var rnd = new Random();
                for (int i = ShuffledQueue.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    (ShuffledQueue[j], ShuffledQueue[i]) = (ShuffledQueue[i], ShuffledQueue[j]);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[QUEUE] Очередь обновлена: {_actualPlayingQueue.Count} треков");
        }
    }

    public enum RepeatMode
    {
        NoRepeat,
        RepeatAll,
        RepeatOne
    }
}

namespace QAMP.Services
{
    public sealed class SpectrumEventArgs(float[] data)
    {
        public float[] Data { get; } = data ?? [];
    }
}