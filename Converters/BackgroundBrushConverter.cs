using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using QAMP.Models;

namespace QAMP.Converters;

public class BackgroundBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? elementPart = parameter as string;

        var manager = SettingsManager.Instance;
        var config = manager?.Config;

        string bgPath = (value as string) ?? string.Empty;

        if (config == null || !config.UseCustomBackground || string.IsNullOrWhiteSpace(bgPath))
        {
            return elementPart switch
            {
                "Header" => Application.Current.TryFindResource("DataGridHeaderBackground") as Brush
                            ?? new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)),
                "Cell" => Application.Current.TryFindResource("DataGridCellBackground") as Brush
                            ?? Brushes.Transparent,
                "Row" => Application.Current.TryFindResource("DataGridRowBackground") as Brush
                            ?? new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)),
                "RowAlt" => Application.Current.TryFindResource("DataGridRowAltBackground") as Brush
                            ?? new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                "Controls" => Application.Current.TryFindResource("CentralPanelHeaderBackground") as Brush ?? Brushes.Transparent,
                _ => Application.Current.TryFindResource("TertiaryBackgroundBrush") as Brush
                            ?? Brushes.Transparent
            };
        }

        if (elementPart == "Cell" || elementPart == "Row" || elementPart == "RowAlt")
        {
            return Brushes.Transparent;
        }

        bool isLight = config.ColorScheme == "Light";
        bool isHeader = elementPart == "Header";

        if (isLight)
        {
            Color lightColor;
            if (isHeader)
            {
                lightColor = Color.FromArgb(0xE6, 0xEF, 0xEF, 0xEF);
            }
            else
            {
                lightColor = Color.FromArgb(0xCC, 0xEF, 0xEF, 0xEF);
            }

            return new SolidColorBrush(lightColor);
        }
        else
        {
            Color darkColor;
            if (isHeader)
            {
                darkColor = Color.FromArgb(0xE6, 0x11, 0x11, 0x11);
            }
            else
            {
                darkColor = Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E);
            }

            return new SolidColorBrush(darkColor);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}