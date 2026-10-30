using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using QAMP.Models;

namespace QAMP.Services;

public static class MemoryOptimizer
{
    private static int _isOptimizing;

    public static void RunAsync(Dispatcher dispatcher)
    {
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (Interlocked.Exchange(ref _isOptimizing, 1) != 0) return;

            _ = Task.Run(() =>
            {
                try
                {
                    GC.Collect(1, GCCollectionMode.Optimized);
                    GC.WaitForPendingFinalizers();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MemoryOptimizer Error: {ex.Message}");
                }
                finally
                {
                    Volatile.Write(ref _isOptimizing, 0);
                }
            });
        }), DispatcherPriority.ApplicationIdle);
    }
#if DEBUG
    public static void FreeRam()
    {

        CoverImageCacheService.ClearMemoryCache();
        LyricsCache.Clear();

        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        try
        {
            using var process = Process.GetCurrentProcess();
            EmptyWorkingSet(process.Handle);
        }
        catch (Exception ex)
        {
            AppConsole.Log(ex.Message);
        }

    }

    [System.Runtime.InteropServices.DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
#endif
}