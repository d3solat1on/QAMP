using System.IO;
using Windows.Media;
using Windows.Storage.Streams;

namespace QAMP.Services
{
    public partial class MediaControlsManager
    {
        private readonly global::Windows.Media.Playback.MediaPlayer? _dummyPlayer;
        private readonly SystemMediaTransportControls? _smtc;
        private IRandomAccessStream? _thumbnailStream;
        private int _trackInfoUpdateId;

        public event Action? OnPlayRequested;
        public event Action? OnPauseRequested;
        public event Action? OnNextRequested;
        public event Action? OnPreviousRequested;

        public MediaControlsManager()
        {
            try
            {
                _dummyPlayer = new global::Windows.Media.Playback.MediaPlayer();
                _dummyPlayer.CommandManager.IsEnabled = true;

                _smtc = _dummyPlayer.SystemMediaTransportControls;

                if (_smtc != null)
                {
                    _smtc.IsPlayEnabled = true;
                    _smtc.IsPauseEnabled = true;
                    _smtc.IsNextEnabled = true;
                    _smtc.IsPreviousEnabled = true;
                    _smtc.ButtonPressed += Smtc_ButtonPressed;
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "SMTC Error");
            }
        }

        private void Smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {

            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    OnPlayRequested?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    OnPauseRequested?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    OnNextRequested?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    OnPreviousRequested?.Invoke();
                    break;
            }
        }

        public void UpdatePlaybackStatus(bool isPlaying)
        {
            if (_smtc == null) return;
            try
            {
                _smtc.PlaybackStatus = isPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
            }
            catch (Exception ex)
            {
                App.LogException(ex, "SMTC UpdatePlaybackStatusError");
            }
        }

        public async void UpdateTrackInfo(string? name, string? executor, string? album, byte[]? coverImage)
        {
            if (_smtc == null) return;

            int updateId = Interlocked.Increment(ref _trackInfoUpdateId);
            InMemoryRandomAccessStream? thumbnailStream = null;

            try
            {
                var updater = _smtc.DisplayUpdater;

                if (coverImage != null && coverImage.Length > 0)
                {
                    try
                    {
                        thumbnailStream = new InMemoryRandomAccessStream();
                        using (var writer = new DataWriter(thumbnailStream))
                        {
                            writer.WriteBytes(coverImage);
                            await writer.StoreAsync();
                            writer.DetachStream();
                        }

                        thumbnailStream.Seek(0);
                    }
                    catch (Exception imgEx)
                    {
                        thumbnailStream?.Dispose();
                        thumbnailStream = null;
                        App.LogException(imgEx, "SMTC SetThumbnailError");
                    }
                }

                if (updateId != Volatile.Read(ref _trackInfoUpdateId))
                {
                    thumbnailStream?.Dispose();
                    return;
                }

                updater.Type = MediaPlaybackType.Music;
                updater.MusicProperties.Title = name ?? "Unknown Track";
                updater.MusicProperties.Artist = executor ?? "Unknown Artist";
                updater.MusicProperties.AlbumTitle = album ?? "Unknown Album";
#if DEBUG
                updater.AppMediaId = "QAMP_Debug";
#else
                updater.AppMediaId = "QAMP";
#endif
                updater.Thumbnail = thumbnailStream == null
                    ? null
                    : RandomAccessStreamReference.CreateFromStream(thumbnailStream);

                var previousThumbnailStream = _thumbnailStream;
                _thumbnailStream = thumbnailStream;
                updater.Update();
                previousThumbnailStream?.Dispose();
            }
            catch (Exception ex)
            {
                thumbnailStream?.Dispose();
                App.LogException(ex, "UpdateTrackInfoError");
            }
        }
    }
}