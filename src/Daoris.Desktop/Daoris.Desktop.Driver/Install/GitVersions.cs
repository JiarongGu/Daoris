using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The git Daoris runs, by its version (TOOLS6 inside GITAUTH1c; D121 §2.5, the git-auth design §4.1), asked of the resolved file once
/// per change of it and judged against two floors. Below <see cref="Doors"/> git reads no command scope (<c>GIT_CONFIG_COUNT</c>), so
/// Daoris's doors cannot stand and its own remote acts refuse; below <see cref="Setting"/> git reads no <c>GIT_CONFIG_GLOBAL</c>, so a
/// git setting refuses and <see cref="GitGlobalFile"/> is not handed.
/// </summary>
/// <remarks>
/// Kept in this process alone, by the file's path, write time and length: a change of way names another file, and an update rewrites
/// the same one, so either is asked again; a start that only reads what is known (<see cref="Known"/>) never starts git.
/// </remarks>
public static class GitVersions
{
    /// <summary>The oldest git Daoris's doors stand in front of.</summary>
    public static readonly Version Doors = new(2, 31);

    /// <summary>The oldest git a git setting, and the file carrying it, applies to.</summary>
    public static readonly Version Setting = new(2, 32);

    private static readonly ConcurrentDictionary<string, (DateTime Written, long Length, Version Version)> s_asked =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The version git said, as its first dotted number (<c>git version 2.53.0.windows.1</c> is 2.53.0); null where it said none.</summary>
    public static Version? Parse(string? said) =>
        said is not null && ToolQuestions.VersionIn(said) is { } dotted && Version.TryParse(dotted, out var version) ? version : null;

    /// <summary>The version kept for <paramref name="file"/> as it stands now; null where it was never asked, or has changed since.</summary>
    public static Version? Known(string? file) =>
        file is not null && Stamp(file) is { } now && s_asked.TryGetValue(Key(file), out var kept) && (kept.Written, kept.Length) == now
            ? kept.Version
            : null;

    /// <summary>
    /// <paramref name="file"/>'s version: the one kept for it as it stands, else asked by <paramref name="ask"/> and kept. An answer with
    /// no version is not kept, so the next act asks again; a file that is not there is null, unasked.
    /// </summary>
    public static async Task<Version?> OfAsync(string? file, Func<string, CancellationToken, Task<string?>> ask, CancellationToken ct)
    {
        if (file is null || Stamp(file) is not { } stamp) return null;
        if (Known(file) is { } known) return known;
        var version = Parse(await ask(file, ct).ConfigureAwait(false));
        if (version is not null) s_asked[Key(file)] = (stamp.Written, stamp.Length, version);
        return version;
    }

    /// <summary>The version of the git <paramref name="home"/>'s Tools resolves, asked as each tool's version is (<see cref="ToolQuestions"/>).</summary>
    public static Task<Version?> OfAsync(string home, CancellationToken ct) =>
        OfAsync(Tools.Resolve(home, "git").File, AskAsync, ct);

    /// <summary>A git's answer to <c>--version</c>, both streams; null where it did not answer.</summary>
    public static async Task<string?> AskAsync(string file, CancellationToken ct) =>
        await ToolQuestions.AskAsync(file, ["--version"], ct).ConfigureAwait(false) is { Started: true, Problem: null } answer ? answer.Output : null;

    /// <summary>Why Daoris's own remote acts refuse on this git (§4.1), or null where it is new enough or not known.</summary>
    public static string? DoorsRefusal(Version? version) =>
        version is not null && version < Doors
            ? $"the git Daoris runs is {version}, older than the {Doors} Daoris's git doors need, so Daoris does not reach a remote with "
              + "it: Settings → Tools sets a newer git, or `daoris tool use git managed` downloads one"
            : null;

    /// <summary>Why a git setting refuses on this git (D121 §2.5), or null where it is new enough or not known.</summary>
    public static string? SettingRefusal(Version? version) =>
        version is not null && version < Setting
            ? $"the git Daoris runs is {version}, and a git setting needs git {Setting} or newer: an older git does not read the file "
              + "Daoris writes its settings in, so the setting would silently not apply. Settings → Tools sets a newer git"
            : null;

    private static string Key(string file) => Path.GetFullPath(file);

    private static (DateTime Written, long Length)? Stamp(string file)
    {
        try
        {
            var info = new FileInfo(file);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
