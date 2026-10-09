using System.IO;
using System.Windows;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Flac;
using Un4seen.Bass.AddOn.Fx;
using QAMP.Models;
using QAMP.ViewModels;
using QAMP.Dialogs;
using QAMP.Visualization;
using Un4seen.BassWasapi;

namespace QAMP.Services
{
    public record OutputDeviceInfo(int Id, string Name);

    public class PlayerService : IDisposable
    {
        private static PlayerService? _instance;
        public static PlayerService Instance => _instance ??= new PlayerService();
        private int _currentStream = 0;
        private int _crossfadeStream = 0;
        private Track? _crossfadeTargetTrack;
        private double _crossfadeTargetDuration;
        private double _crossfadeDuration;
        private double _crossfadeElapsed;
        private double _crossfadeLastPosition;
        private bool _crossfadeAttempted;
        private bool _isInitialized = false;

        // BASS параметры
        private readonly BASS_CHANNELINFO _channelInfo = new();
        private int _sampleRate = 44100;
        private readonly SYNCPROC? _endSyncProc;
        private readonly SemaphoreSlim _playSemaphore = new(1, 1);
        private int _endSyncHandle = 0;
        private bool _usesWasapiCurrentStream;
        public float[] EqGains { get; set; } = new float[10];
        private readonly Dictionary<int, StreamEffects> _streamEffects = [];
        private long _savedPositionBytes = 0;
        private bool _wasPlayingBeforeEdit = false;
        private bool _disposed = false;
        private bool _playCountIncremented = false;
        private double _listenedSeconds = 0;
        private double _lastCountPosition = -1;

        private sealed class StreamEffects
        {
            public int[] EqFxHandles { get; } = new int[10];
            public int ReverbFxHandle { get; set; }
            public int EchoFxHandle { get; set; }
            public int CompressorFxHandle { get; set; }
        }

        public List<SpectrumControl> SpectrumControls { get; } = [];

        // Таймер для обновления позиции и спектра
        private readonly DispatcherTimer _positionTimer = new();
        private readonly DispatcherTimer _spectrumTimer = new();

        // Buffer
        private readonly float[] _nativeSpectrumBuffer = new float[128];
        private readonly float[] _nativePeakBuffer = new float[128];
        private readonly double[] _scottPlotBuffer = new double[128];
        private readonly double[] _scottPlotPeakBuffer = new double[128];
        private readonly float[] _fftBuffer = new float[512]; // buffer for FFT data

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
        private readonly double _masterGain = 3.0;
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Max(0, Math.Min(1, value));
                if (_isInitialized && _currentStream != 0)
                {
                    float linearVolume = (float)(_volume * _masterGain);
                    if (_usesWasapiCurrentStream)
                    {
                        WasapiEngine.Instance.SetVolume(linearVolume);
                    }
                    else
                    {
                        Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);
                    }
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

        public List<Track> _actualPlayingQueue = [];

        private PlayerService()
        {
            InitializeBass();

            _positionTimer.Interval = TimeSpan.FromMilliseconds(100);
            _positionTimer.Tick += PositionTimer_Tick;

            _spectrumTimer.Interval = TimeSpan.FromMilliseconds(30);
            _spectrumTimer.Tick += SpectrumTimer_Tick;

            EqGains = new float[10];

            var config = SettingsManager.Instance.Config;
            if (config.EqualizerGains != null)
            {
                for (int i = 0; i < config.EqualizerGains.Length && i < EqGains.Length; i++)
                {
                    EqGains[i] = (float)config.EqualizerGains[i];
                }
            }
            _endSyncProc = EndSyncCallback;
            WasapiEngine.Instance.PlaybackEnded += WasapiPlaybackEnded;
        }

        private void InitializeBass()
        {
            _isInitialized = true;
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
                control.RefreshDisplayType();
                control.RefreshColors();
            }
        }

