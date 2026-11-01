using Un4seen.Bass;
using QAMP.Models;
using QAMP.Visualization;
using Un4seen.BassWasapi;

namespace QAMP.Services;

public partial class PlayerService
{
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

    private void UpdateSpectrumTimerInterval()
    {
        int fps = SettingsManager.Instance.Config.SpectrumFps;
        if (fps <= 0) fps = 60;

        _spectrumTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
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
}
