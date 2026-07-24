using System.Diagnostics;
using QAMP.Models;

namespace QAMP.Services;

public static class MemoryProfiler
{
    public static string GetMemoryReport()
    {
        // хз хуйня какая-то
        using Process currentProcess = Process.GetCurrentProcess();

        long workingSet = currentProcess.WorkingSet64;

        long managedMemory = GC.GetTotalMemory(forceFullCollection: true);

        long unmanagedMemory = workingSet - managedMemory;

        double workingSetMB = workingSet / 1024.0 / 1024.0;
        double managedMB = managedMemory / 1024.0 / 1024.0;
        double unmanagedMB = unmanagedMemory / 1024.0 / 1024.0;

        return $"Память QAMP\n" +
               $"Физическая память: {workingSetMB:F2} MB\n" +
               $"Данные плеера: {managedMB:F2} MB\n" +
               $"Графика, аудио-движок: {unmanagedMB:F2} MB\n";
               
    }
    public static int GetSizeCoverTrack(Track? track)
    {
        if (track == null)
            return 0;

        int size = 0;

        if (track.CoverImage != null)
            size += track.CoverImage.Length;

        return size;
    }
}