        public async Task PlayTrack(Track track, bool isNewQueue = false)
        {
            await _playSemaphore.WaitAsync();
            try
            {
                if (isNewQueue || _actualPlayingQueue == null || _actualPlayingQueue.Count == 0)
                {
                    if (MusicLibrary.Instance.CurrentPlaylist != null)
                    {
                        _actualPlayingQueue = [.. MusicLibrary.Instance.PlaybackQueue];
                        System.Diagnostics.Debug.WriteLine($"[QUEUE] Очередь... {_actualPlayingQueue.Count}");
                    }
                }

                StopInternal();
                CurrentTrack = track;
                _playCountIncremented = false;
                _listenedSeconds = 0;
                _lastCountPosition = -1;
                _crossfadeAttempted = false;
                _usesWasapiCurrentStream = SettingsManager.Instance.Config.UseWASAPI;

                int stream = 0;

                await Task.Run(() =>
                {
                    stream = CreateStreamFromFile(track.Path, _usesWasapiCurrentStream);
                    if (stream == 0)
                    {
                        int error = (int)Bass.BASS_ErrorGetCode();
                        throw new Exception($"Failed to create stream. BASS error: {error}");
                    }

                    Bass.BASS_ChannelGetInfo(stream, _channelInfo);
                    _sampleRate = _channelInfo.freq;

                    float linearVolume = (float)(_volume * _masterGain);
                    if (!_usesWasapiCurrentStream)
                    {
                        Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);
                    }

                    _currentStream = stream;
                    ApplyEqualizerToStream();

                    long length = Bass.BASS_ChannelGetLength(stream, BASSMode.BASS_POS_BYTE);
                    _duration = Bass.BASS_ChannelBytes2Seconds(stream, length);

                    if (!_usesWasapiCurrentStream)
                    {
                        int targetDeviceId = SettingsManager.Instance.Config.OutputDeviceId;
                        Bass.BASS_ChannelSetDevice(stream, targetDeviceId);
                    }

                    if (!_usesWasapiCurrentStream && _endSyncProc != null)
                    {
                        _endSyncHandle = Bass.BASS_ChannelSetSync(stream, BASSSync.BASS_SYNC_END, 0, _endSyncProc, IntPtr.Zero);
                    }
                });

                if (_currentStream != 0 && _currentStream != stream)
                {
                    RemoveEffectsFromStream(_currentStream);
                }

                _currentStream = stream;

                if (_usesWasapiCurrentStream)
                {
                    WasapiEngine.Instance.Start(
                        _currentStream,
                        _channelInfo,
                        SettingsManager.Instance.Config.OutputDeviceId,
                        (float)(_volume * _masterGain));
                }
                else if (!Bass.BASS_ChannelPlay(_currentStream, false))
                {
                    throw new Exception($"Failed to play stream. Error: {Bass.BASS_ErrorGetCode()}");
                }

                IsPlaying = true;
                _positionTimer.Start();
                _spectrumTimer.Start();

                TrackChanged?.Invoke(track);
            }
            catch (Exception ex)
            {
                _ = NotificationWindow.Show($"Ошибка: {ex.Message}", Application.Current.MainWindow);
                System.Diagnostics.Debug.WriteLine($"Ошибка в PlayTrack: {ex.Message}");
                App.LogException(ex, "PlayTrack");
                StopInternal();
            }
            finally
            {
                _playSemaphore.Release();
            }
        }

        public async Task ApplyOutputModeAsync()
        {
            if (CurrentTrack == null) return;

            Track track = CurrentTrack;
            double position = Position;
            bool wasPlaying = IsPlaying;

            await PlayTrack(track);
            if (_currentStream == 0 || CurrentTrack.Path != track.Path) return;

            Seek(position);
            if (!wasPlaying)
            {
                await PauseAsync();
            }
        }

        public void PrepareForTagEdit()
        {
            if (_currentStream == 0) return;

            _playSemaphore.Wait();
            try
            {
                _savedPositionBytes = Bass.BASS_ChannelGetPosition(_currentStream, BASSMode.BASS_POS_BYTE);

                _wasPlayingBeforeEdit = IsPlaying;

                _positionTimer.Stop();
                _spectrumTimer.Stop();

                StopInternal();
            }
            finally
            {
                _playSemaphore.Release();
            }
        }
        public void ResumeAfterTagEdit(Track track)
        {
            if (track == null) return;

            _playSemaphore.Wait();
            try
            {
                CurrentTrack = track;
                _lastCountPosition = -1;

                _usesWasapiCurrentStream = SettingsManager.Instance.Config.UseWASAPI;
                int stream = CreateStreamFromFile(track.Path, _usesWasapiCurrentStream);
                if (stream == 0) return;

                Bass.BASS_ChannelGetInfo(stream, _channelInfo);
                _sampleRate = _channelInfo.freq;

                float linearVolume = (float)(_volume * _masterGain);
                if (!_usesWasapiCurrentStream)
                {
                    Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);
                }

                _currentStream = stream;
                ApplyEqualizerToStream();

                long length = Bass.BASS_ChannelGetLength(stream, BASSMode.BASS_POS_BYTE);
                _duration = Bass.BASS_ChannelBytes2Seconds(stream, length);

                if (!_usesWasapiCurrentStream)
                {
                    int targetDeviceId = SettingsManager.Instance.Config.OutputDeviceId;
                    Bass.BASS_ChannelSetDevice(stream, targetDeviceId);
                }

                if (!_usesWasapiCurrentStream && _endSyncProc != null)
                {
                    _endSyncHandle = Bass.BASS_ChannelSetSync(stream, BASSSync.BASS_SYNC_END, 0, _endSyncProc, IntPtr.Zero);
                }

                _currentStream = stream;

                Bass.BASS_ChannelSetPosition(_currentStream, _savedPositionBytes, BASSMode.BASS_POS_BYTE);
                _lastCountPosition = Bass.BASS_ChannelBytes2Seconds(_currentStream, _savedPositionBytes);

                if (_wasPlayingBeforeEdit)
                {
                    if (_usesWasapiCurrentStream)
                    {
                        WasapiEngine.Instance.Start(
                            _currentStream,
                            _channelInfo,
                            SettingsManager.Instance.Config.OutputDeviceId,
                            (float)(_volume * _masterGain));
                    }
                    else
                    {
                        Bass.BASS_ChannelPlay(_currentStream, false);
                    }
                    IsPlaying = true;
                    _positionTimer.Start();
                    _spectrumTimer.Start();
                }
                else
                {
                    IsPlaying = false;
                }

                TrackChanged?.Invoke(track);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error occurred: {ex.Message}");
            }
            finally
            {
                _playSemaphore.Release();
            }
        }
        private static int CreateStreamFromFile(string filePath, bool decodeForWasapi = false)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            BASSFlag streamFlags = decodeForWasapi
                ? BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_PRESCAN
                : BASSFlag.BASS_DEFAULT;

            int stream = 0;
            if (extension == ".flac")
            {
                stream = BassFlac.BASS_FLAC_StreamCreateFile(filePath, 0, 0, streamFlags);
            }

            if (stream == 0)
            {
                stream = Bass.BASS_StreamCreateFile(filePath, 0, 0, streamFlags);
            }

            return stream;
        }

        private void RemoveEffectsFromStream(int stream)
        {
            if (stream == 0) return;

            if (!_streamEffects.Remove(stream, out StreamEffects? effects)) return;

            foreach (int fx in effects.EqFxHandles)
            {
                if (fx != 0)
                {
                    Bass.BASS_ChannelRemoveFX(stream, fx);
                }
            }

            if (effects.ReverbFxHandle != 0)
            {
                Bass.BASS_ChannelRemoveFX(stream, effects.ReverbFxHandle);
            }
            if (effects.EchoFxHandle != 0)
            {
                Bass.BASS_ChannelRemoveFX(stream, effects.EchoFxHandle);
            }
            if (effects.CompressorFxHandle != 0)
            {
                Bass.BASS_ChannelRemoveFX(stream, effects.CompressorFxHandle);
            }
        }

        private void ApplyEqualizerToStream()
        {
            if (_currentStream != 0) ApplyEqualizerToStream(_currentStream);
            if (_crossfadeStream != 0) ApplyEqualizerToStream(_crossfadeStream);
        }

        private void ApplyEqualizerToStream(int stream)
        {
            if (stream == 0) return;

            if (!_streamEffects.TryGetValue(stream, out StreamEffects? effects))
            {
                effects = new StreamEffects();
                _streamEffects.Add(stream, effects);
            }

            foreach (int fx in effects.EqFxHandles)
            {
                if (fx != 0) Bass.BASS_ChannelRemoveFX(stream, fx);
            }
            Array.Clear(effects.EqFxHandles);

            if (effects.ReverbFxHandle != 0)
            {
                Bass.BASS_ChannelRemoveFX(stream, effects.ReverbFxHandle);
                effects.ReverbFxHandle = 0;
            }
            if (effects.EchoFxHandle != 0)
            {
                Bass.BASS_ChannelRemoveFX(stream, effects.EchoFxHandle);
                effects.EchoFxHandle = 0;
            }
            if (effects.CompressorFxHandle != 0)
            {
                Bass.BASS_ChannelRemoveFX(stream, effects.CompressorFxHandle);
                effects.CompressorFxHandle = 0;
            }

            var config = SettingsManager.Instance.Config;
            Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_PAN, (float)config.Balance);

            float[] effectiveGains = new float[EqGains.Length];
            EqGains.CopyTo(effectiveGains, 0);

            if (config.VocalEnhancementEnabled)
            {
                for (int i = 4; i <= 6 && i < effectiveGains.Length; i++)
                {
                    effectiveGains[i] += 2.0f;
                }
            }

            if (config.LoudnessEnabled && Volume < 0.4)
            {
                effectiveGains[0] += 3.0f;
                effectiveGains[1] += 2.0f;
                effectiveGains[8] += 2.0f;
                effectiveGains[9] += 3.0f;
            }

            var equalizer = new BASS_DX8_PARAMEQ[10];
            float[] frequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
            for (int i = 0; i < 10 && i < effectiveGains.Length; i++)
            {
                equalizer[i] = new BASS_DX8_PARAMEQ
                {
                    fGain = effectiveGains[i],
                    fBandwidth = 18.0f,
                    fCenter = frequencies[i]
                };
            }

            for (int i = 0; i < 10; i++)
            {
                int fx = Bass.BASS_ChannelSetFX(stream, BASSFXType.BASS_FX_DX8_PARAMEQ, 1);
                if (fx != 0)
                {
                    Bass.BASS_FXSetParameters(fx, equalizer[i]);
                    effects.EqFxHandles[i] = fx;
                }
            }

            ApplyOptionalEffects(stream, effects, config);
        }

        private static void ApplyOptionalEffects(int stream, StreamEffects effects, AppSettings config)
        {
            if (config.ReverbEnabled)
            {
                int fx = Bass.BASS_ChannelSetFX(stream, BASSFXType.BASS_FX_BFX_FREEVERB, 1);
                if (fx != 0)
                {
                    var reverb = new BASS_BFX_FREEVERB
                    {
                        fDryMix = 0.4f,
                        fWetMix = (float)(1.0 + config.ReverbLevel / 100.0),
                        fRoomSize = 0.7f,
                        fDamp = 0.5f,
                        fWidth = 1.0f
                    };
                    Bass.BASS_FXSetParameters(fx, reverb);
                    effects.ReverbFxHandle = fx;
                }
            }

            if (config.EchoEnabled)
            {
                int fx = Bass.BASS_ChannelSetFX(stream, BASSFXType.BASS_FX_BFX_ECHO4, 1);
                if (fx != 0)
                {
                    var echo = new BASS_BFX_ECHO4
                    {
                        fDryMix = 1.0f,
                        fWetMix = 0.5f,
                        fFeedback = 0.5f,
                        fDelay = (float)Math.Max(0.05, Math.Min(2.0, config.EchoDelay / 1000.0)),
                        bStereo = true
                    };
                    Bass.BASS_FXSetParameters(fx, echo);
                    effects.EchoFxHandle = fx;
                }
            }

            if (config.CompressorEnabled)
            {
                int fx = Bass.BASS_ChannelSetFX(stream, BASSFXType.BASS_FX_BFX_COMPRESSOR2, 1);
                if (fx != 0)
                {
                    var compressor = new BASS_BFX_COMPRESSOR2
                    {
                        fGain = 5.0f,
                        fThreshold = (float)(-60.0 + config.CompressorThreshold * 60.0),
                        fRatio = 4.0f,
                        fAttack = 20.0f,
                        fRelease = 200.0f
                    };
                    Bass.BASS_FXSetParameters(fx, compressor);
                    effects.CompressorFxHandle = fx;
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

            ApplyEqualizerToStream();

            var config = SettingsManager.Instance.Config;
            for (int i = 0; i < gains.Length; i++)
            {
                config.EqualizerGains[i] = gains[i];
            }
            SettingsManager.Instance.Save();
        }

        public void ApplyCurrentEqGains()
        {
            ApplyEqualizerToStream();
        }

        public void ApplyAudioEffects()
        {
            ApplyEqualizerToStream();
        }

        public void ApplyPlaybackRate(double tempo, double pitch)
        {
            var config = SettingsManager.Instance.Config;
            config.Tempo = tempo;
            config.Pitch = pitch;
            SettingsManager.Instance.Save();

            if (_currentStream != 0)
            {
                Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_TEMPO, (float)((tempo - 1.0) * 100.0));
                float pitchSemitones = pitch > 0 ? (float)(Math.Log(pitch, 2.0) * 12.0) : 0f;
                Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_TEMPO_PITCH, pitchSemitones);
            }
        }

        public void SetBalance(float balance)
        {
            if (_currentStream != 0)
            {
                Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_PAN, balance);
            }
        }
        public static List<OutputDeviceInfo> GetOutputDevices()
        {
            var devices = new List<OutputDeviceInfo>();
            int count = Bass.BASS_GetDeviceCount();
            for (int i = 0; i < count; i++)
            {
                var info = Bass.BASS_GetDeviceInfo(i);
                if (info != null)
                {
                    devices.Add(new OutputDeviceInfo(i, info.name));
                }
            }
            return devices;
        }

        public void SetOutputDevice(int deviceId)
        {
            var config = SettingsManager.Instance.Config;
            config.OutputDeviceId = deviceId;
            SettingsManager.Instance.Save();

            if (_isInitialized)
            {
                if (!Bass.BASS_Init(deviceId, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero)
                    && Bass.BASS_ErrorGetCode() != BASSError.BASS_ERROR_ALREADY)
                {
                    return;
                }
                Bass.BASS_SetDevice(deviceId);

                if (_currentStream != 0 && Bass.BASS_ChannelIsActive(_currentStream) != BASSActive.BASS_ACTIVE_STOPPED)
                {
                    Bass.BASS_ChannelSetDevice(_currentStream, deviceId);
                }
                if (_crossfadeStream != 0 && Bass.BASS_ChannelIsActive(_crossfadeStream) != BASSActive.BASS_ACTIVE_STOPPED)
                {
                    Bass.BASS_ChannelSetDevice(_crossfadeStream, deviceId);
                }

                if (_usesWasapiCurrentStream && IsPlaying && _currentStream != 0)
                {
                    Bass.BASS_ChannelGetInfo(_currentStream, _channelInfo);
                    WasapiEngine.Instance.Start(
                        _currentStream,
                        _channelInfo,
                        deviceId,
                        (float)(_volume * _masterGain));
                }
            }
        }

        private void EndSyncCallback(int handle, int channel, int data, IntPtr user)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // The callback may already be queued when the user switches tracks.
                if (channel != _currentStream || CurrentTrack == null || _crossfadeStream != 0) return;

                IncrementCurrentTrackPlayCount();

                PlayNextTrack();
            });
        }

        private void WasapiPlaybackEnded(int stream)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (!_usesWasapiCurrentStream || stream != _currentStream || CurrentTrack == null) return;

                IncrementCurrentTrackPlayCount();
                PlayNextTrack();
            });
        }

        private void IncrementCurrentTrackPlayCount()
        {
            if (_playCountIncremented || CurrentTrack == null) return;

            _playCountIncremented = true;
            Track track = CurrentTrack;
            int trackId = track.Id;

            _ = Task.Run(() =>
            {
                if (!DatabaseService.IncrementTrackPlayCount(trackId)) return;

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    track.PlayCount++;
                    PlayCountUpdated?.Invoke(trackId);
                });
            });
        }

        public void LoadTrack(Track track)
        {
            try
            {
                Stop();
                CurrentTrack = track;
                _playCountIncremented = false;
                _listenedSeconds = 0;
                _lastCountPosition = 0;

                _usesWasapiCurrentStream = SettingsManager.Instance.Config.UseWASAPI;
                int stream = CreateStreamFromFile(track.Path, _usesWasapiCurrentStream);

                if (stream == 0)
                {
                    int error = (int)Bass.BASS_ErrorGetCode();
                    throw new Exception($"Failed to load stream. BASS error: {error}");
                }

                _currentStream = stream;

                Bass.BASS_ChannelGetInfo(_currentStream, _channelInfo);
                _sampleRate = _channelInfo.freq;

                float linearVolume = (float)(_volume * _masterGain);
                if (!_usesWasapiCurrentStream)
                {
                    Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);
                }

                ApplyEqualizerToStream();

                long length = Bass.BASS_ChannelGetLength(_currentStream, BASSMode.BASS_POS_BYTE);
                _duration = Bass.BASS_ChannelBytes2Seconds(_currentStream, length);

                if (!_usesWasapiCurrentStream && _endSyncProc != null)
                {
                    Bass.BASS_ChannelSetSync(_currentStream, BASSSync.BASS_SYNC_END, 0, _endSyncProc, IntPtr.Zero);
                }

                IsPlaying = false;
                _positionTimer.Stop();
                _spectrumTimer.Stop();

                if ((_actualPlayingQueue == null || _actualPlayingQueue.Count == 0) && MusicLibrary.Instance.PlaybackQueue.Count > 0)
                {
                    _actualPlayingQueue = [.. MusicLibrary.Instance.PlaybackQueue];
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
            if (_currentStream == 0 || !IsPlaying || !SettingsManager.Instance.Config.IsVisualizerEnabled)
                return;

            foreach (var control in SpectrumControls)
            {
                int barsCount = control.BarCount;
                if (barsCount > 128) barsCount = 128;

                bool hasFft;

                if (_usesWasapiCurrentStream)
                {
                    int bytesRead = BassWasapi.BASS_WASAPI_GetData(_fftBuffer, (int)BASSData.BASS_DATA_FFT1024);
                    hasFft = bytesRead > 0;
                }
                else
                {
                    int bytesRead = Bass.BASS_ChannelGetData(_currentStream, _fftBuffer, (int)BASSData.BASS_DATA_FFT1024);
                    hasFft = bytesRead > 0;
                }

                if (hasFft)
                {
                    if (Native.QampCoreNative.CalculateSpectrumFromFFT(_fftBuffer, _nativeSpectrumBuffer, _nativePeakBuffer, barsCount))
                    {
                        for (int i = 0; i < barsCount; i++)
                        {
                            _scottPlotBuffer[i] = _nativeSpectrumBuffer[i];
                            _scottPlotPeakBuffer[i] = _nativePeakBuffer[i];
                        }

                        control.UpdateSpectrum(_scottPlotBuffer, _scottPlotPeakBuffer, barsCount);
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
                        if (_lastCountPosition >= 0)
                        {
                            double elapsed = newPosition - _lastCountPosition;
                            if (elapsed > 0 && elapsed <= 1.0)
                            {
                                _listenedSeconds += elapsed;
                            }
                        }
                        _lastCountPosition = newPosition;

                        double playCountThreshold = totalDuration > 0
                            ? Math.Min(30.0, totalDuration / 2.0)
                            : double.PositiveInfinity;
                        if (!_playCountIncremented && _listenedSeconds >= playCountThreshold)
                        {
                            IncrementCurrentTrackPlayCount();
                        }

                        Position = newPosition;
                        PositionChanged?.Invoke(Position);

                        var config = SettingsManager.Instance.Config;
                        if (!_crossfadeAttempted &&
                            _crossfadeStream == 0 &&
                            !_usesWasapiCurrentStream &&
                            config.CrossfadeEnabled &&
                            config.CrossfadeDuration > 0 &&
                            totalDuration - newPosition <= config.CrossfadeDuration)
                        {
                            _crossfadeAttempted = true;
                            TryStartCrossfade(newPosition, totalDuration);
                        }

                        if (_crossfadeStream != 0)
                        {
                            UpdateCrossfade(newPosition);
                        }

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
                if (_usesWasapiCurrentStream)
                {
                    WasapiEngine.Instance.Pause();
                }
                else
                {
                    Bass.BASS_ChannelPause(_currentStream);
                }
                if (_crossfadeStream != 0)
                {
                    Bass.BASS_ChannelPause(_crossfadeStream);
                }
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
                if (_usesWasapiCurrentStream)
                {
                    if (WasapiEngine.Instance.IsInitialized)
                    {
                        WasapiEngine.Instance.Resume();
                    }
                    else
                    {
                        WasapiEngine.Instance.Start(
                            _currentStream,
                            _channelInfo,
                            SettingsManager.Instance.Config.OutputDeviceId,
                            (float)(_volume * _masterGain));
                    }
                }
                else
                {
                    Bass.BASS_ChannelPlay(_currentStream, false);
                }
                if (_crossfadeStream != 0)
                {
                    Bass.BASS_ChannelPlay(_crossfadeStream, false);
                }
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
            _playSemaphore.Wait();
            try
            {
                StopInternal();
            }
            finally
            {
                _playSemaphore.Release();
            }
        }
        private void StopInternal()
        {
            if (_usesWasapiCurrentStream)
            {
                WasapiEngine.Instance.Stop();
                _usesWasapiCurrentStream = false;
            }

            if (_crossfadeStream != 0)
            {
                RemoveEffectsFromStream(_crossfadeStream);
                Bass.BASS_ChannelStop(_crossfadeStream);
                Bass.BASS_StreamFree(_crossfadeStream);
                _crossfadeStream = 0;
                _crossfadeTargetTrack = null;
            }

            if (_currentStream != 0)
            {
                RemoveEffectsFromStream(_currentStream);

                if (_endSyncHandle != 0)
                {
                    Bass.BASS_ChannelRemoveSync(_currentStream, _endSyncHandle);
                    _endSyncHandle = 0;
                }

                Bass.BASS_ChannelStop(_currentStream);
                Bass.BASS_StreamFree(_currentStream);
                _currentStream = 0;
            }

            _positionTimer?.Stop();
            _spectrumTimer?.Stop();
            IsPlaying = false;
            _crossfadeAttempted = false;
        }

        private void TryStartCrossfade(double currentPosition, double currentDuration)
        {
            Track? nextTrack = RepeatMode == RepeatMode.RepeatOne ? CurrentTrack : GetNextTrack();
            if (nextTrack == null || currentDuration <= currentPosition) return;

            int stream = 0;
            try
            {
                stream = CreateStreamFromFile(nextTrack.Path);
                if (stream == 0) return;

                Bass.BASS_ChannelGetInfo(stream, _channelInfo);
                double nextDuration = Bass.BASS_ChannelBytes2Seconds(stream, Bass.BASS_ChannelGetLength(stream, BASSMode.BASS_POS_BYTE));

                int targetDeviceId = SettingsManager.Instance.Config.OutputDeviceId;
                Bass.BASS_ChannelSetDevice(stream, targetDeviceId);

                var config = SettingsManager.Instance.Config;

                Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, 0f);
                ApplyEqualizerToStream(stream);

                if (!Bass.BASS_ChannelPlay(stream, false))
                {
                    Bass.BASS_StreamFree(stream);
                    return;
                }

                _crossfadeDuration = Math.Min(config.CrossfadeDuration, Math.Min(currentDuration - currentPosition, nextDuration));
                if (_crossfadeDuration <= 0)
                {
                    Bass.BASS_ChannelStop(stream);
                    Bass.BASS_StreamFree(stream);
                    return;
                }

                _crossfadeStream = stream;
                _crossfadeTargetTrack = nextTrack;
                _crossfadeTargetDuration = nextDuration;
                _crossfadeElapsed = 0;
                _crossfadeLastPosition = currentPosition;

                int fadeMs = (int)(_crossfadeDuration * 1000);
                float masterVolume = (float)(_volume * _masterGain);

                Bass.BASS_ChannelSlideAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_VOL, 0f, fadeMs);

                Bass.BASS_ChannelSlideAttribute(_crossfadeStream, BASSAttribute.BASS_ATTRIB_VOL, masterVolume, fadeMs);
            }
            catch (Exception ex)
            {
                if (stream != 0) Bass.BASS_StreamFree(stream);
                System.Diagnostics.Debug.WriteLine($"Crossfade failed: {ex.Message}");
            }
        }

        private void UpdateCrossfade(double currentPosition)
        {
            double elapsed = currentPosition - _crossfadeLastPosition;
            if (elapsed > 0 && elapsed <= 1.0)
            {
                _crossfadeElapsed += elapsed;
            }
            _crossfadeLastPosition = currentPosition;

            if (_crossfadeElapsed >= _crossfadeDuration)
            {
                CompleteCrossfade();
            }
        }

        private void CompleteCrossfade()
        {
            if (_crossfadeStream == 0 || _crossfadeTargetTrack == null) return;

            int previousStream = _currentStream;
            int previousEndSync = _endSyncHandle;
            IncrementCurrentTrackPlayCount();

            if (previousEndSync != 0)
            {
                Bass.BASS_ChannelRemoveSync(previousStream, previousEndSync);
            }
            RemoveEffectsFromStream(previousStream);
            Bass.BASS_ChannelStop(previousStream);
            Bass.BASS_StreamFree(previousStream);

            _currentStream = _crossfadeStream;
            _crossfadeStream = 0;
            CurrentTrack = _crossfadeTargetTrack;
            _crossfadeTargetTrack = null;
            Duration = _crossfadeTargetDuration;
            _crossfadeTargetDuration = 0;
            _crossfadeDuration = 0;
            _crossfadeElapsed = 0;
            _endSyncHandle = 0;
            _playCountIncremented = false;
            _listenedSeconds = 0;
            _lastCountPosition = 0;
            _crossfadeAttempted = false;

            float linearVolume = (float)(_volume * _masterGain);
            Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_VOL, linearVolume);
            if (_endSyncProc != null)
            {
                _endSyncHandle = Bass.BASS_ChannelSetSync(
                    _currentStream,
                    BASSSync.BASS_SYNC_END,
                    0,
                    _endSyncProc,
                    IntPtr.Zero);
            }

            IsPlaying = true;
            Position = 0;
            TrackChanged?.Invoke(CurrentTrack);
            MainWindow.UpdateOSD();
            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.UpdateLyricsView();
                mainWin.UpdateNextTrackUI();
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
                _lastCountPosition = seconds;
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
            Native.QampCoreNative.ResetCorePeaks();
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

        public void AppendTracksToQueue(List<Track> newTracks)
        {
            if (newTracks == null || newTracks.Count == 0) return;

            _actualPlayingQueue.AddRange(newTracks);

            if (IsShuffleEnabled)
            {
                var shuffledNewTracks = newTracks.OrderBy(x => Guid.NewGuid()).ToList();

                ShuffledQueue.AddRange(shuffledNewTracks);
            }

            foreach (var track in newTracks)
            {
                MusicLibrary.Instance.PlaybackQueue.Add(track);
            }

            System.Diagnostics.Debug.WriteLine($"[QUEUE] Очередь дополнена на {newTracks.Count} треков. Всего: {_actualPlayingQueue.Count}");
        }
    }

    public enum RepeatMode
    {
        NoRepeat,
        RepeatAll,
        RepeatOne
    }
}