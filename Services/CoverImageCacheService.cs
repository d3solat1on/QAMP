using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using QAMP.Models;

namespace QAMP.Services;

public static class CoverImageCacheService
{
    private static readonly Lock _syncRoot = new();
    private static readonly Dictionary<string, CacheEntry> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> _accessOrder = new();
    private const int MaxMemoryEntries = 32;
    private const long MaxMemorySizeBytes = 100_000_000;
    private static long _currentMemorySize = 0;

    private static readonly string _cacheDirectory = Path.Combine(
        AppDataManager.AppDataPath,
        "cover_cache");

    public static bool IsEnabled
    {
        get
        {
            try
            {
                return SettingsManager.Instance.Config.EnableCoverCache;
            }
            catch
            {
                return true;
            }
        }
    }

    static CoverImageCacheService()
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
        catch
        {
            // I
        }
    }

    public static BitmapImage? GetImage(byte[]? bytes, int decodePixelWidth)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        if (!IsEnabled)
        {
            ClearMemoryCache();
            return CreateImageFromBytes(bytes, decodePixelWidth);
        }

        string key = GetCacheKey(bytes);
        lock (_syncRoot)
        {
            if (_memoryCache.TryGetValue(key, out var entry))
            {
                Touch(key);
                return entry.Image;
            }
        }

        string cacheFilePath = GetCacheFilePath(key);
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
                SaveImageToFile(bytes, cacheFilePath);
            }
        }

        if (image != null)
        {
            StoreInMemory(key, image);
        }

        return image;
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
            using var stream = File.OpenRead(filePath);
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

    private static void SaveImageToFile(byte[] bytes, string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                return;
            }

            using var stream = File.Open(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // ignore
        }
    }

    private static string GetCacheKey(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string GetCacheFilePath(string key)
    {
        return Path.Combine(_cacheDirectory, $"{key}.bin");
    }

    private static void StoreInMemory(string key, BitmapImage image)
    {
        lock (_syncRoot)
        {
            long estimatedImageSize = 800 * 800 * 4;

            while ((_memoryCache.Count >= MaxMemoryEntries || _currentMemorySize + estimatedImageSize > MaxMemorySizeBytes)
                   && _accessOrder.Count > 0)
            {
                string oldestKey = _accessOrder.First?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(oldestKey) && _memoryCache.TryGetValue(oldestKey, out var oldEntry))
                {
                    _memoryCache.Remove(oldestKey);
                    _accessOrder.RemoveFirst();
                    _currentMemorySize -= estimatedImageSize;
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

    private sealed class CacheEntry(BitmapImage image)
    {
        public BitmapImage Image { get; } = image;
    }
}