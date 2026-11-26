using System.IO;
using QAMP.Models;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Alac;
using Un4seen.Bass.AddOn.Flac;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.AddOn.Opus;

namespace QAMP.Services;

public partial class PlayerService
{
    private static int CreateStreamFromFile(string filePath, bool useDecodeFlags = false)
    {
        int sourceStream = CreateRawStreamFromFile(filePath, useDecodeFlags: true);
        if (sourceStream == 0) return 0;

        BASSFlag tempoFlags = BASSFlag.BASS_FX_FREESOURCE;
        if (useDecodeFlags)
        {
            tempoFlags |= BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT;
        }

        int tempoStream = BassFx.BASS_FX_TempoCreate(sourceStream, tempoFlags);
        if (tempoStream == 0)
        {
            int error = (int)Bass.BASS_ErrorGetCode();
            Bass.BASS_StreamFree(sourceStream);
            System.Diagnostics.Debug.WriteLine($"[AUDIO] Failed to create tempo stream for '{filePath}'. BASS error: {error}");
        }

        return tempoStream;
    }

    private static int CreateRawStreamFromFile(string filePath, bool useDecodeFlags)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        BASSFlag streamFlags = useDecodeFlags
            ? BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_PRESCAN
            : BASSFlag.BASS_DEFAULT;

        int stream = 0;
        if (extension == ".flac")
        {
            stream = BassFlac.BASS_FLAC_StreamCreateFile(filePath, 0, 0, streamFlags);
        }
        else if (extension == ".m4a" || extension == ".alac")
        {
            stream = BassAlac.BASS_ALAC_StreamCreateFile(filePath, 0, 0, streamFlags);
        }
        else if (extension == ".opus")
        {
            stream = BassOpus.BASS_OPUS_StreamCreateFile(filePath, 0, 0, streamFlags);
        }

        if (stream == 0)
        {
            stream = Bass.BASS_StreamCreateFile(filePath, 0, 0, streamFlags);
        }

