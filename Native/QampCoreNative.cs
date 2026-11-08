using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QAMP.Native;

public static partial class QampCoreNative
{
    private const string DllName = "QampCore.dll";

    public static bool CalculateSpectrumFromFFT(
        ReadOnlySpan<float> fftData,
        Span<float> mainBuffer,
        Span<float> peakBuffer,
        int bandsCount
    )
    {
        return CalculateSpectrumFromFFTNative(fftData, mainBuffer, peakBuffer, bandsCount);
    }

    [LibraryImport(DllName, EntryPoint = "CalculateSpectrumFromFFT")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool CalculateSpectrumFromFFTNative(
        ReadOnlySpan<float> fftData,
        Span<float> mainBuffer,
        Span<float> peakBuffer,
        int bandsCount
    );

    [LibraryImport(DllName, EntryPoint = "ResetCorePeaks")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ResetCorePeaks();
}