namespace Daoris.Driver;

/// <summary>
/// What the home keeps of finished work, read and tidied (HIST1c; the history-clearing design §2.2–§2.4, §5 step 4): the
/// kept files' sizes, a workspace's reading, the left-over files of records no store has any more, and the files that name a
/// cleared record.
/// </summary>
public static partial class HistoryClearing
{
    /// <summary>The service's folders of kept files under the home, a quest's and an ask's (D65 §1a), each <c>&lt;id&gt;/</c>.</summary>
    private const string QuestFolder = "quests";
    private const string AskFolder = "asks";

    /// <summary>The folder under <c>sessions/</c> where a terminal's requests wait (<see cref="SessionRequests"/>): never a session's.</summary>
    private const string RequestsFolder = "requests";

    /// <summary>The kept files of these quests and asks, in bytes.</summary>
    private static HistoryBytes KeptFiles(SessionHomeFiles files, IEnumerable<string> quests, IEnumerable<string> asks)
    {
        var home = files.Home;
        var kept = quests.Where(Plain).Sum(quest => HistoryBytes.Of(Path.Combine(home, QuestFolder, quest)))
                   + asks.Where(Plain).Sum(ask => HistoryBytes.Of(Path.Combine(home, AskFolder, ask)));
        return new HistoryBytes(0, 0, 0, kept, 0);
    }

    /// <summary>An id that names one folder under its parent and nothing else.</summary>
    private static bool Plain(string id) =>
        id.Length > 0 && id.IndexOfAny(['/', '\\', ':']) < 0 && !id.Contains("..", StringComparison.Ordinal);

    private static bool Here(string? workspace, string named) =>
        string.Equals(RemoteTarget.Workspace(workspace), named, StringComparison.OrdinalIgnoreCase);

    private static bool Closed(QuestView quest) => Is(quest.Status, "Done") || Is(quest.Status, "Declined");

    private static bool Closed(AskView ask) => Is(ask.State, "Closed") || Is(ask.State, "Done");

