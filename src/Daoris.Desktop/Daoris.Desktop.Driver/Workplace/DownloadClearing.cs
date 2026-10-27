using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One version an agent or a tool downloaded into the Daoris folder's <c>cache/</c> (CACHECLEAR1), and what keeps it.
/// </summary>
/// <param name="Kind"><c>agent</c>, a pinned agent's version (<c>cache/toolchain/&lt;agent&gt;/&lt;version&gt;</c>), or <c>tool</c>, a
/// managed tool's (<c>cache/tools/&lt;tool&gt;/&lt;version&gt;</c>).</param>
/// <param name="Name">The agent or the tool, as its folder names it.</param>
/// <param name="Version">The version, as its folder names it.</param>
/// <param name="Path">Its folder.</param>
/// <param name="Bytes">Its size, as far as the walk got; none where the list did not measure it.</param>
/// <param name="Partial">Whether any read failed, so the size is not whole.</param>
/// <param name="Keeps">
/// Null where nothing uses it, else why it stays: <c>pinned</c> (an agent's pin names it), <c>used</c> (Tools runs it), <c>named</c> (a
/// setting names a file inside it), <c>unread</c> (a file that says what is used cannot be read, or the running sessions cannot be),
/// <c>recent</c> (downloaded within <see cref="DownloadClearing.Settling"/>, so a pin or a use may be on its way), <c>running</c> (a running
/// session was handed it, KEEP1b), or, at a removal, <c>in-use</c> (a program holds it).
/// </param>
/// <param name="By">What keeps it: <c>machine</c> or <c>workspace:&lt;name&gt;</c> for a pin, <c>session:&lt;id&gt;</c> for a running
/// session, <c>sessions</c> where they could not be read, else the file (<c>tools.json</c>, <c>driver.json</c>, <c>harnesses.json</c>).</param>
/// <param name="Unfinished">An earlier clearing moved it aside and could not remove all of it: nothing runs it, and it goes at the next press.</param>
public sealed record DownloadedVersion(
    string Kind, string Name, string Version, string Path, long Bytes, bool Partial, string? Keeps, IReadOnlyList<string> By,
    bool Unfinished = false);

/// <summary>Every downloaded version, and why none could be listed: <c>no-home</c>, or <c>twice</c> where both places of the layout hold one.</summary>
public sealed record DownloadListing(IReadOnlyList<DownloadedVersion> Versions, string? Why);

/// <summary>What a press asked to remove: a listed row's kind, name and version, and whether it was an unfinished one.</summary>
public sealed record DownloadAsked(string Kind, string Name, string Version, bool Unfinished = false);

/// <summary>
/// What one asked row came to: <c>removed</c>; <c>kept</c>, with the version's <see cref="DownloadedVersion.Keeps"/> as it was judged right
/// before it would have gone; <c>gone</c>, no longer there; or <c>unfinished</c>, moved aside so nothing runs it, and not all removed. A
/// version that went carries its size, measured once moved aside.
/// </summary>
public sealed record DownloadCleared(DownloadAsked Asked, string Outcome, DownloadedVersion? Version);

/// <summary>What says which downloaded versions are used, read at once: the agents' pins, the tools' ways and the commands named.</summary>
/// <param name="Harnesses"><c>harnesses.json</c>: the agents' pins, the machine's and each workspace's.</param>
/// <param name="Tools"><c>tools.json</c>: each tool's way, and a managed tool's version or a named file.</param>
/// <param name="Config"><c>driver.json</c>'s commands, which may name a file inside a version; null where it cannot be read.</param>
public sealed record DownloadUses(HarnessSettings Harnesses, ToolsRead Tools, DriverConfig? Config)
{
    /// <summary>The three files as they stand now, for <paramref name="home"/>.</summary>
    /// <param name="harnesses">The agents' wiring, as the roster reads it (<see cref="HarnessRoster.Settings"/>).</param>
    /// <param name="configPath">The person's choices, as the loop resolves them; null where none.</param>
    public static DownloadUses Read(string home, HarnessSettings harnesses, string? configPath)
    {
        DriverConfig? config;
        try
        {
            config = configPath is null ? DriverConfig.Empty : DriverConfig.Load(configPath);
        }
        catch (DriverConfigUnreadableException)
        {
            config = null;
        }

        return new DownloadUses(harnesses, global::Daoris.Driver.Tools.Read(home), config);
    }
}

