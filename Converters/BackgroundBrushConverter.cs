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
        var config = SettingsManager.Instance.Config;
        string bgPath = (value as string) ?? string.Empty;

        if (!config.UseCustomBackground || string.IsNullOrWhiteSpace(bgPath))
        {
            return GetThemeBrush(elementPart switch
            {
                "Header" => "DataGridHeaderBackground",
                "Cell" => "DataGridCellBackground",
                "Row" => "DataGridRowBackground",
                "RowAlt" => "DataGridRowAltBackground",
                "Selected" => "DataGridRowSelectedBackground",
                "Controls" => "CentralPanelHeaderBackground",
                _ => "TertiaryBackgroundBrush"
            }, elementPart switch
            {
                "Header" => new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)),
                "Row" => new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)),
                "RowAlt" => new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                _ => Brushes.Transparent
            });
        }

        if (elementPart == "Selected")
        {
            var selectionBrush = GetThemeBrush("DataGridRowSelectedBackground", Brushes.Transparent).CloneCurrentValue();
            selectionBrush.Opacity *= 0.4;

            if (selectionBrush.CanFreeze)
            {
                selectionBrush.Freeze();
            }

            return selectionBrush;
        }

        if (elementPart == "Cell" || elementPart == "Row" || elementPart == "RowAlt")
        {
            return Brushes.Transparent;
        }

        string overlayResourceKey = elementPart switch
        {
            "Header" => "DataGridHeaderBackground",
            "Controls" => "CentralPanelHeaderBackground",
            _ => "SecondaryBackgroundBrush"
        };
        var overlayBrush = GetThemeBrush(
            overlayResourceKey,
            GetThemeBrush("BackgroundBrush", Brushes.Transparent));
        var result = overlayBrush.CloneCurrentValue();
        result.Opacity *= (elementPart == "Header" ? 0xE6 : 0xCC) / 255d;

        if (result.CanFreeze)
        {
            result.Freeze();
        }

        return result;
    }

    private static Brush GetThemeBrush(string key, Brush fallback)
        => Application.Current?.TryFindResource(key) as Brush ?? fallback;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}