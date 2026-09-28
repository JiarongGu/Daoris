using System.Globalization;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// The person's Edge as Daoris's browser (BRW12, D84): Edge on a profile under the home, started with a
/// debug port the plugins' servers attach to, as `${browser}`. Pure, so where it is looked for, what it
/// is started with and how a running one is found again are tested without an Edge.
/// </summary>
/// <remarks>
/// <para><b>A profile of Daoris's, never the person's default.</b> Chromium refuses a debug port on
/// the default profile, so the person's everyday Edge cannot be driven at all. Edge signs a profile
/// in to the Windows account's Microsoft account on its own, which is what this option is for, and
/// its sync is the person's to turn on (`docs/2026-09-28-managed-edge-evidence.md` §5).</para>
///
/// <para><b>Found again, not started twice.</b> A second Edge started on the same profile hands over
/// to the one already running and exits, and that one listens on the port it was started with. So the
/// port is recorded beside the profile, and a shell that restarts adopts the Edge it finds there.</para>
/// </remarks>
public static class EdgeBrowser
{
    public static string ProfileFolder(string home) => Path.Combine(home, "browser", "edge");

    /// <summary>What Daoris started its Edge with, beside the profile.</summary>
    public static string RecordPath(string home) => Path.Combine(home, "browser", "edge.json");

    /// <summary>Where Edge is installed: for the machine (both program folders), then for one person.</summary>
    public static IReadOnlyList<string> Candidates(Func<string, string?> environment)
    {
        var candidates = new List<string>();
        foreach (var folder in new[] { "ProgramFiles(x86)", "ProgramFiles", "LOCALAPPDATA" })
        {
            if (environment(folder) is { Length: > 0 } root)
            {
                candidates.Add(Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe"));
            }
        }

        return candidates;
    }

    public static string? Locate(Func<string, string?> environment, Func<string, bool> exists) =>
        Candidates(environment).FirstOrDefault(exists);

    public static string? Locate() => Locate(Environment.GetEnvironmentVariable, File.Exists);

    /// <summary>
    /// What Edge is started with: the profile under the home, the debug port, and none of its first-run
    /// pages. Sync is not turned off: whether Edge syncs is the person's choice inside Edge.
    /// </summary>
    public static IReadOnlyList<string> Arguments(string profile, int port) =>
    [
        $"--user-data-dir={profile}",
        $"--remote-debugging-port={port.ToString(CultureInfo.InvariantCulture)}",
        "--no-first-run",
        "--no-default-browser-check",
        "edge://newtab/",
    ];

    /// <summary>The port a recorded Edge was started with, or null when the record says none.</summary>
    public static int? RecordedPort(string? json)
    {
        try
        {
            if (json is not null
                && JsonDocument.Parse(json).RootElement is { ValueKind: JsonValueKind.Object } record
                && record.TryGetProperty("port", out var port)
                && port.ValueKind == JsonValueKind.Number && port.TryGetInt32(out var value)
                && value is >= 1024 and <= 65535)
            {
                return value;
            }
        }
        catch (JsonException)
        {
            // A record Daoris cannot read records nothing.
        }

        return null;
    }

    public static string Record(int port) => JsonSerializer.Serialize(new { port }) + "\n";

    /// <summary>Whether a debug endpoint's own account of itself is an Edge: its `Browser` names `Edg/`.</summary>
    public static bool IsEdge(string versionJson)
    {
        try
        {
            return JsonDocument.Parse(versionJson).RootElement.TryGetProperty("Browser", out var browser)
                   && browser.GetString() is { } name && name.StartsWith("Edg/", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
