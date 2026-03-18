using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace QAMP.Converters;
public class UniversalVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible;
        if (value is string str)
            isVisible = !string.IsNullOrWhiteSpace(str);
        else if (value is byte[] bytes)
            isVisible = bytes.Length > 0;
        else if (value is int i)
            isVisible = i > 0;
        else
            isVisible = value != null;

        // Если в параметре передано "Inverted", инвертируем логику
        if (parameter?.ToString() == "Inverted")
            isVisible = !isVisible;

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
        => throw new NotImplementedException();
}