/// <summary>
/// How the act judges each version right before it goes (KEEP1b, the cleanup design §3.5): the use files read as they stand, the keep locks
/// of those files held per version from the judgement to the rename, the running sessions, and the first-unused time of a version's current
/// stretch, which the start-time rule reads.
/// </summary>
/// <param name="Uses">The files that say what is used, read again for each version, under its locks where <see cref="Locked"/> names them.</param>
/// <param name="Now">What time it is.</param>
public sealed record DownloadJudge(Func<DownloadUses> Uses, Func<DateTimeOffset> Now)
{
    /// <summary>
    /// The use files whose keep locks are held, in this order, from a version's judgement to its rename, never across the removal that
    /// follows (Fable F2.2 to F2.4): the harness file the roster reads, <c>tools.json</c>, and <c>driver.json</c> at the loop's path.
    /// </summary>
    public IReadOnlyList<string> Locked { get; init; } = [];

    /// <summary>The running sessions, read right before each version's locks are taken.</summary>
    public Func<CancellationToken, Task<LiveSessions>> Sessions { get; init; } = _ => Task.FromResult(LiveSessions.None);

    /// <summary>When a version's current unused stretch began, by its key (<see cref="DownloadKeeping.Key(string, string, string)"/>); null where unknown, read as now.</summary>
    public Func<string, DateTimeOffset?> FirstUnused { get; init; } = _ => null;

    /// <summary>
    /// Whether a version downloaded within <see cref="DownloadClearing.Settling"/> stays (<c>recent</c>): a pin may be on its way. Tools'
    /// delete names its one version, so the person's word answers that.
    /// </summary>
    public bool KeepsRecent { get; init; } = true;

    /// <summary>A test's look under the locks, after the judgement and before the rename.</summary>
    internal Action<DownloadedVersion>? Judged { get; init; }

    /// <summary>A test's look once a version is moved aside and its locks are let go, before it is removed.</summary>
    internal Action<DownloadedVersion>? Renamed { get; init; }

    /// <summary>
    /// The judge the look's pass and the routes use: the harness file the roster reads (<paramref name="harnesses"/>, which
    /// <c>DAORIS_HARNESS_CONFIG</c> may name), <c>tools.json</c> and <paramref name="configPath"/>, each read as it stands and locked.
    /// </summary>
    public static DownloadJudge Of(
        string home, string harnesses, string? configPath, Func<CancellationToken, Task<LiveSessions>> sessions, Func<DateTimeOffset>? now = null)
    {
        string? tools;
        try
        {
            tools = DaorisHome.Entry(home, HomeEntry.Tools);
        }
        catch (HomeEntryTwiceException)
        {
            // Both places hold one: the tools' read says so, and every version is kept as unread.
            tools = null;
        }

        return new(() => DownloadUses.Read(home, HarnessSettings.Load(harnesses), configPath), now ?? (() => DateTimeOffset.UtcNow))
        {
            Locked = [.. new[] { harnesses, tools, configPath }.OfType<string>()
                .DistinctBy(path => Path.GetFullPath(path), OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)],
            Sessions = sessions,
        };
    }
}

