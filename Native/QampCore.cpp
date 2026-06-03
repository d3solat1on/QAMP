#include <windows.h>
#include <math.h>
#include "bass.h"

extern "C" __declspec(dllexport) int GetCoreVersion()
{
    return 106;
}

float g_lastValues[512] = {0};
float g_peakValues[512] = {0};
float g_peakSpeeds[512] = {0};

extern "C" __declspec(dllexport) void ResetCorePeaks()
{
    for (int i = 0; i < 512; i++)
    {
        g_lastValues[i] = 0.0f;
        g_peakValues[i] = 0.0f;
        g_peakSpeeds[i] = 0.0f;
    }
}

extern "C" __declspec(dllexport) bool GetSpectrumDataAdvanced(DWORD channel, float *mainBuffer, float *peakBuffer, int bandsCount)
{
    if (bandsCount > 512)
        bandsCount = 512;

    if (!channel || BASS_ChannelIsActive(channel) != BASS_ACTIVE_PLAYING)
    {
        for (int i = 0; i < bandsCount; i++)
        {
            g_lastValues[i] *= 0.85f;
            mainBuffer[i] = g_lastValues[i] < 0.001f ? 0.0f : g_lastValues[i];

            g_peakSpeeds[i] += 0.005f;
            g_peakValues[i] -= g_peakSpeeds[i];
            if (g_peakValues[i] < 0.0f)
            {
                g_peakValues[i] = 0.0f;
                g_peakSpeeds[i] = 0.0f;
            }
            peakBuffer[i] = g_peakValues[i];
        }
        return true;
    }

    float fft[512];
    int ret = BASS_ChannelGetData(channel, fft, BASS_DATA_FFT1024);
    if (ret == -1)
        return false;

    const float fallOffSpeed = 0.045f;
    const float gravity = 0.0022f;
    const float initialDelay = -0.010f;

    const float minDb = -70.0f;
    const float maxDb = -5.0f;

    for (int i = 0; i < bandsCount; i++)
    {
        double interpolation = (double)i / bandsCount;

        int startIdx = (int)(pow(2.0, pow(interpolation, 0.75) * log2(512.0)));
        int endIdx = (int)(pow(2.0, pow((double)(i + 1) / bandsCount, 0.75) * log2(512.0)));

        if (startIdx >= 512)
            startIdx = 511;
        if (endIdx > 512)
            endIdx = 512;
        if (endIdx <= startIdx)
            endIdx = startIdx + 1;

        float maxVal = 0;
        for (int j = startIdx; j < endIdx; j++)
        {
            if (fft[j] > maxVal)
                maxVal = fft[j];
        }

        if (maxVal < 0.0000001f)
            maxVal = 0.0000001f;
        float db = 20.0f * log10f(maxVal);

        float minDb = -70.0f;
        float maxDb = -3.0f;
        float intensity = (db - minDb) / (maxDb - minDb);
        if (intensity < 0.0f)
            intensity = 0.0f;
        if (intensity > 1.0f)
            intensity = 1.0f;

        intensity = powf(intensity, 1.5f);

        float frequencyFactor = (float)i / bandsCount;
        float boost = 1.0f + frequencyFactor * 0.8f;
        intensity *= boost;
        if (intensity > 0.95f)
            intensity = 0.95f;

        if (intensity >= g_lastValues[i])
            g_lastValues[i] = intensity;
        else
            g_lastValues[i] -= fallOffSpeed;
        if (g_lastValues[i] < 0.0f)
            g_lastValues[i] = 0.0f;

        mainBuffer[i] = g_lastValues[i];

        if (mainBuffer[i] >= g_peakValues[i])
        {
            g_peakValues[i] = mainBuffer[i];
            g_peakSpeeds[i] = initialDelay;
        }
        else
        {
            g_peakSpeeds[i] += gravity;
            g_peakValues[i] -= g_peakSpeeds[i];

            if (g_peakValues[i] < 0.0f)
            {
                g_peakValues[i] = 0.0f;
                g_peakSpeeds[i] = 0.0f;
            }
        }

        peakBuffer[i] = g_peakValues[i];
    }

    return true;
}