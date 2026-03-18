using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace QAMP.Converters
{
    public class ByteArrayToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte[] bytes && bytes.Length > 0)
            {
                using var ms = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;

                // ОГРАНИЧИВАЕМ РАЗМЕР ДЕКОДИРОВАНИЯ
                // Это заставит WPF не грузить картинку целиком, а взять только 300 пикселей
                image.DecodePixelWidth = 225;

                image.StreamSource = ms;
                image.EndInit();
                image.Freeze(); // Важно для производительности
                return image;
            }
            return null; // Или стандартная иконка
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}