/// <summary>
/// Clearing downloads (CACHECLEAR1): the versions the agents and the tools downloaded into <c>cache/</c> that nothing uses, LISTED
/// first with what keeps each of the rest, then removed by the person's press (D88's shape: listed, pressed, each judged again right
/// before it goes). The screen's half is Settings → This Daoris's <i>Downloads</i> row. Since KEEP1b it is the downloads' one act
/// (D195 point 6, the cleanup design §3.5, §6.6): the look's rule (<see cref="DownloadKeeping"/>) and Tools' delete of one version
/// (<see cref="ToolInstall.DeleteAsync"/>) remove through it too.
/// </summary>
/// <remarks>
/// <para><b>What uses a version is what would run it.</b> An agent's version is used where a pin names it, this machine's or any
/// workspace's (<see cref="HarnessSettings.Versions"/>, <see cref="HarnessSettings.WorkspaceVersions"/>), since a start resolves its
/// workspace's pin first; a tool's where Tools runs it managed at that version. A version a setting names a file inside (a tool's
/// <c>file</c>, a command in <c>driver.json</c>) is used too. A file that cannot be read keeps everything it would have said.</para>
///
/// <para><b>And what a running session was handed</b> (KEEP1b, Fable F2.5): a version holding a file a running session's start was handed
/// stays (<c>running</c>), both of an ACP session's; a record from before that field keeps the start-time rule, kept while a session that
/// started before the version's current unused stretch began runs. A pin and an unpin between two passes leave no trace in that
/// stretch (§2.3), which is why the session's own versions are the guard.</para>
///
/// <para><b>Judged under the use files' locks</b> (Fable F2.2 to F2.4): each version is judged holding the keep locks of the files that
/// say what uses it (<see cref="DownloadJudge.Locked"/>), from its judgement to its rename and never across its removal, so a pin written
/// meanwhile waits and is then seen, and a pin waiting while another version is deleted is taken at once. A pin's writer looks again
/// under its lock that its version is there (<see cref="HarnessSettings.Pin"/>).</para>
///
/// <para><b>A download a moment old stays.</b> A pin and a use are written after their download lands (<see cref="HarnessActions"/>,
/// <see cref="ToolInstall.DownloadAsync"/>), so a version downloaded within <see cref="Settling"/> is kept: it may be the one about to be
/// pinned. A staged download (<c>.part</c>, and a tool's <c>.part-&lt;pid&gt;</c>, PUSH1f2c) is never listed: a tool's folder lists
/// exact versions alone.</para>
///
/// <para><b>Moved aside, then removed.</b> A version goes by one rename beside itself first, which fails while a program runs from it
/// (Windows holds a running executable's folder), so a running session's old version is kept as <c>in-use</c> rather than half
/// deleted; but a rename's success proves nothing (Codex 2.6), an agent run through npm's shims holding no file open. Once moved, nothing
/// resolves it, and what a removal could not take is listed as unfinished and goes at the next press.</para>
///
/// <para><b>Only what was listed.</b> A press names the rows the page showed; a version downloaded since, or one not listed, is never
/// touched, and a name is matched to a listed folder, never joined into a path.</para>
/// </remarks>
public static class DownloadClearing
{
    /// <summary>How long a fresh download stays whatever uses it: a pin or a use is written once it lands.</summary>
    public static readonly TimeSpan Settling = TimeSpan.FromHours(1);

    /// <summary>A version moved aside by a press, beside where it was: <c>&lt;version&gt;.&lt;8 hex&gt;.clearing</c>.</summary>
    public const string Aside = ".clearing";

    /// <summary>A download under way, which both installers stage beside its folder.</summary>
    private const string Staged = ".part";

    private const int MoveTries = 5;

    private const int MoveWaitMs = 200;

    /// <summary>Every downloaded version under <paramref name="home"/>, agents' first then tools', each with what keeps it.</summary>
    /// <param name="measure">Whether each version's folder is walked for its size; the look's rule lists without (§3.1) and measures what it removes.</param>
    public static DownloadListing List(
        string? home, DownloadUses uses, DateTimeOffset now, CancellationToken cancellationToken = default, bool measure = true)
    {
        if (home is null) return new DownloadListing([], "no-home");
        string agents, tools;
        try
        {
            agents = DaorisHome.Entry(home, HomeEntry.Toolchain);
            tools = DaorisHome.Entry(home, HomeEntry.ToolVersions);
        }
        catch (HomeEntryTwiceException)
        {
            return new DownloadListing([], "twice");
        }

        var versions = new List<DownloadedVersion>();
        foreach (var agent in Folders(agents))
        {
            foreach (var version in Folders(agent.FullName)) Add("agent", agent.Name, version);
        }

        foreach (var tool in Tools.Declared)
        {
            foreach (var version in Folders(System.IO.Path.Combine(tools, tool.Id))) Add("tool", tool.Id, version);
        }

        return new DownloadListing(versions, null);

        void Add(string kind, string name, DirectoryInfo folder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (UnfinishedVersion(folder.Name) is { } left)
            {
                versions.Add(Measured(new DownloadedVersion(kind, name, left, folder.FullName, 0, false, null, [], Unfinished: true)));
                return;
            }

            if (folder.Name.StartsWith('.') || folder.Name.EndsWith(Staged, StringComparison.OrdinalIgnoreCase)) return;
            // A tool's folder holds its versions and nothing else this build names: one that is no exact version is not one.
            if (kind == "tool" && !Tools.IsExactVersion(folder.Name)) return;
            var (keeps, by) = Judge(kind, name, folder.Name, folder.FullName, uses, now);
            versions.Add(Measured(new DownloadedVersion(kind, name, folder.Name, folder.FullName, 0, false, keeps, by)));
        }

        DownloadedVersion Measured(DownloadedVersion row) => measure ? MeasuredAt(row, row.Path, cancellationToken) : row;
    }

