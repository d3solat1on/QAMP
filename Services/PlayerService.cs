using System.Windows.Threading;
using Un4seen.Bass;
using QAMP.Models;
using QAMP.Visualization;

namespace QAMP.Services
{
    public record OutputDeviceInfo(int Id, string Name);

    public partial class PlayerService : IDisposable
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
        // private int _sampleRate = 44100;
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

        private double _lastVolume = 0.5;  //
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

            UpdateSpectrumTimerInterval();
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

    }

    public enum RepeatMode
    {
        NoRepeat,
        RepeatAll,
        RepeatOne
    }
}