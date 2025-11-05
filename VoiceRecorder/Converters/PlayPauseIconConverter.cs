using Avalonia;
using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Controls;
using VoiceRecorder.Models;

namespace VoiceRecorder.Converters;

public class PlayPauseIconConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var playIcon = Application.Current?.FindResource("PlayIcon");
        var pauseIcon = Application.Current?.FindResource("PauseIcon");

        if (values == null || values.Count == 0)
            return playIcon;

        if (values.Count == 1 && values[0] is bool isActivelyPlayingGlobalButton)
        {
            return isActivelyPlayingGlobalButton ? pauseIcon : playIcon;
        }

        if (values.Count == 3 &&
            values[0] is bool isActuallyPlayingVm &&
            values[1] is string currentPlayingFileNameVm &&
            values[2] is AudioFileItem itemFile)
        {
            if (!string.IsNullOrEmpty(itemFile.Name))
            {
                if (isActuallyPlayingVm &&
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
