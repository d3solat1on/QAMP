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
        var fftArray = fftData.ToArray();
        var mainArray = new float[mainBuffer.Length];
        var peakArray = new float[peakBuffer.Length];
        var result = CalculateSpectrumFromFFTNative(fftArray, mainArray, peakArray, bandsCount);
        mainArray.AsSpan().CopyTo(mainBuffer);
        peakArray.AsSpan().CopyTo(peakBuffer);
        return result;
    }

    [LibraryImport(DllName, EntryPoint = "CalculateSpectrumFromFFT")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool CalculateSpectrumFromFFTNative(
        [In] float[] fftData,
        [Out] float[] mainBuffer,
        [Out] float[] peakBuffer,
        int bandsCount
    );

    [LibraryImport(DllName, EntryPoint = "ResetCorePeaks")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial void ResetCorePeaks();
}