    /// <summary>
    /// A workspace's reading (§2.4): its closed quests and asks, their sessions and what they take on the disk, what a clear
    /// would take now, what is kept and why, its conversations that served no quest, and the whole home's left-over files and
    /// log. Counts and bytes, never a path or a title.
    /// </summary>
    private static HistoryReading Read(HistoryWorld world, string workspace, IReadOnlyList<HistoryUnitPlan> units, Facts facts, Machine machine)
    {
        var quests = facts.Quests.Where(quest => Here(quest.Workspace, workspace) && Closed(quest)).Select(quest => quest.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asks = facts.Asks.Where(ask => Here(ask.Workspace, workspace) && Closed(ask)).Select(ask => ask.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var records = facts.Records
            .Where(record => (record.Quest is { } quest && quests.Contains(quest)) || (record.Ask is { } ask && asks.Contains(ask)))
            .ToList();
        var ours = records.Where(record => !record.Teammate).ToList();

        var clearable = units.Where(unit => unit.Clearable).ToList();
        var intake = HistoryBytes.Of(IntakeRoom.PathOf(world.Home, workspace));
        var freesRoom = facts.Asks.Where(ask => Here(ask.Workspace, workspace))
            .All(ask => clearable.Any(unit => unit.Asks.Contains(ask.Id, StringComparer.OrdinalIgnoreCase)));
        var leftOver = LeftOver(world, facts, machine);
        var chats = facts.Records
            .Where(record => Here(record.Workspace, workspace) && !record.Teammate && record.Quest is null && record.Ask is null)
            .ToList();

        return new HistoryReading(workspace)
        {
            Quests = quests.Count,
            Asks = asks.Count,
            Sessions = ours.Count,
            Teammates = records.Count - ours.Count,
            Bytes = ours.Aggregate(HistoryBytes.None, (sum, record) => sum + machine.Files.Size(record.Id))
                    + KeptFiles(machine.Files, quests, asks),
            Intake = intake,
            Takes = new HistoryTakes(
                clearable.Sum(unit => unit.Quests.Count),
                clearable.Sum(unit => unit.Asks.Count),
                clearable.Sum(unit => unit.Sessions.Count),
                clearable.Sum(unit => unit.Teammates.Count),
                clearable.Sum(unit => unit.Bytes.Total) + (freesRoom ? intake : 0) + leftOver.Sum(file => file.Bytes)),
            KeptBy = units.Where(unit => !unit.Clearable)
                .GroupBy(unit => unit.Keep!.Word, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            Conversations = chats.Count,
            ConversationBytes = chats.Sum(record => machine.Files.Size(record.Id).Total),
            LeftOver = leftOver.Count,
            LeftOverBytes = leftOver.Sum(file => file.Bytes),
            Log = HistoryBytes.Of(Path.Combine(world.Home, MachineLog.Folder)),
        };
    }

    /// <summary>A file or folder of a record no store has any more (§2.3).</summary>
    private sealed record LeftOverFile(string Path, bool Folder, long Bytes);

    /// <summary>
    /// The home's left-over files (§2.3): a file under <c>sessions/</c>, <c>spawn/</c>, <c>quests/</c> or <c>asks/</c> whose id
    /// no record, quest or ask holds, which no process here runs, and which nothing touched for an hour. Only names a session's
    /// own files carry are read, so a file Daoris did not write is never one; it names no workspace, so every workspace's
    /// clear takes it.
    /// </summary>
    private static IReadOnlyList<LeftOverFile> LeftOver(HistoryWorld world, Facts facts, Machine machine)
    {
        var records = facts.Records.Select(record => record.Id).ToHashSet(StringComparer.Ordinal);
        var quests = facts.Quests.Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asks = facts.Asks.Select(ask => ask.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var before = world.Clock() - world.Untouched;
        var found = new List<LeftOverFile>();

        bool Gone(string id) => SessionEvents.IsId(id) && !records.Contains(id) && !machine.Alive(id);
        void Add(string path, bool folder)
        {
            if (Touched(path, folder) < before) found.Add(new LeftOverFile(path, folder, HistoryBytes.Of(path)));
        }

        var sessions = Path.Combine(world.Home, "sessions");
        foreach (var file in Files(sessions))
        {
            if (IdOf(Path.GetFileName(file), SessionHomeFiles.SessionSuffixes) is { } id && Gone(id)) Add(file, folder: false);
        }

        foreach (var folder in Folders(sessions))
        {
            var id = Path.GetFileName(folder);
            if (!string.Equals(id, RequestsFolder, StringComparison.OrdinalIgnoreCase) && Gone(id)) Add(folder, folder: true);
        }

        foreach (var file in Files(Path.Combine(world.Home, SpawnServers.Folder)))
        {
            if (IdOf(Path.GetFileName(file), SessionHomeFiles.SpawnSuffixes) is { } id && Gone(id)) Add(file, folder: false);
        }

        foreach (var folder in Folders(Path.Combine(world.Home, QuestFolder)))
        {
            var id = Path.GetFileName(folder);
            if (Plain(id) && !quests.Contains(id)) Add(folder, folder: true);
        }

        foreach (var folder in Folders(Path.Combine(world.Home, AskFolder)))
        {
            var id = Path.GetFileName(folder);
            if (Plain(id) && !asks.Contains(id)) Add(folder, folder: true);
        }

        return found;
    }

    /// <summary>The id a session's file names before one of <paramref name="suffixes"/>, or null for a name no session's file has.</summary>
    private static string? IdOf(string name, IReadOnlyList<string> suffixes) =>
        suffixes.FirstOrDefault(suffix => name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)) is { } found
            ? name[..^found.Length]
            : null;

    private static IEnumerable<string> Files(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder)] : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> Folders(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? [.. Directory.EnumerateDirectories(folder)] : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>When anything last touched a file, or a folder and everything in it; now, where it cannot be read, so it is never taken.</summary>
    private static DateTimeOffset Touched(string path, bool folder)
    {
        try
        {
            if (!folder) return File.GetLastWriteTimeUtc(path);
            var info = new DirectoryInfo(path);
            return info.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
                .Select(entry => entry.LastWriteTimeUtc)
                .Append(info.LastWriteTimeUtc)
                .Max();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return DateTimeOffset.MaxValue;
        }
    }

    /// <summary>
    /// A workspace's clear takes the home's left-over files (§2.3, §5 step 4): how many went and the bytes that went, through the
    /// helper that measures as it removes (HIST1j), so one the disk keeps is failed and counts only what it let go of.
    /// </summary>
    private static (int Count, long Bytes) RemoveLeftOver(HistoryWorld world, Facts after, Machine machine, List<string> failed)
    {
        var count = 0;
        var bytes = 0L;
        foreach (var file in LeftOver(world, after, machine))
        {
            var (gone, went) = machine.Files.Take(file.Path, file.Folder, failed);
            if (gone) count += 1;
            bytes += went;
        }

        return (count, bytes);
    }

    /// <summary>
    /// A workspace's clear that leaves it no ask takes its intake's room (§2.2): one room per workspace, rendered again at the
    /// next intake's open. A workspace that keeps an ask keeps its room. Only the bytes that went are counted (HIST1j).
    /// </summary>
    private static (bool Went, long Bytes) RemoveRoom(HistoryWorld world, string workspace, Facts after, SessionHomeFiles files, List<string> failed) =>
        after.Asks.Any(ask => Here(ask.Workspace, workspace))
            ? (false, 0)
            : files.Take(IntakeRoom.PathOf(world.Home, workspace), folder: true, failed);

    /// <summary>
    /// What names a cleared record, tidied (§5 step 4, §4): an empty folder a cleared session's tree left and its empty parents,
    /// a landing's trace, an abandon's entry, and <c>driver.json</c>'s entries for a cleared quest or ask. A workspace's clear
    /// also takes the traces and entries that name records no store has any more, as it takes their files. Never a standing
    /// landing, a tree with anything in it, or a setting.
    /// </summary>
    private static void Tidy(
        HistoryWorld world, HistoryScope scope, string workspace, IReadOnlyList<HistoryUnitPlan> cleared, Facts before, Facts? after,
        List<string> failed)
    {
        var sessions = cleared.SelectMany(unit => unit.Sessions.Concat(unit.Teammates)).ToHashSet(StringComparer.Ordinal);
        EmptyTrees(world, sessions.Select(session => before.Record(session)?.Tree).OfType<string>(), failed);

        var held = after?.Records.Select(record => record.Id).ToHashSet(StringComparer.Ordinal);
        bool Gone(string session) => sessions.Contains(session) || (held is not null && !held.Contains(session));
        IEnumerable<string> Named(LandedBranch entry) => entry.Advances.Select(advance => advance.Session).Prepend(entry.Session);
        try
        {
            new LandedBranches(world.Home).ForgetTraces(entry => Named(entry).All(Gone)
                && (Named(entry).Any(sessions.Contains) || (scope == HistoryScope.Workspace && Here(entry.Workspace, workspace))));
            if (after is not null)
            {
                var quests = after.Quests.Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var asks = after.Asks.Select(ask => ask.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                new AbandonRecord(world.Home).Forget(entry => entry.Scope == "ask" ? asks.Contains(entry.Id) : quests.Contains(entry.Id));
            }

            Config(world, scope, cleared, after);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            failed.Add(error.Message);
        }
    }

    /// <summary>
    /// <c>driver.json</c>'s entries for each cleared quest (its restart mark, its release, its own pause) and each cleared ask
    /// (its pause), as abandon's step 7 tidies a closed quest's (§4); a failed-sessions clear keeps its quest, so its entries
    /// stay. A workspace's clear also drops the entries for quests and asks no store holds, as a hand purge left them.
    /// </summary>
    private static void Config(HistoryWorld world, HistoryScope scope, IReadOnlyList<HistoryUnitPlan> cleared, Facts? after)
    {
        var config = DriverConfig.Load(world.ConfigPath);
        var quests = cleared.Where(unit => unit.Kind != HistoryKinds.Failed).SelectMany(unit => unit.Quests).ToList();
        var asks = cleared.SelectMany(unit => unit.Asks).ToList();
        if (scope == HistoryScope.Workspace && after is not null)
        {
            var held = after.Quests.Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            quests.AddRange(config.Forgiven.Keys.Concat(config.Released.Keys).Concat(config.PausedQuests.Keys).Where(quest => !held.Contains(quest)));
            var standing = after.Asks.Select(ask => ask.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            asks.AddRange(config.PausedAsks.Keys.Where(ask => !standing.Contains(ask)));
        }

        var next = config;
        foreach (var quest in quests.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            next = next.WithoutQuest(quest);
            if (next.PausedQuest(quest) is not null) next = next.WithPausedQuest(quest, null);
        }

        foreach (var ask in asks.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (next.PausedAsk(ask) is not null) next = next.WithPausedAsk(ask, null);
        }

        if (!ReferenceEquals(next, config)) next.Save(world.ConfigPath);
    }

    /// <summary>
    /// An empty folder a cleared session's tree left under the trees home (§2.2), removed with each empty parent it leaves up
    /// to the trees home itself, which nothing removed before. A folder with anything in it is never touched: it is a tree, and
    /// the clear refused it before.
    /// </summary>
    private static void EmptyTrees(HistoryWorld world, IEnumerable<string> trees, List<string> failed)
    {
        var home = new SessionTrees(world.Home);
        var root = Path.GetFullPath(home.TreesRoot).TrimEnd(Path.DirectorySeparatorChar);
        foreach (var tree in trees.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string folder;
            try
            {
                if (!home.Holds(tree)) continue;
                folder = Path.GetFullPath(tree).TrimEnd(Path.DirectorySeparatorChar);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            while (folder.Length > root.Length && folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // A folder already gone may still have left its parent empty.
                    if (Directory.Exists(folder))
                    {
                        if (Directory.EnumerateFileSystemEntries(folder).Any()) break;
                        Directory.Delete(folder, recursive: false);
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    failed.Add(folder);
                    break;
                }

                folder = Path.GetDirectoryName(folder)!;
            }
        }
    }
}
