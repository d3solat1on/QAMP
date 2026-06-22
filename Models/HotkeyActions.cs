using System.Windows.Input;

namespace QAMP.Models
{
    public enum HotkeyAction
    {
        TogglePlayPause,
        SeekForward,
        SeekBackward,
        VolumeUp,
        VolumeDown,
        NextTrack,
        PreviousTrack,
        ViewLyrics,
        ShowTrackInfo,
        ToggleRepeat,
        ToggleShuffle,
        OpenFullScreenSpectrum,
        ToggleFocusGrid,
        ToggleFavorite
    }
}
namespace QAMP.Models
{
    public class HotkeyItem
    {
        public HotkeyAction Action { get; set; }
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }

        public HotkeyItem(HotkeyAction action, Key key, ModifierKeys modifiers = ModifierKeys.Control)
        {
            Action = action;
            Key = key;
            Modifiers = modifiers;
        }

        public HotkeyItem() { }
    }
}