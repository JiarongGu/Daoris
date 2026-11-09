using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Lazy fetch off for every read Daoris asks in a repository with a promisor, where the git in use is proved to honour
/// <c>GIT_NO_LAZY_FETCH</c>: then a filtered object answers missing inside an ordinary place, and the caller may ask once more as a
/// start allowed to fetch (<see cref="Fetching"/>). A read the git in use cannot be told so is placed as waiting outside Daoris from
/// its start (<see cref="Fetches"/>), since it may fetch inside its run.
/// </summary>
/// <remarks>
/// No version says it: git's <c>promisor-remote.c</c> reads the variable in 2.39.4, 2.44.1 and 2.45.1 but not in 2.44.0 or 2.45.0. So
/// it is proved once per git in use (<see cref="GitRuns.GitInUse"/>: its file and its write time), in the first repository with a
/// promisor a read is asked in, by two starts with every transport refused (so neither reaches a network) and git's trace on: asked an
/// object no repository holds, the git without the variable must run its fetch, and the same git told the variable must not. A git
/// whose plain run shows no fetch proves nothing and is not trusted with it.
/// </remarks>
internal static class GitLazyFetch
{
    internal const string Variable = "GIT_NO_LAZY_FETCH";

    private static readonly ConcurrentDictionary<string, Lazy<Task<bool>>> s_proved = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, (long Written, string? Text)> s_configs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly AsyncLocal<bool> s_fetching = new();
    private static readonly AsyncLocal<bool> s_probing = new();

    /// <summary>
    /// The test seam for the proof: where set, it answers for the git in use instead of the two starts, so an unsupported build's path
    /// is one fast case. Null in production, and an async local.
    /// </summary>
    internal static readonly AsyncLocal<Func<string, Task<bool>>?> Proof = new();

    /// <summary>Until disposed, this flow's reads may fetch: the one remote start of a read that answered missing.</summary>
    internal static IDisposable Fetching()
    {
        var before = s_fetching.Value;
        s_fetching.Value = true;
        return new Back(() => s_fetching.Value = before);
    }

    /// <summary>
    /// A read's start made ready before it queues: in a repository with a promisor, told the variable where the git in use honours it.
    /// Nothing is read or started for a repository without one.
    /// </summary>
    internal static async Task PrepareAsync(ProcessStartInfo info, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        if (s_fetching.Value || s_probing.Value || info.Environment.ContainsKey(Variable)) return;
        if (GitCommands.Writes(arguments) || GitStartAsks.WaitOf(arguments, writes: false) != GitWait.Read) return;
        var folder = Path.GetFullPath(string.IsNullOrEmpty(info.WorkingDirectory) ? "." : info.WorkingDirectory);
        if (!Promisor(folder)) return;
        if (await HonouredAsync(info.FileName, folder, ct).ConfigureAwait(false)) info.Environment[Variable] = "1";
    }

    /// <summary>Whether a start may fetch inside its run: one in a repository with a promisor that is not told the variable.</summary>
    internal static bool Fetches(ProcessStartInfo info, string folder) =>
        !(info.Environment.TryGetValue(Variable, out var off) && off == "1") && Promisor(folder);

    /// <summary>Whether the repository <paramref name="folder"/> is a folder of names a promisor remote (a partial clone's).</summary>
    internal static bool Promisor(string folder) => ConfigOf(folder) is { } text && PromisorLine().IsMatch(text);

    /// <summary>
    /// Whether the two traces prove the variable honoured: the plain run started git's fetch, and the run told the variable did not.
    /// </summary>
    internal static bool Proves(string plain, string told) => RanFetch(plain) && !RanFetch(told);

    private static bool RanFetch(string trace) =>
        trace.Split('\n').Any(line => line.Contains("run_command:", StringComparison.Ordinal) && FetchWord().IsMatch(line));

    /// <summary>Whether the git in use honours the variable, proved in <paramref name="folder"/> (a repository with a promisor) once per git.</summary>
    internal static Task<bool> HonouredAsync(string file, string folder, CancellationToken ct)
    {
        var key = GitRuns.GitInUse(ProgramHome.Named) ?? file;
        if (Proof.Value is { } stand) return stand(key);
        var proof = s_proved.GetOrAdd(key, _ => new Lazy<Task<bool>>(() => ProveAsync(folder)));
        return proof.Value.WaitAsync(ct);
    }

    private static async Task<bool> ProveAsync(string folder)
    {
        s_probing.Value = true;
        var absent = Absent(folder);
        // Every transport refused on the command line, which every configuration file yields to, so a fetch fails before it connects.
        string[] refused = ["-c", "protocol.allow=never", .. new[] { "file", "git", "ssh", "http", "https", "ext" }.SelectMany(name => new[] { "-c", $"protocol.{name}.allow=never" })];
        string[] arguments = [.. refused, "cat-file", "-t", absent];
        var bound = TimeSpan.FromSeconds(30);
        var (_, _, plain) = await GitRuns.GitAsync(folder, arguments, new Dictionary<string, string> { ["GIT_TRACE"] = "1" }, bound, CancellationToken.None).ConfigureAwait(false);
        var (_, _, told) = await GitRuns.GitAsync(folder, arguments, new Dictionary<string, string> { ["GIT_TRACE"] = "1", [Variable] = "1" }, bound, CancellationToken.None).ConfigureAwait(false);
        return Proves(plain, told);
    }

    // An id of the repository's format that names nothing.
    private static string Absent(string folder)
    {
        var sha256 = ConfigOf(folder) is { } text && Sha256Line().IsMatch(text);
        return string.Concat(Enumerable.Repeat("da0f", sha256 ? 16 : 10));
    }

    // The repository's shared configuration, read again only once its file is written.
    private static string? ConfigOf(string folder)
    {
        if (RefFiles.CommonDirOf(folder) is not { } common) return null;
        var file = new FileInfo(Path.Combine(common, "config"));
        try
        {
            if (!file.Exists) return null;
            var written = file.LastWriteTimeUtc.Ticks;
            if (s_configs.TryGetValue(file.FullName, out var held) && held.Written == written) return held.Text;
            var text = File.ReadAllText(file.FullName);
            s_configs[file.FullName] = (written, text);
            return text;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static readonly Regex s_promisor = new(@"^\s*(partialclone\s*=|promisor\s*=\s*true\b)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex s_sha256 = new(@"^\s*objectformat\s*=\s*sha256\b", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex s_fetch = new(@"\bfetch\b", RegexOptions.CultureInvariant);

    private static Regex PromisorLine() => s_promisor;

    private static Regex Sha256Line() => s_sha256;

    private static Regex FetchWord() => s_fetch;

    private sealed class Back(Action back) : IDisposable
    {
        public void Dispose() => back();
    }
}