    /// <summary>
    /// Remove the versions a press, the look's rule or Tools' delete names that nothing keeps, each judged again right before it goes, from
    /// the files as they stand, holding their locks (<paramref name="judge"/>).
    /// </summary>
    /// <param name="move">The rename aside; a test hands in one that refuses, as a running program makes Windows refuse.</param>
    /// <param name="door">Who removed it, for the machine log: <c>press</c>, <c>look</c> (the rule) or <c>tools</c> (Tools' delete).</param>
    /// <param name="spent">Whether the caller's budget is spent (the look's pass, §3.1): asked between versions, never inside one, and the
    /// versions not reached are not in the answer.</param>
    public static async Task<IReadOnlyList<DownloadCleared>> ClearAsync(
        string? home, IReadOnlyList<DownloadAsked> asked, DownloadJudge judge, MachineLog? log = null,
        Action<string, string>? move = null, CancellationToken cancellationToken = default, string? door = null, Func<bool>? spent = null)
    {
        log ??= MachineLog.None;
        var listed = List(home, judge.Uses(), judge.Now(), cancellationToken, measure: false).Versions;
        var cleared = new List<DownloadCleared>();
        foreach (var each in asked)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (spent?.Invoke() == true) break;
            var row = listed.FirstOrDefault(version => version.Kind == each.Kind && version.Name == each.Name
                && version.Version == each.Version && version.Unfinished == each.Unfinished);
            if (row is null || !Directory.Exists(row.Path))
            {
                cleared.Add(new DownloadCleared(each, "gone", null));
                continue;
            }

            var aside = row.Path;
            if (!row.Unfinished)
            {
                // Read before the locks, so a slow service never holds a person's pin at the lock (F2.3).
                var sessions = await judge.Sessions(cancellationToken).ConfigureAwait(false);
                var (moved, kept) = MoveAside(row, judge, sessions, move);
                if (moved is null && kept is null)
                {
                    // Taken by another remover while this one waited on the locks.
                    cleared.Add(new DownloadCleared(each, "gone", null));
                    continue;
                }

                if (kept is not null)
                {
                    cleared.Add(Kept(each, kept, log, door));
                    continue;
                }

                aside = moved!;
                judge.Renamed?.Invoke(row);
            }

            var measured = MeasuredAt(row, aside, cancellationToken);
            var removed = Remove(aside);
            log.Info(removed ? "download.cleared" : "download.unfinished", Said(row, door));
            cleared.Add(new DownloadCleared(each, removed ? "removed" : "unfinished", measured));
            if (removed) RemoveIfEmpty(Directory.GetParent(row.Path)?.FullName);
        }

