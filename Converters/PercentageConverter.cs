using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace QAMP.Converters
{
    public class PercentageConverter : MarkupExtension, IValueConverter
    {
        private static PercentageConverter? _instance;
        
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return _instance ??= new PercentageConverter();
        }
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue && doubleValue >= 0)
            {
                // Возвращаем значение в процентах от максимального (100)
                return doubleValue;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}