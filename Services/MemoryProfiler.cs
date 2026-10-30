#if DEBUG
using System.Diagnostics;
using QAMP.Models;

namespace QAMP.Services;

public static class MemoryProfiler
{
    public static int GetSizeCoverTrack(Track? track)
    {
        if (track == null)
            return 0;

        int size = 0;

        if (track.CoverImage != null)
            size += track.CoverImage.Length;

        return size;
    }
    public static long GetMemoryUsage()
    {
        using var process = Process.GetCurrentProcess();
        long memoryMb = process.WorkingSet64 / (1024 * 1024);
        return memoryMb;
    }
}
#endif