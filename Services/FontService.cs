using System.IO;
using System.Windows;
using System.Windows.Media;

namespace QAMP.Services;

public class FontService
{
    private static FontService? _instance;
    public static FontService Instance => _instance ??= new FontService();

    private readonly string _fontsFolder;
    
    public const string DefaultFontKey = "GOST type A";
    private const string DefaultFontPackUri = "pack://application:,,,/Resources/Fonts/#GOST type A";

    public List<FontOption> AvailableFonts { get; } = [];

    public record FontOption(string DisplayName, FontFamily Family, bool IsCustom, string? FilePath = null);

    public FontOption DefaultFont => AvailableFonts.First(font => !font.IsCustom);

    private FontService()
    {
        _fontsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
        if (!Directory.Exists(_fontsFolder))
        {
            Directory.CreateDirectory(_fontsFolder);
        }

        RefreshFontList();
    }

    public void RefreshFontList()
    {
        AvailableFonts.Clear();

        AvailableFonts.Add(new FontOption("GOST Type A", new FontFamily(DefaultFontPackUri), false));

        var fontFiles = Directory.GetFiles(_fontsFolder, "*.*")
            .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || 
                         f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

        foreach (var file in fontFiles)
        {
            try
            {
                var baseUri = new Uri(_fontsFolder + Path.DirectorySeparatorChar);
                var families = Fonts.GetFontFamilies(baseUri, "./" + Path.GetFileName(file));
                foreach (var family in families)
                {
                    string fontName = GetFamilyName(family);
                    
                    if (!AvailableFonts.Any(f => f.DisplayName == fontName))
                    {
                        AvailableFonts.Add(new FontOption(fontName, family, true, file));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FontService] Ошибка чтения шрифта {file}: {ex.Message}");
            }
        }
    }

    public FontOption? FindFont(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return null;
        }

        return AvailableFonts.FirstOrDefault(font =>
            string.Equals(font.DisplayName, fontName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetFamilyName(font.Family), fontName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(font.Family.Source, fontName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetFamilyName(FontFamily family)
    {
        var source = family.Source;
        var separatorIndex = source.LastIndexOf('#');
        return separatorIndex >= 0 ? source[(separatorIndex + 1)..] : source;
    }

    /// <summary>
    /// Переопределяет ресурс MainFont во всем приложении WPF
    /// </summary>
    public static void ApplyFontToApplication(FontFamily newFontFamily)
    {
        Application.Current?.Resources["MainFont"] = newFontFamily;
    }

    /// <summary>
    /// Копирует новый файл шрифта в папку Fonts/ и возвращает созданный FontOption
    /// </summary>
    public FontOption? ImportFont(string sourceFilePath)
    {
        try
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destPath = Path.Combine(_fontsFolder, fileName);

            File.Copy(sourceFilePath, destPath, overwrite: true);

            var uri = new Uri(_fontsFolder + "/");
            var families = Fonts.GetFontFamilies(uri, "./" + fileName);

            var family = families.FirstOrDefault();
            if (family is not null)
            {
                string fontName = GetFamilyName(family);
                var existingFont = AvailableFonts.FirstOrDefault(font =>
                    string.Equals(font.DisplayName, fontName, StringComparison.OrdinalIgnoreCase));
                if (existingFont is not null)
                {
                    return existingFont;
                }

                var option = new FontOption(fontName, family, true, destPath);
                
                AvailableFonts.Add(option);
                return option;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FontService] Ошибка импорта шрифта: {ex.Message}");
        }

        return null;
    }
}