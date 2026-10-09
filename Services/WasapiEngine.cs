using System.Diagnostics;
using System.Runtime.InteropServices;
using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace QAMP.Services;

public class WasapiEngine
{
    private static WasapiEngine? _instance;
    public static WasapiEngine Instance => _instance ??= new WasapiEngine();
    private int _decoderStream;
    private readonly WASAPIPROC _wasapiProc;
    private bool _isInitialized;
    private bool _isStarted;
    private bool _sourceEnded;
    private int _endNotified;
    private float _volume = 1f;
    private float[]? _decodeBuffer;
    private GCHandle _decodeBufferHandle;
    private int _sourceFrequency;
    private int _sourceChannels;
    private int _outputFrequency;
    private int _outputChannels;
    private int _bufferedSourceFrames;
    private double _sourceFramePosition;

    private WasapiEngine()
    {
        _wasapiProc = WasapiCallback;
    }

    public event Action<int>? PlaybackEnded;
    public bool IsInitialized => _isInitialized;

    public void Start(int decoderStream, BASS_CHANNELINFO channelInfo, int bassDeviceId, float volume)
    {
        Stop();
        _decoderStream = decoderStream;
        _endNotified = 0;
        _volume = volume;

        int deviceId = ResolveWasapiDevice(bassDeviceId);
        var deviceInfo = GetOutputDeviceInfo(deviceId);

        BASSWASAPIInit flags = BASSWASAPIInit.BASS_WASAPI_EXCLUSIVE | BASSWASAPIInit.BASS_WASAPI_BUFFER;

        bool initSuccess = BassWasapi.BASS_WASAPI_Init(
            deviceId,
            channelInfo.freq,
            channelInfo.chans,
            flags,
            0.1f,
            0.01f,
            _wasapiProc,
            IntPtr.Zero);

        if (!initSuccess && deviceInfo != null && deviceInfo.mixfreq > 0 && deviceInfo.mixchans > 0)
        {
            initSuccess = BassWasapi.BASS_WASAPI_Init(
                deviceId,
                deviceInfo.mixfreq,
                deviceInfo.mixchans,
                BASSWASAPIInit.BASS_WASAPI_SHARED,
                0.1f,
                0.01f,
                _wasapiProc,
                IntPtr.Zero);
        }

        if (!initSuccess)
        {
            int error = (int)Bass.BASS_ErrorGetCode();
            _decoderStream = 0;
            throw new InvalidOperationException($"Failed to initialize WASAPI. BASS error: {error}");
        }

        _isInitialized = true;
        var wasapiInfo = BassWasapi.BASS_WASAPI_GetInfo();
        if (wasapiInfo == null)
        {
            int error = (int)Bass.BASS_ErrorGetCode();
            Stop();
            throw new InvalidOperationException($"Failed to get WASAPI format. BASS error: {error}");
        }
        
        _sourceFrequency = channelInfo.freq;
        _sourceChannels = channelInfo.chans;
        _outputFrequency = wasapiInfo.freq;
        _outputChannels = wasapiInfo.chans;
        _bufferedSourceFrames = 0;
        _sourceFramePosition = 0;
        _sourceEnded = false;

        if (_sourceFrequency != _outputFrequency || _sourceChannels != _outputChannels)
        {
            long requiredOutputBytes = Math.Max(
                wasapiInfo.buflen,
                (long)_outputFrequency * _outputChannels * sizeof(float));
            int outputFrames = checked((int)(requiredOutputBytes / (_outputChannels * sizeof(float))));
            int sourceBufferFrames = checked((int)Math.Ceiling(
                (double)outputFrames * _sourceFrequency / _outputFrequency)) + 4;

            _decodeBuffer = new float[checked(sourceBufferFrames * _sourceChannels)];
            _decodeBufferHandle = GCHandle.Alloc(_decodeBuffer, GCHandleType.Pinned);
        }

        if (!BassWasapi.BASS_WASAPI_Start())
        {
            int error = (int)Bass.BASS_ErrorGetCode();
            Stop();
            throw new InvalidOperationException($"Failed to start WASAPI. BASS error: {error}");
        }

        _isStarted = true;
    }

