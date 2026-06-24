using System.Runtime.InteropServices;

namespace QAMP.Native;
public static class QampCoreNative
{
    private const string DllName = "QampCore.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetCoreVersion();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool GetSpectrumDataAdvanced(int channel, float[] mainBuffer, float[] peakBuffer, int bandsCount);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ResetCorePeaks();
}