        return stream;
    }

    internal static int CreateDecodeStreamFromFile(string filePath) =>
        CreateRawStreamFromFile(filePath, useDecodeFlags: true);

    private void RemoveEffectsFromStream(int stream)
    {
        if (stream == 0) return;

        if (!_streamEffects.Remove(stream, out StreamEffects? effects)) return;

        if (effects.EqFxHandle != 0)
        {
            Bass.BASS_ChannelRemoveFX(stream, effects.EqFxHandle);
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

        if (effects.EqFxHandle != 0)
        {
            Bass.BASS_ChannelRemoveFX(stream, effects.EqFxHandle);
            effects.EqFxHandle = 0;
        }

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
        if (!Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_PAN, (float)config.Balance))
        {
            LogBassFailure(stream, "setting balance");
        }
        ApplyPlaybackRateToStream(stream, config.Tempo, config.Pitch);

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

        float[] frequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
        int equalizerFx = CreateEffect(stream, BASSFXType.BASS_FX_BFX_PEAKEQ, "equalizer");
        if (equalizerFx != 0)
        {
            var channelInfo = new BASS_CHANNELINFO();
            float maxFrequency = Bass.BASS_ChannelGetInfo(stream, channelInfo)
                ? channelInfo.freq / 2f
                : frequencies[^1];
            bool equalizerConfigured = true;

            for (int i = 0; i < frequencies.Length && i < effectiveGains.Length; i++)
            {
                var band = new BASS_BFX_PEAKEQ
                {
                    lBand = i,
                    fBandwidth = 1.0f,
                    fCenter = Math.Min(frequencies[i], maxFrequency),
                    fGain = effectiveGains[i],
                    lChannel = BASSFXChan.BASS_BFX_CHANALL
                };

                if (!TrySetEffectParameters(equalizerFx, band, $"equalizer band {i + 1}"))
                {
                    equalizerConfigured = false;
                    break;
                }
            }

            if (equalizerConfigured)
            {
                effects.EqFxHandle = equalizerFx;
            }
            else
            {
                Bass.BASS_ChannelRemoveFX(stream, equalizerFx);
            }
        }

        ApplyOptionalEffects(stream, effects, config);
    }

    private static void ApplyOptionalEffects(int stream, StreamEffects effects, AppSettings config)
    {
        if (config.ReverbEnabled)
        {
            int fx = CreateEffect(stream, BASSFXType.BASS_FX_BFX_FREEVERB, "reverb");
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
                if (TrySetEffectParameters(fx, reverb, "reverb"))
                {
                    effects.ReverbFxHandle = fx;
                }
                else
                {
                    Bass.BASS_ChannelRemoveFX(stream, fx);
                }
            }
        }

        if (config.EchoEnabled)
        {
            int fx = CreateEffect(stream, BASSFXType.BASS_FX_BFX_ECHO4, "echo");
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
                if (TrySetEffectParameters(fx, echo, "echo"))
                {
                    effects.EchoFxHandle = fx;
                }
                else
                {
                    Bass.BASS_ChannelRemoveFX(stream, fx);
                }
            }
        }

        if (config.CompressorEnabled)
        {
            int fx = CreateEffect(stream, BASSFXType.BASS_FX_BFX_COMPRESSOR2, "compressor");
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
                if (TrySetEffectParameters(fx, compressor, "compressor"))
                {
                    effects.CompressorFxHandle = fx;
                }
                else
                {
                    Bass.BASS_ChannelRemoveFX(stream, fx);
                }
            }
        }
    }

    private static int CreateEffect(int stream, BASSFXType type, string name)
    {
        int fx = Bass.BASS_ChannelSetFX(stream, type, 1);
        if (fx == 0)
        {
            LogBassFailure(stream, $"creating {name} effect");
        }

        return fx;
    }

    private static bool TrySetEffectParameters(int fx, BASS_BFX_PEAKEQ parameters, string name)
    {
        if (Bass.BASS_FXSetParameters(fx, parameters)) return true;
        LogBassFailure(fx, $"configuring {name}");
        return false;
    }

    private static bool TrySetEffectParameters(int fx, BASS_BFX_FREEVERB parameters, string name)
    {
        if (Bass.BASS_FXSetParameters(fx, parameters)) return true;
        LogBassFailure(fx, $"configuring {name}");
        return false;
    }

    private static bool TrySetEffectParameters(int fx, BASS_BFX_ECHO4 parameters, string name)
    {
        if (Bass.BASS_FXSetParameters(fx, parameters)) return true;
        LogBassFailure(fx, $"configuring {name}");
        return false;
    }

    private static bool TrySetEffectParameters(int fx, BASS_BFX_COMPRESSOR2 parameters, string name)
    {
        if (Bass.BASS_FXSetParameters(fx, parameters)) return true;
        LogBassFailure(fx, $"configuring {name}");
        return false;
    }

    private static void LogBassFailure(int handle, string operation)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[AUDIO] BASS failed while {operation} (handle {handle}). Error: {Bass.BASS_ErrorGetCode()}");
    }

    public void UpdateEqualizerGains(float[] gains)
    {
        for (int i = 0; i < gains.Length && i < EqGains.Length; i++)
        {
            EqGains[i] = gains[i];
        }

        ApplyEqualizerToStream();

        var config = SettingsManager.Instance.Config;
        for (int i = 0; i < gains.Length && i < config.EqualizerGains.Length; i++)
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
            ApplyPlaybackRateToStream(_currentStream, tempo, pitch);
        }
        if (_crossfadeStream != 0)
        {
            ApplyPlaybackRateToStream(_crossfadeStream, tempo, pitch);
        }
    }

    private static void ApplyPlaybackRateToStream(int stream, double tempo, double pitch)
    {
        if (!Bass.BASS_ChannelSetAttribute(
                stream,
                BASSAttribute.BASS_ATTRIB_TEMPO,
                (float)((tempo - 1.0) * 100.0)))
        {
            LogBassFailure(stream, "setting tempo");
        }

        float pitchSemitones = pitch > 0 ? (float)(Math.Log(pitch, 2.0) * 12.0) : 0f;
        if (!Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_TEMPO_PITCH, pitchSemitones))
        {
            LogBassFailure(stream, "setting pitch");
        }
    }

    public void SetBalance(float balance)
    {
        if (_currentStream != 0)
        {
            if (!Bass.BASS_ChannelSetAttribute(_currentStream, BASSAttribute.BASS_ATTRIB_PAN, balance))
            {
                LogBassFailure(_currentStream, "setting balance");
            }
        }
        if (_crossfadeStream != 0)
        {
            if (!Bass.BASS_ChannelSetAttribute(_crossfadeStream, BASSAttribute.BASS_ATTRIB_PAN, balance))
            {
                LogBassFailure(_crossfadeStream, "setting balance");
            }
        }
    }
    public async Task SwitchOutputModeAsync(bool enableWasapi)
    {
        System.Diagnostics.Debug.WriteLine($"[AUDIO] Switching output mode. WASAPI enabled: {enableWasapi}");

        await _playSemaphore.WaitAsync();
        try
        {
            double currentPosition = Position;
            bool wasPlaying = IsPlaying;
            Track? track = CurrentTrack;

            if (track == null) return;

            StopInternal();

            if (!enableWasapi && WasapiEngine.Instance.IsInitialized)
            {
                WasapiEngine.Instance.Stop();
            }

            _usesWasapiCurrentStream = enableWasapi;

            int stream = CreateStreamFromFile(track.Path, _usesWasapiCurrentStream);
            if (stream == 0)
            {
                throw new Exception($"Failed to create stream. Error: {Bass.BASS_ErrorGetCode()}");
            }

            Bass.BASS_ChannelGetInfo(stream, _channelInfo);
            _currentStream = stream;
            ApplyEqualizerToStream();

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
                int targetDeviceId = SettingsManager.Instance.Config.OutputDeviceId;
                Bass.BASS_ChannelSetDevice(_currentStream, targetDeviceId);
                if (!Bass.BASS_ChannelPlay(_currentStream, false))
                {
                    throw new Exception($"Failed to play stream. Error: {Bass.BASS_ErrorGetCode()}");
                }
            }

            Seek(currentPosition);

            IsPlaying = wasPlaying;
            if (wasPlaying)
            {
                _positionTimer.Start();
                _spectrumTimer.Start();
            }
            else
            {
                await PauseAsync();
            }
        }
        finally
        {
            _playSemaphore.Release();
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
}
