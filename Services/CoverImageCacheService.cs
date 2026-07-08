using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace QAMP.Services;

public static class CoverImageCacheService
{
    private static readonly Lock _syncRoot = new();
    private static readonly Dictionary<string, CacheEntry> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> _accessOrder = new();
    private const int MaxMemoryEntries = 250;
    private const long MaxMemorySizeBytes = 50_000_000;
    private static long _currentMemorySize = 0;

    private static string GetCacheDirectory()
    {
        string basePath = AppDataManager.AppDataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QAMP");
        return Path.Combine(basePath, "cover_cache");
    }

    public static bool IsEnabled
    {
        get
        {
            try
            {
                return Models.SettingsManager.Instance.Config.EnableCoverCache;
            }
            catch
            {
                return true;
            }
        }
    }

    public static BitmapImage? GetImage(string trackPath, byte[]? bytes, int decodePixelWidth)
    {
        if (string.IsNullOrEmpty(trackPath) || bytes == null || bytes.Length == 0)
        {
            return null;
        }

        if (!IsEnabled)
        {
            ClearMemoryCache();
            return CreateImageFromBytes(bytes, decodePixelWidth);
        }

        string key = GetCacheKeyFromPath(trackPath);

        lock (_syncRoot)
        {
            if (_memoryCache.TryGetValue(key, out var entry))
            {
                Touch(key);
                return entry.Image;
            }
        }

        string cacheDir = GetCacheDirectory();
        string cacheFilePath = Path.Combine(cacheDir, $"{key}.bin");
        BitmapImage? image = null;

        if (File.Exists(cacheFilePath))
        {
            image = LoadImageFromFile(cacheFilePath, decodePixelWidth);
        }

        if (image == null)
        {
            image = CreateImageFromBytes(bytes, decodePixelWidth);
            if (image != null)
            {
                SaveImageToFile(bytes, cacheDir, cacheFilePath);
            }
        }

        if (image != null)
        {
            StoreInMemory(key, image);
        }

        return image;
    }

    private static BitmapImage? CreateImageFromBytes(byte[] bytes, int decodePixelWidth)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodePixelWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? LoadImageFromFile(string filePath, int decodePixelWidth)
    {
        try
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            using var stream = new MemoryStream(fileBytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodePixelWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveImageToFile(byte[] bytes, string cacheDir, string filePath)
    {
        try
        {
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            if (File.Exists(filePath)) return;
            File.WriteAllBytes(filePath, bytes);
        }
        catch
        {
            //I
        }
    }

    private static string GetCacheKeyFromPath(string path)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes);
    }

    private static void StoreInMemory(string key, BitmapImage image)
    {
        lock (_syncRoot)
        {
            long estimatedImageSize = image.PixelWidth * image.PixelHeight * 4;
            if (estimatedImageSize <= 0) estimatedImageSize = 200 * 200 * 4;

            while ((_memoryCache.Count >= MaxMemoryEntries || _currentMemorySize + estimatedImageSize > MaxMemorySizeBytes)
                   && _accessOrder.Count > 0)
            {
                string oldestKey = _accessOrder.First?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(oldestKey) && _memoryCache.TryGetValue(oldestKey, out var oldEntry))
                {
                    _memoryCache.Remove(oldestKey);
                    _accessOrder.RemoveFirst();

                    long oldSize = oldEntry.Image.PixelWidth * oldEntry.Image.PixelHeight * 4;
                    if (oldSize <= 0) oldSize = 200 * 200 * 4;
                    _currentMemorySize -= oldSize;
                }
            }

            if (_memoryCache.ContainsKey(key))
            {
                _memoryCache[key] = new CacheEntry(image);
                Touch(key);
                return;
            }

            _memoryCache[key] = new CacheEntry(image);
            _accessOrder.AddLast(key);
            _currentMemorySize += estimatedImageSize;
        }
    }

    private static void Touch(string key)
    {
        _accessOrder.Remove(key);
        _accessOrder.AddLast(key);
    }

    public static void ForceGarbageCollection()
    {
        lock (_syncRoot)
        {
            _memoryCache.Clear();
            _accessOrder.Clear();
            _currentMemorySize = 0;
        }
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        GC.WaitForPendingFinalizers();
    }

    public static void ClearMemoryCache()
    {
        lock (_syncRoot)
        {
            _memoryCache.Clear();
            _accessOrder.Clear();
            _currentMemorySize = 0;
        }
    }

    private sealed class CacheEntry(BitmapImage image)
    {
        public BitmapImage Image { get; } = image;
    }
}