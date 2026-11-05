using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A fact that runs only on a machine with an HEVC decoder (D234). The engine plays HEVC through the machine's own
/// decoder and carries none, so a machine without one cannot show it: the fact is skipped with that reason and never
/// passes. The decoder is the one Chromium uses on Windows, the graphics adapter's Direct3D 11 video decoder with
/// HEVC's Main profile, never a Media Foundation transform in the registry: a machine with none of those played the
/// clip on its adapter's decoder, while the fact skipped it.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HevcDecoderFactAttribute : FactAttribute
{
    private static readonly Guid HevcMain = new("5b11d51b-2f4c-4452-bcc3-09f2a1160cc0");
    private const int HardwareDriver = 1;
    private const uint VideoSupport = 0x800;
    private const uint SdkVersion = 7;

    public HevcDecoderFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "the HEVC half needs the machine's decoder, and this is not Windows";
        else if (!HasDecoder()) Skip = "this machine's graphics adapter offers no Direct3D 11 HEVC Main decoder, and the engine carries none of its own";
    }

    [SupportedOSPlatform("windows")]
    private static bool HasDecoder()
    {
        try
        {
            if (D3D11CreateDevice(IntPtr.Zero, HardwareDriver, IntPtr.Zero, VideoSupport, IntPtr.Zero, 0, SdkVersion, out var device, out _, out var context) < 0)
            {
                return false;
            }

            var wrapped = Marshal.GetObjectForIUnknown(device);
            try
            {
                if (wrapped is not ID3D11VideoDevice video) return false;
                for (uint i = 0, count = video.GetVideoDecoderProfileCount(); i < count; i++)
                {
                    if (video.GetVideoDecoderProfile(i, out var profile) >= 0 && profile == HevcMain) return true;
                }

                return false;
            }
            finally
            {
                Marshal.ReleaseComObject(wrapped);
                Marshal.Release(context);
                Marshal.Release(device);
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or COMException or InvalidCastException)
        {
            // No Direct3D 11 here, such as a server without its graphics: no decoder to play on.
            return false;
        }
    }

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr featureLevels, uint featureLevelCount,
        uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);

    /// <summary>The video half of a Direct3D 11 device, declared up to the two calls read here: the slots before them keep its order.</summary>
    [ComImport, Guid("10EC4D5B-975A-4689-B9E4-D0AAC30FE333"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D11VideoDevice
    {
        void CreateVideoDecoder();
        void CreateVideoProcessor();
        void CreateAuthenticatedChannel();
        void CreateCryptoSession();
        void CreateVideoDecoderOutputView();
        void CreateVideoProcessorInputView();
        void CreateVideoProcessorOutputView();
        void CreateVideoProcessorEnumerator();

        [PreserveSig]
        uint GetVideoDecoderProfileCount();

        [PreserveSig]
        int GetVideoDecoderProfile(uint index, out Guid profile);
    }
}
