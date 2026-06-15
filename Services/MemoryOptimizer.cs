using System.Diagnostics;
using System.Windows.Threading;

namespace QAMP.Services;

public static class MemoryOptimizer
{
    private static bool _isOptimizing = false;

    public static void RunAsync(Dispatcher dispatcher)
    {
        if (_isOptimizing) return;
        _isOptimizing = true;

        dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                GC.Collect(1, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();

                await Task.Run(() =>
                {
                    try
                    {
                        using var process = Process.GetCurrentProcess();
                        process.MaxWorkingSet = process.MinWorkingSet;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DEBUG]: CHTO-TO POSHLO NE TAK: {ex.Message}");
                    }
                });
            }
            finally
            {
                _isOptimizing = false;
            }
        }), DispatcherPriority.ApplicationIdle);
    }
}