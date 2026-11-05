using System.IO;
using QAMP.Models;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Alac;
using Un4seen.Bass.AddOn.Flac;
using Un4seen.Bass.AddOn.Fx;

namespace QAMP.Services;

public partial class PlayerService
{
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

        if (extension == ".m4a" || extension == ".alac")
        {
            stream = BassAlac.BASS_ALAC_StreamCreateFile(filePath, 0, 0, streamFlags);
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
}
