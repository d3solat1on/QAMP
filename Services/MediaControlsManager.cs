using Windows.Media;

namespace QAMP.Services
{
    public partial class MediaControlsManager
    {
        private readonly global::Windows.Media.Playback.MediaPlayer? _dummyPlayer;
        private readonly SystemMediaTransportControls? _smtc;

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

        public void UpdateTrackInfo(string? name, string? executor, string? album)
        {
            if (_smtc == null) return;

            try
            {
                var updater = _smtc.DisplayUpdater;
                updater.Type = MediaPlaybackType.Music;

                updater.MusicProperties.Title = name ?? "Unknown Track";
                updater.MusicProperties.Artist = executor ?? "Unknown Artist";
                updater.MusicProperties.AlbumTitle = album ?? "Unknown Album";


                updater.Update();
            }
            catch (Exception ex)
            {
                App.LogException(ex, "UpdateTrackInfoError");
            }
        }
    }
}