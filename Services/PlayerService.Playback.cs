using System.Windows;
using Un4seen.Bass;
using QAMP.Models;
using QAMP.ViewModels;
using QAMP.Dialogs;

namespace QAMP.Services;

public partial class PlayerService
{
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
                    System.Diagnostics.Debug.WriteLine($"[QUEUE] Queue size: {_actualPlayingQueue.Count}");
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
            _ = NotificationWindow.Show($"Error: {ex.Message}", Application.Current.MainWindow);
            System.Diagnostics.Debug.WriteLine($"Error in PlayTrack: {ex.Message}");
            App.LogException(ex, "PlayTrack");
            StopInternal();
        }
        finally
        {
            _playSemaphore.Release();
        }
    }

    public void ToggleMute()
    {
        if (Volume > 0)
        {
            _lastVolume = Volume;
            Volume = 0;
        }
        else
        {
            if (_lastVolume > 0)
            {
                Volume = _lastVolume;
            }
            else
            {
                Volume = 0.2;
            }
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
            App.LogException(ex, "ResumeAfterTagEdit");
        }
        finally
        {
            _playSemaphore.Release();
        }
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
                System.Diagnostics.Debug.WriteLine($"[QUEUE] [QUEUE] Queue restored from PlaybackQueue: {_actualPlayingQueue.Count} tracks");
            }
            else if (_actualPlayingQueue == null || _actualPlayingQueue.Count == 0)
            {
                _actualPlayingQueue = [track];
                System.Diagnostics.Debug.WriteLine("[QUEUE] [QUEUE] Current track set as the only entry in the queue");
            }

            TrackChanged?.Invoke(track);
        }
        catch (Exception ex)
        {
            _ = NotificationWindow.Show($"Error: {ex.Message}", Application.Current.MainWindow);
            System.Diagnostics.Debug.WriteLine($"Error in LoadTrack: {ex.Message}");
            App.LogException(ex, "Load track");
            Stop();
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
}
