using System.Windows;
using System.Globalization;

namespace QAMP.Services
{
    public static class LocalizationService
    {

        public static string GetString(string key)
        {
            try
            {
                var resource = Application.Current.TryFindResource(key);
                return resource?.ToString() ?? $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
        }

        public static string GetFormattedString(string key, params object[] args)
        {
            var format = GetString(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        public static string GetLocalizedString(string value, string defaultValueKey, params object[] args)
        {
            if (string.IsNullOrEmpty(value) || value == "null")
            {
                return GetFormattedString(defaultValueKey, args);
            }
            return value;
        }
    }
}