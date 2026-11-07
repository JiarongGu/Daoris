using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The newest build that has run on a home: <c>data/last-build.json</c>, <c>{ "version": "…" }</c>, kept by the
/// application once its start is settled and read by the launcher, which refuses to start an older build. A home keeps no reader of an
/// older build's records, so an older build is never started on one a newer build has written.
/// </summary>
/// <remarks>
/// <para><b>Kept once the start is settled</b> (<c>StartConfirmation</c>): a build the launcher can still roll back has not
/// written the home as its own, and its build before it must still start. It is never lowered.</para>
/// <para><b>A file that is absent or does not read refuses nothing</b>: the launcher's last act is starting Daoris, and a home
/// it cannot read is no reason to keep the person out.</para>
/// <para>The base library alone: the launcher compiles it in.</para>
/// </remarks>
public static class HomeLastBuild
{
    /// <summary>The newest version that has run on <paramref name="home"/>; null where none is kept or it does not read.</summary>
    public static string? Read(string home)
    {
        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.LastBuild);
            return File.Exists(path) && JsonNode.Parse(AtomicFile.ReadText(path)) is JsonObject root && root["version"] is JsonValue value
                && value.TryGetValue<string>(out var version) && Numbered(version) is not null
                ? version
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return null;
        }
    }

    /// <summary>Keep <paramref name="version"/> as the home's newest where it is newer than the one kept, or none is.</summary>
    public static void Keep(string home, string? version)
    {
        if (Numbered(version) is not { } running) return;
        if (Numbered(Read(home)) is { } kept && kept >= running) return;
        var path = DaorisHome.Entry(home, HomeEntry.LastBuild);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, new JsonObject { ["version"] = version }.ToJsonString() + "\n");
    }

    /// <summary>The sentence the launcher refuses <paramref name="starting"/> with on <paramref name="home"/>, or null where it may start.</summary>
    public static string? Refusal(string home, string? starting)
    {
        if (Read(home) is not { } last || Numbered(starting) is not { } older || Numbered(last) is not { } newer || older >= newer) return null;
        return $"This Daoris is version {starting}, and its home was last run by version {last}, which an older build cannot read. "
            + "Install that version or a newer one into this folder, then start Daoris again.";
    }

    /// <summary>A version's numbered core, a prerelease and a build's metadata left off; null where it does not read.</summary>
    public static Version? Numbered(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var core = version.Trim().TrimStart('v').Split('-', '+')[0];
        return Version.TryParse(core, out var parsed) ? parsed : null;
    }
}
