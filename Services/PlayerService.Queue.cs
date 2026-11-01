using System.Windows;
using Un4seen.Bass;
using QAMP.Models;
using QAMP.ViewModels;

namespace QAMP.Services;

public partial class PlayerService
{
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
            App.LogException(ex, "TryStartCrossfade");
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
            StopAtEndOfQueue();
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
            StopAtEndOfQueue();
        }

        MainWindow.UpdateOSD();
        if (Application.Current.MainWindow is MainWindow mainWin)
        {
            mainWin.UpdateLyricsView();
            mainWin.UpdateNextTrackUI();
        }
    }

    private void StopAtEndOfQueue()
    {
        Stop();
        PlaybackPaused?.Invoke(true);
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

        System.Diagnostics.Debug.WriteLine($"[QUEUE] Queue updated with {newTracks.Count} tracks. Total: {_actualPlayingQueue.Count}");
    }
}
