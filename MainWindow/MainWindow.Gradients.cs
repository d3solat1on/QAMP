using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;

namespace QAMP;

public partial class MainWindow
{
    private void UpdateUpperPanelGradient(BitmapSource cover)
    {
        var config = SettingsManager.Instance.Config;

        if (cover == null || !config.UseAdaptiveGradients)
        {
            UpperPanel.Background = GetDefaultUpperPanelBrush(config);
            return;
        }

        try
        {
            Color dominant = ThemeHelper.GetDominantColor(cover);
            Color secondary = ThemeHelper.GetAdaptiveSecondaryColor(dominant);

            if (!string.IsNullOrEmpty(config.CustomBackgroundPath))
            {
                dominant.A = 0xCC;
                secondary.A = 0x66;
            }

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };

            brush.GradientStops.Add(new GradientStop(dominant, 0));
            brush.GradientStops.Add(new GradientStop(secondary, 1));

            UpperPanel.Background = brush;
        }
        catch (Exception ex)
        {
            App.LogException(ex, "GradientUpdate");
            UpperPanel.Background = GetDefaultUpperPanelBrush(config);
        }
    }

    /// <summary>
    /// Обновляет градиент верхней панели для плейлиста "Избранное" на основе главного цвета приложения
    /// </summary>
    private void UpdateUpperPanelGradientForFavorites()
    {
        try
        {
            var config = SettingsManager.Instance.Config;

            if (Application.Current.Resources["AccentBrush"] is SolidColorBrush accentBrush && config.UseAdaptiveGradients)
            {
                Color primary = accentBrush.Color;
                Color secondary = ThemeHelper.GetAdaptiveSecondaryColor(primary);

                if (!string.IsNullOrEmpty(config.CustomBackgroundPath))
                {
                    primary.A = 0xCC;
                    secondary.A = 0x66;
                }

                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };

                brush.GradientStops.Add(new GradientStop(primary, 0));
                brush.GradientStops.Add(new GradientStop(secondary, 1));

                UpperPanel.Background = brush;
            }
            else
            {
                UpperPanel.Background = GetDefaultUpperPanelBrush(config);
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex, "FavoritesGradientUpdate");
            UpperPanel.Background = GetDefaultUpperPanelBrush(SettingsManager.Instance.Config);
        }
    }
    private static Brush GetDefaultUpperPanelBrush(AppSettings config)
    {
        if (!string.IsNullOrEmpty(config.CustomBackgroundPath))
        {
            return Brushes.Transparent;
        }

        return (Brush)Application.Current.Resources["BackgroundBrush"];
    }
    /// <summary>
    /// Обновляет градиент верхней панели при изменении настроек адаптивных градиентов
    /// </summary>
    public void RefreshAdaptiveGradients()
    {
        var currentPlaylist = MusicLibrary.Instance.CurrentPlaylist;
        if (currentPlaylist == null)
            return;

        if (currentPlaylist.IsSystemPlaylist)
        {
            UpdateUpperPanelGradientForFavorites();
        }
        else
        {
            var bitmap = _imageConverter.Convert(currentPlaylist.CoverImage, typeof(BitmapSource), null, System.Globalization.CultureInfo.InvariantCulture) as BitmapSource;
            UpdateUpperPanelGradient(bitmap!);
        }
    }
}