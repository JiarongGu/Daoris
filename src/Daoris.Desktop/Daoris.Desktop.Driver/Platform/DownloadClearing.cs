namespace Daoris.Driver;

/// <summary>
/// One version an agent or a tool downloaded into the Daoris folder's <c>cache/</c> (CACHECLEAR1), and what keeps it.
/// </summary>
/// <param name="Kind"><c>agent</c>, a pinned agent's version (<c>cache/toolchain/&lt;agent&gt;/&lt;version&gt;</c>), or <c>tool</c>, a
/// managed tool's (<c>cache/tools/&lt;tool&gt;/&lt;version&gt;</c>).</param>
/// <param name="Name">The agent or the tool, as its folder names it.</param>
/// <param name="Version">The version, as its folder names it.</param>
/// <param name="Path">Its folder.</param>
/// <param name="Bytes">Its size, as far as the walk got.</param>
/// <param name="Partial">Whether any read failed, so the size is not whole.</param>
/// <param name="Keeps">
/// Null where nothing uses it, else why it stays: <c>pinned</c> (an agent's pin names it), <c>used</c> (Tools runs it), <c>named</c> (a
/// setting names a file inside it), <c>unread</c> (a file that says what is used cannot be read), <c>recent</c> (downloaded within
/// <see cref="DownloadClearing.Settling"/>, so a pin or a use may be on its way), or, at a press, <c>in-use</c> (a program holds it).
/// </param>
/// <param name="By">What keeps it: <c>machine</c> or <c>workspace:&lt;name&gt;</c> for a pin, else the file (<c>tools.json</c>,
/// <c>driver.json</c>, <c>harnesses.json</c>).</param>
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
/// before it would have gone; <c>gone</c>, no longer there; or <c>unfinished</c>, moved aside so nothing runs it, and not all removed.
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
/// Clearing downloads (CACHECLEAR1): the versions the agents and the tools downloaded into <c>cache/</c> that nothing uses, LISTED
/// first with what keeps each of the rest, then removed by the person's press (D88's shape: listed, pressed, each judged again right
/// before it goes). The screen's half is Settings → This Daoris's <i>Downloads</i> row.
/// </summary>
/// <remarks>
/// <para><b>What uses a version is what would run it.</b> An agent's version is used where a pin names it, this machine's or any
/// workspace's (<see cref="HarnessSettings.Versions"/>, <see cref="HarnessSettings.WorkspaceVersions"/>), since a start resolves its
/// workspace's pin first; a tool's where Tools runs it managed at that version. A version a setting names a file inside (a tool's
/// <c>file</c>, a command in <c>driver.json</c>) is used too. A file that cannot be read keeps everything it would have said.</para>
///
/// <para><b>A download a moment old stays.</b> A pin and a use are written after their download lands (<see cref="HarnessActions"/>,
/// <see cref="ToolInstall.DownloadAsync"/>), so a version downloaded within <see cref="Settling"/> is kept: it may be the one about to be
/// pinned. A staged download (<c>.part</c>) is never listed.</para>
///
/// <para><b>Moved aside, then removed.</b> A version goes by one rename beside itself first, which fails while a program runs from it
/// (Windows holds a running executable's folder), so a running session's old version is kept as <c>in-use</c> rather than half
/// deleted. Once moved, nothing resolves it, and what a removal could not take is listed as unfinished and goes at the next press.</para>
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
    public static DownloadListing List(string? home, DownloadUses uses, DateTimeOffset now, CancellationToken cancellationToken = default)
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

        DownloadedVersion Measured(DownloadedVersion row)
        {
            var (bytes, _, partial) = WiringSizes.MeasureFolder(row.Path, WiringBudget.Default, cancellationToken);
            return row with { Bytes = bytes, Partial = partial };
        }
    }

    /// <summary>
    /// Remove the versions a press names that nothing keeps, each judged again, from the files as they stand, right before it goes.
    /// </summary>
    /// <param name="uses">The files that say what is used, read again for each version.</param>
    /// <param name="move">The rename aside; a test hands in one that refuses, as a running program makes Windows refuse.</param>
    public static IReadOnlyList<DownloadCleared> Clear(
        string? home, IReadOnlyList<DownloadAsked> asked, Func<DownloadUses> uses, Func<DateTimeOffset> now, MachineLog? log = null,
        Action<string, string>? move = null, CancellationToken cancellationToken = default)
    {
        log ??= MachineLog.None;
        var listed = List(home, uses(), now(), cancellationToken).Versions;
        var cleared = new List<DownloadCleared>();
        foreach (var each in asked)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                var (keeps, by) = Judge(row.Kind, row.Name, row.Version, row.Path, uses(), now());
                if (keeps is not null)
                {
                    cleared.Add(Kept(each, row with { Keeps = keeps, By = by }, log));
                    continue;
                }

                aside = $"{row.Path}.{Guid.NewGuid().ToString("N")[..8]}{Aside}";
                try
                {
                    AtomicFile.MoveFolder(row.Path, aside, MoveTries, MoveWaitMs, move);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    cleared.Add(Kept(each, row with { Keeps = "in-use", By = [] }, log));
                    continue;
                }
            }

            var removed = Remove(aside);
            log.Info(removed ? "download.cleared" : "download.unfinished", ("kind", row.Kind), ("name", row.Name), ("version", row.Version));
            cleared.Add(new DownloadCleared(each, removed ? "removed" : "unfinished", row));
            if (removed) RemoveIfEmpty(Directory.GetParent(row.Path)?.FullName);
        }

        return cleared;
    }

    private static DownloadCleared Kept(DownloadAsked asked, DownloadedVersion row, MachineLog log)
    {
        log.Info("download.kept", ("kind", row.Kind), ("name", row.Name), ("version", row.Version), ("why", row.Keeps));
        return new DownloadCleared(asked, "kept", row);
    }

    /// <summary>Why a version stays, and what keeps it; null where nothing uses it.</summary>
    public static (string? Keeps, IReadOnlyList<string> By) Judge(
        string kind, string name, string version, string folder, DownloadUses uses, DateTimeOffset now)
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
        return now - Arrived(folder) < Settling ? ("recent", []) : (null, []);
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
