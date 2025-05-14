using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using VoiceRecorder.Models;

namespace VoiceRecorder.Converters;



/*
public class PlayPauseIconConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        var playIcon = Application.Current.FindResource("PlayIcon");
        var pauseIcon = Application.Current.FindResource("PauseIcon");

        if (values == null || values.Count == 0) return playIcon;

        if (values.Count == 1 && values[0] is bool isPlayingForPlayerButton)
        {
            return isPlayingForPlayerButton ? pauseIcon : playIcon;
        }

        if (values.Count == 3 &&
            values[0] is bool isPlayingGlobally &&
            values[1] is string currentPlayingFileNameVm &&
            values[2] is AudioFileItem itemFile)
        {
            if (itemFile != null && !string.IsNullOrEmpty(itemFile.Name))
            {
                if (isPlayingGlobally &&
                    !string.IsNullOrEmpty(currentPlayingFileNameVm) &&
                    itemFile.Name.Equals(currentPlayingFileNameVm, StringComparison.OrdinalIgnoreCase))
                {
                    return pauseIcon;
                }
            }
        }

        return playIcon;
    }
}
*/


// ...existing code...
public class PlayPauseIconConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        var playIcon = Application.Current.FindResource("PlayIcon");
        var pauseIcon = Application.Current.FindResource("PauseIcon");

        if (values == null || values.Count == 0) return playIcon;

        // This part is for a global player button, if used.
        // If it's bound to a property that's true only when actively playing, it will show "pause".
        // If it's bound to a property that's true when playing OR paused (like the old IsPlaying),
        // it would show "pause" for both, which might not be desired for a toggle.
        if (values.Count == 1 && values[0] is bool isActivelyPlayingGlobalButton)
        {
            return isActivelyPlayingGlobalButton ? pauseIcon : playIcon;
        }

        if (values.Count == 3 &&
            values[0] is bool isActuallyPlayingVm && // This will be the new IsActuallyPlaying property
            values[1] is string currentPlayingFileNameVm &&
            values[2] is AudioFileItem itemFile)
        {
            if (itemFile != null && !string.IsNullOrEmpty(itemFile.Name))
            {
                // If this specific item is the one currently *actively* playing
                if (isActuallyPlayingVm &&
                    !string.IsNullOrEmpty(currentPlayingFileNameVm) &&
                    itemFile.Name.Equals(currentPlayingFileNameVm, StringComparison.OrdinalIgnoreCase))
                {
                    return pauseIcon; // Show PAUSE icon (click to pause)
                }
            }
        }
        // In all other cases (not this file, or this file is paused, or stopped), show PLAY icon
        return playIcon;
    }
}