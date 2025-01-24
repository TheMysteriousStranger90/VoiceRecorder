using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder.Converters;

public class PlayingFileConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var currentFile = value as string;
        var fileParameter = parameter as string;
        return currentFile == fileParameter ? new SolidColorBrush(Color.Parse("#80F")) : new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}