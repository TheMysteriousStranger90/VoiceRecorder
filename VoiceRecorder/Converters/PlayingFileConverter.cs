using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using Avalonia.Controls;
using VoiceRecorder.Models;

namespace VoiceRecorder.Converters;

public class PlayingFileConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        IBrush defaultBrush = Brushes.WhiteSmoke;
        IBrush playingBrush = Brushes.MediumPurple;

        if (Application.Current.TryFindResource("AccentBrush", out var accentBrushResource) &&
            accentBrushResource is IBrush accBrush)
        {
            playingBrush = accBrush;
        }

        if (value is string currentPlayingFileName && parameter is AudioFileItem itemFile)
        {
            if (!string.IsNullOrEmpty(currentPlayingFileName) &&
                itemFile.Name.Equals(currentPlayingFileName, StringComparison.OrdinalIgnoreCase))
            {
                return playingBrush;
            }
        }

        return defaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}