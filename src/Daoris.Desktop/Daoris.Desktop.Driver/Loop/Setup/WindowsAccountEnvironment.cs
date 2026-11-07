using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Daoris.Driver;

/// <summary>
/// The Windows account's variables, kept in the registry's <c>HKCU\Environment</c>: never the machine's.
/// A value is read and written unexpanded, since the account's <c>PATH</c> holds <c>%USERPROFILE%</c> references that an
/// expanded write would lose, and a <c>PATH</c> is stored as an expandable string as Windows stores it.
/// </summary>
public sealed class WindowsAccountEnvironment : IAccountEnvironment
{
    private const string Key = "Environment";
    private const uint SettingChange = 0x001A;
    private const uint AbortIfHung = 0x0002;

    /// <summary>Whether this machine keeps an account's variables here at all.</summary>
    public static bool Exists => OperatingSystem.IsWindows();

    public string? Read(string name)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("the account's variables are kept on Windows");
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    public void Write(string name, string? value)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("the account's variables are kept on Windows");
        using var key = Registry.CurrentUser.OpenSubKey(Key, writable: true)
            ?? throw new InvalidOperationException("the account's environment could not be opened for writing");
        if (value is null) key.DeleteValue(name, throwOnMissingValue: false);
        else key.SetValue(name, value, string.Equals(name, "PATH", StringComparison.OrdinalIgnoreCase) ? RegistryValueKind.ExpandString : RegistryValueKind.String);
        // Programs that listen (Explorer, so a new terminal) re-read the account's environment; a hung one is not waited on.
        SendMessageTimeout(new IntPtr(0xFFFF), SettingChange, IntPtr.Zero, Key, AbortIfHung, 2_000, out _);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wide, string setting, uint flags, uint timeout, out IntPtr result);
}
