using Microsoft.Win32;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A fact that runs only on a machine with an HEVC decoder (D234). The engine plays HEVC through the
/// machine's own decoder and carries none, so a machine without one cannot show it: the fact is skipped
/// with that reason and never passes. The decoder is read as Windows registers it, a Media Foundation
/// transform that takes the HEVC subtype.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HevcDecoderFactAttribute : FactAttribute
{
    private static readonly byte[][] Subtypes =
    [
        [0x48, 0x45, 0x56, 0x43, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71],
        [0x31, 0x43, 0x56, 0x48, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71],
    ];

    public HevcDecoderFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "the HEVC half needs the machine's decoder, and this is not Windows";
        else if (!HasDecoder()) Skip = "this machine has no HEVC decoder registered with Media Foundation, and the engine carries none of its own";
    }

    private static bool HasDecoder()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var transforms = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\MediaFoundation\Transforms");
        if (transforms is null) return false;
        foreach (var name in transforms.GetSubKeyNames())
        {
            using var transform = transforms.OpenSubKey(name);
            if (transform?.GetValue("InputTypes") is not byte[] types) continue;
            if (Subtypes.Any(subtype => Contains(types, subtype))) return true;
        }

        return false;
    }

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