        return cleared;
    }

    /// <summary>
    /// One version judged and moved aside, holding the use files' locks from the judgement to the rename and letting them go before anything
    /// is removed (F2.3): where it went, or the row as judged where it stays; neither where it was gone once the locks were held.
    /// </summary>
    private static (string? Aside, DownloadedVersion? Kept) MoveAside(
        DownloadedVersion row, DownloadJudge judge, LiveSessions sessions, Action<string, string>? move)
    {
        var held = new List<IDisposable>();
        try
        {
            foreach (var file in judge.Locked)
            {
                try
                {
                    held.Add(KeepLock.Take(file));
                }
                catch (IOException)
                {
                    // A writer held it for the whole of the wait: what that file says cannot be read now, so the version stays.
                    return (null, row with { Keeps = "unread", By = [System.IO.Path.GetFileName(file)] });
                }
            }

            if (!Directory.Exists(row.Path)) return (null, null);
            var (keeps, by) = Judge(
                row.Kind, row.Name, row.Version, row.Path, judge.Uses(), judge.Now(), sessions, judge.FirstUnused(DownloadKeeping.Key(row)),
                judge.KeepsRecent);
            judge.Judged?.Invoke(row);
            if (keeps is not null) return (null, row with { Keeps = keeps, By = by });

            var aside = $"{row.Path}.{Guid.NewGuid().ToString("N")[..8]}{Aside}";
            try
            {
                AtomicFile.MoveFolder(row.Path, aside, MoveTries, MoveWaitMs, move);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return (null, row with { Keeps = "in-use", By = [] });
            }

            return (aside, null);
        }
        finally
        {
            for (var at = held.Count - 1; at >= 0; at--) held[at].Dispose();
        }
    }

    private static DownloadCleared Kept(DownloadAsked asked, DownloadedVersion row, MachineLog log, string? door)
    {
        log.Info("download.kept", [.. Said(row, door), ("why", row.Keeps)]);
        return new DownloadCleared(asked, "kept", row);
    }

    /// <summary>A removal's log line: the version by its names, never its path, and the door only where one is named (an added field).</summary>
    private static (string Key, object? Value)[] Said(DownloadedVersion row, string? door) => door is null
        ? [("kind", row.Kind), ("name", row.Name), ("version", row.Version)]
        : [("kind", row.Kind), ("name", row.Name), ("version", row.Version), ("door", door)];

    /// <summary>Why a version stays, and what keeps it; null where nothing uses it.</summary>
    /// <param name="sessions">The running sessions (KEEP1b); null judges by the files alone, as the list does.</param>
    /// <param name="firstUnused">When the version's current unused stretch began; null is now, for the start-time rule.</param>
    /// <param name="settles">Whether a version downloaded within <see cref="Settling"/> stays.</param>
    public static (string? Keeps, IReadOnlyList<string> By) Judge(
        string kind, string name, string version, string folder, DownloadUses uses, DateTimeOffset now,
        LiveSessions? sessions = null, DateTimeOffset? firstUnused = null, bool settles = true)
    {
        if (kind == "agent")
        {
            if (uses.Harnesses.Problem is not null) return ("unread", ["harnesses.json"]);
            var by = new List<string>();
            if (uses.Harnesses.Versions.TryGetValue(name, out var machine) && Same(machine, version)) by.Add("machine");
            foreach (var (workspace, pins) in uses.Harnesses.WorkspaceVersions.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                if (pins.TryGetValue(name, out var pinned) && Same(pinned, version)) by.Add("workspace:" + workspace);
            }

            if (by.Count > 0) return ("pinned", by);
        }
        else
        {
            if (!uses.Tools.Entries.TryGetValue(name, out var entry) || entry.Problem is not null) return ("unread", ["tools.json"]);
            if (entry.Way == ToolWay.Managed && entry.Version is { } used && Same(used, version)) return ("used", ["tools.json"]);
        }

        if (uses.Tools.Problem is not null) return ("unread", ["tools.json"]);
        if (uses.Tools.Entries.Values.Any(entry => entry.Way == ToolWay.File && Inside(entry.File, folder))) return ("named", ["tools.json"]);
        if (uses.Config is null) return ("unread", [DriverConfig.FileName]);
        if (uses.Config.Commands.Values.Any(command => command.Count > 0 && Inside(command[0], folder))) return ("named", [DriverConfig.FileName]);
        if (settles && now - Arrived(folder) < Settling) return ("recent", []);
        if (sessions is null) return (null, []);
        if (sessions.Problem is not null) return ("unread", ["sessions"]);

        // A session that says what its starts were handed keeps exactly those; one from before that field keeps what went unused after it
        // started, since its start may have resolved it (F2.5); one whose start is not known keeps everything.
        var key = DownloadKeeping.Key(kind, name, version);
        var since = firstUnused ?? now;
        var running = sessions.Sessions
            .Where(session => session.Resolved is { } resolved
                ? resolved.Contains(key, StringComparer.OrdinalIgnoreCase)
                : session.Started is not { } started || started < since)
            .Select(session => "session:" + session.Id)
            .ToList();
        return running.Count > 0 ? ("running", running) : (null, []);
    }

    /// <summary>
    /// The downloaded versions a start was handed (KEEP1b, F2.5), read off the start itself: every path in what it runs, its arguments (a
    /// shim's, where a command runs it) and its environment (an ACP adapter's <c>claude</c>, each managed tool's folder on its PATH) that
    /// lies inside a version under <c>cache/</c>, as <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>, in order. A path elsewhere is none.
    /// </summary>
    public static IReadOnlyList<string> Handed(string home, ProcessStartInfo info)
    {
        string agents, tools;
        try
        {
            agents = DaorisHome.Entry(home, HomeEntry.Toolchain);
            tools = DaorisHome.Entry(home, HomeEntry.ToolVersions);
        }
        catch (HomeEntryTwiceException)
        {
            return [];
        }

        var keys = new SortedSet<string>(StringComparer.Ordinal);
        Look(info.FileName);
        foreach (var argument in info.ArgumentList) Look(argument);
        foreach (var value in info.Environment.Values) Look(value);
        return [.. keys];

        void Look(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var part in text.Split(System.IO.Path.PathSeparator))
            {
                if (VersionOf(agents, part) is { } agent) keys.Add(DownloadKeeping.Key("agent", agent.Name, agent.Version));
                if (VersionOf(tools, part) is { } tool) keys.Add(DownloadKeeping.Key("tool", tool.Name, tool.Version));
            }
        }
    }

    /// <summary>The version folder under <paramref name="root"/> a whole path lies in, by whole segments; null for none.</summary>
    private static (string Name, string Version)? VersionOf(string root, string path)
    {
        var trimmed = path.Trim().Trim('"');
        if (trimmed.Length == 0 || !System.IO.Path.IsPathFullyQualified(trimmed)) return null;
        try
        {
            var under = System.IO.Path.GetRelativePath(System.IO.Path.GetFullPath(root), System.IO.Path.GetFullPath(trimmed));
            if (System.IO.Path.IsPathRooted(under)) return null;
            var parts = under.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            return parts.Length >= 2 && parts[0] is not ("." or "..") && parts[1] is not ("." or "..") && parts[0].Length > 0 && parts[1].Length > 0
                ? (parts[0], parts[1])
                : null;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>The version an unfinished folder was, or null where the name is no such folder's.</summary>
    private static string? UnfinishedVersion(string name)
    {
        if (!name.EndsWith(Aside, StringComparison.Ordinal)) return null;
        var stem = name[..^Aside.Length];
        var dot = stem.LastIndexOf('.');
        return dot > 0 && stem.Length - dot == 9 && stem[(dot + 1)..].All(Uri.IsHexDigit) ? stem[..dot] : null;
    }

    private static bool Same(string? pinned, string version) => string.Equals(pinned?.Trim(), version, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="file"/>, a whole path, is inside <paramref name="folder"/>.</summary>
    private static bool Inside(string? file, string folder)
    {
        if (string.IsNullOrWhiteSpace(file) || !System.IO.Path.IsPathFullyQualified(file)) return false;
        var top = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(folder)) + System.IO.Path.DirectorySeparatorChar;
        return System.IO.Path.GetFullPath(file).StartsWith(top, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>When a version arrived: its folder was made as its download began, and written as it landed.</summary>
    private static DateTimeOffset Arrived(string folder)
    {
        try
        {
            var info = new DirectoryInfo(folder);
            var at = info.CreationTimeUtc > info.LastWriteTimeUtc ? info.CreationTimeUtc : info.LastWriteTimeUtc;
            return new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A time it cannot read is a download it cannot call old.
            return DateTimeOffset.MaxValue;
        }
    }

    /// <summary>A row with the size of <paramref name="folder"/>, its own or where it was moved aside.</summary>
    private static DownloadedVersion MeasuredAt(DownloadedVersion row, string folder, CancellationToken cancellationToken)
    {
        var (bytes, _, partial) = WiringSizes.MeasureFolder(folder, WiringBudget.Default, cancellationToken);
        return row with { Bytes = bytes, Partial = partial };
    }

    /// <summary>The folders under <paramref name="path"/>, no link among them; none where it is not there or cannot be read.</summary>
    private static IReadOnlyList<DirectoryInfo> Folders(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? [.. new DirectoryInfo(path).EnumerateDirectories()
                    .Where(folder => !folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    .OrderBy(folder => folder.Name, StringComparer.Ordinal)]
                : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>A folder moved aside, removed whole: a read-only file is made writable first, and a link inside is removed, never followed.</summary>
    private static bool Remove(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Read-only files (a package's own) refuse a recursive delete: made writable, then tried once more.
        }

        try
        {
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", new EnumerationOptions
                     {
                         RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true,
                     }))
            {
                if (file.Attributes.HasFlag(FileAttributes.ReadOnly)) file.Attributes &= ~FileAttributes.ReadOnly;
            }

            Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return !Directory.Exists(folder);
        }
    }

    private static void RemoveIfEmpty(string? folder)
    {
        try
        {
            if (folder is not null && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Something arrived in it, or holds it: an empty folder left is nothing lost.
        }
    }
}
