using System.Globalization;
using System.Windows.Data;

namespace QAMP.Converters
{
    public class PercentOfConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is double value && values[1] is double maximum && maximum > 0)
            {
                return value / maximum * 100;
            }
            return 0;
        }
        
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}