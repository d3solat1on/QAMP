using System.Globalization;
using System.Windows.Data;
using QAMP.Services;

namespace QAMP.Converters
{
    public class ByteArrayToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte[] bytes && bytes.Length > 0)
            {
                return CoverImageCacheService.GetImage(bytes, 200);
            }
            return null;
        }
        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}