    private int WasapiCallback(IntPtr buffer, int length, IntPtr user)
    {
        int stream = Volatile.Read(ref _decoderStream);
        if (stream == 0) return length;

        try
        {
            if (_sourceFrequency == _outputFrequency && _sourceChannels == _outputChannels)
            {
                int bytesRead = Bass.BASS_ChannelGetData(stream, buffer, length);
                if (bytesRead <= 0)
                {
                    NotifyPlaybackEnded(stream);
                    return 0;
                }

                ApplyVolume(buffer, bytesRead, Volatile.Read(ref _volume));
                return bytesRead;
            }

            return Resample(buffer, length, stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WASAPI callback failed: {ex}");
            NotifyPlaybackEnded(stream);
            return 0;
        }
    }

    private unsafe int Resample(IntPtr outputBuffer, int outputLength, int stream)
    {
        if (_decodeBuffer == null || !_decodeBufferHandle.IsAllocated)
        {
            Debug.WriteLine("WASAPI resampling buffer is not available.");
            return 0;
        }

        int outputFrames = outputLength / (_outputChannels * sizeof(float));
        if (outputFrames == 0) return 0;

        new Span<float>((void*)outputBuffer, outputLength / sizeof(float)).Clear();
        double frameStep = (double)_sourceFrequency / _outputFrequency;
        int requiredSourceFrames = checked((int)Math.Floor(
            _sourceFramePosition + (outputFrames - 1) * frameStep)) + 2;

        while (_bufferedSourceFrames < requiredSourceFrames)
        {
            if (_sourceEnded) break;

            int framesToRead = requiredSourceFrames - _bufferedSourceFrames;
            int freeFrames = _decodeBuffer.Length / _sourceChannels - _bufferedSourceFrames;
            if (freeFrames < framesToRead)
            {
                Debug.WriteLine("WASAPI resampling buffer capacity exceeded.");
                break;
            }

            IntPtr target = IntPtr.Add(
                _decodeBufferHandle.AddrOfPinnedObject(),
                _bufferedSourceFrames * _sourceChannels * sizeof(float));
            int bytesRead = Bass.BASS_ChannelGetData(
                stream,
                target,
                framesToRead * _sourceChannels * sizeof(float));
            if (bytesRead <= 0)
            {
                _sourceEnded = true;
                break;
            }

            _bufferedSourceFrames += bytesRead / (_sourceChannels * sizeof(float));
        }

        int availableFrames = _bufferedSourceFrames;
        if (availableFrames < 2)
        {
            if (_sourceEnded) NotifyPlaybackEnded(stream);
            return 0;
        }

        float* output = (float*)outputBuffer;
        float volume = Volatile.Read(ref _volume);
        int writtenFrames = 0;

        for (; writtenFrames < outputFrames; writtenFrames++)
        {
            int firstFrame = (int)_sourceFramePosition;
            if (firstFrame + 1 >= availableFrames) break;

            float fraction = (float)(_sourceFramePosition - firstFrame);
            int firstOffset = firstFrame * _sourceChannels;
            int secondOffset = firstOffset + _sourceChannels;
            int outputOffset = writtenFrames * _outputChannels;

            for (int channel = 0; channel < _outputChannels; channel++)
            {
                float first = GetSourceChannelSample(firstOffset, channel);
                float second = GetSourceChannelSample(secondOffset, channel);
                output[outputOffset + channel] = (first + (second - first) * fraction) * volume;
            }

            _sourceFramePosition += frameStep;
        }

        if (writtenFrames == 0 && _sourceEnded)
        {
            NotifyPlaybackEnded(stream);
            return 0;
        }

        int consumedFrames = Math.Min(
            Math.Max(0, (int)_sourceFramePosition - 1),
            _bufferedSourceFrames - 1);
        if (consumedFrames > 0)
        {
            int remainingFrames = _bufferedSourceFrames - consumedFrames;
            Array.Copy(
                _decodeBuffer,
                consumedFrames * _sourceChannels,
                _decodeBuffer,
                0,
                remainingFrames * _sourceChannels);
            _bufferedSourceFrames = remainingFrames;
            _sourceFramePosition -= consumedFrames;
        }

        return writtenFrames * _outputChannels * sizeof(float);
    }

    private float GetSourceChannelSample(int frameOffset, int outputChannel)
    {
        if (_sourceChannels == 1)
        {
            return _decodeBuffer![frameOffset];
        }

        if (_outputChannels == 1)
        {
            float sum = 0;
            for (int channel = 0; channel < _sourceChannels; channel++)
            {
                sum += _decodeBuffer![frameOffset + channel];
            }

            return sum / _sourceChannels;
        }

        return _decodeBuffer![frameOffset + Math.Min(outputChannel, _sourceChannels - 1)];
    }

    private static unsafe void ApplyVolume(IntPtr buffer, int length, float volume)
    {
        if (Math.Abs(volume - 1f) < 0.001f) return;

        float* samples = (float*)buffer;
        int sampleCount = length / sizeof(float);
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] *= volume;
        }
    }

    private void NotifyPlaybackEnded(int stream)
    {
        if (Interlocked.Exchange(ref _endNotified, 1) == 0)
        {
            PlaybackEnded?.Invoke(stream);
        }
    }

    private static BASS_WASAPI_DEVICEINFO? GetOutputDeviceInfo(int deviceId)
    {
        if (deviceId >= 0)
        {
            return BassWasapi.BASS_WASAPI_GetDeviceInfo(deviceId);
        }

        for (int index = 0; ; index++)
        {
            var info = BassWasapi.BASS_WASAPI_GetDeviceInfo(index);
            if (info == null) return null;

            if ((info.flags & BASSWASAPIDeviceInfo.BASS_DEVICE_INPUT) == 0 &&
                (info.flags & BASSWASAPIDeviceInfo.BASS_DEVICE_DEFAULT) != 0)
            {
                return info;
            }
        }
    }

    public void Pause()
    {
        if (_isStarted && !BassWasapi.BASS_WASAPI_Stop(false))
        {
            int error = (int)Bass.BASS_ErrorGetCode();
            throw new InvalidOperationException($"Failed to pause WASAPI. BASS error: {error}");
        }
        _isStarted = false;
    }

    public void Resume()
    {
        if (_isInitialized && !_isStarted)
        {
            if (!BassWasapi.BASS_WASAPI_Start())
            {
                int error = (int)Bass.BASS_ErrorGetCode();
                throw new InvalidOperationException($"Failed to resume WASAPI. BASS error: {error}");
            }
            _isStarted = true;
        }
    }

    public void SetVolume(float volume)
    {
        Volatile.Write(ref _volume, volume);
    }

    public void Stop()
    {
        if (_isInitialized)
        {
            BassWasapi.BASS_WASAPI_Free();
        }

        if (_decodeBufferHandle.IsAllocated)
        {
            _decodeBufferHandle.Free();
        }

        _decodeBuffer = null;
        _isInitialized = false;
        _isStarted = false;
        _decoderStream = 0;
        _sourceFrequency = 0;
        _sourceChannels = 0;
        _outputFrequency = 0;
        _outputChannels = 0;
        _bufferedSourceFrames = 0;
        _sourceFramePosition = 0;
        _sourceEnded = false;
    }

    private static int ResolveWasapiDevice(int bassDeviceId)
    {
        if (bassDeviceId < 0) return -1;
        string? bassDeviceDriver = Bass.BASS_GetDeviceInfo(bassDeviceId)?.driver;
        if (string.IsNullOrWhiteSpace(bassDeviceDriver)) return -1;

        for (int deviceId = 0; ; deviceId++)
        {
            var info = BassWasapi.BASS_WASAPI_GetDeviceInfo(deviceId);
            if (info == null) break;
            if ((info.flags & (BASSWASAPIDeviceInfo.BASS_DEVICE_INPUT | BASSWASAPIDeviceInfo.BASS_DEVICE_LOOPBACK)) == 0 &&
                string.Equals(info.id, bassDeviceDriver, StringComparison.OrdinalIgnoreCase))
            {
                return deviceId;
            }
        }
        return -1;
    }
}