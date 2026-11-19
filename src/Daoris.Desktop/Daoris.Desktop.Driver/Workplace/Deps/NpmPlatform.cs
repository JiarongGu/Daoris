using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Whether npm leaves a lockfile's package out on this machine: its <c>os</c> excludes this system as npm's own platform check reads
/// the list (a name where it installs, a <c>!name</c> where it does not). A package left out is never fetched, so its install script
/// never runs here. Only the system is read: the processor npm sees is node's, which can differ from this process's.
/// </summary>
internal static class NpmPlatform
{
    /// <summary>This system as node's <c>process.platform</c> names it.</summary>
    internal static string Os { get; } =
        OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsFreeBSD() ? "freebsd" : "unknown";

    /// <summary>Whether <paramref name="package"/>, a lockfile's <c>packages</c> entry, is one npm installs nowhere on this system.</summary>
    internal static bool LeavesOut(JsonElement package)
    {
        if (package.ValueKind != JsonValueKind.Object || !package.TryGetProperty("os", out var listed) || listed.ValueKind != JsonValueKind.Array) return false;
        var names = listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!).ToList();
        if (names is ["any"]) return false;
        if (names.Contains("!" + Os, StringComparer.Ordinal)) return true;
        var only = names.Where(name => !name.StartsWith('!')).ToList();
        return only.Count > 0 && !only.Contains(Os, StringComparer.Ordinal);
    }
}
