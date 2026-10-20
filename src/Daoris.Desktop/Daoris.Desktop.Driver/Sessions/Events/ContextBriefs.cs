namespace Daoris.Driver;

/// <summary>One part of a line as its record says it (SESSCTX1b): its id, why it continued the part before it, and its record.</summary>
/// <param name="Why">Its <c>fromWhy</c>, one of <see cref="HandOverWhy"/>; null where it continues none.</param>
public sealed record LinePart(string Session, string? Why, PriorSession Record);

/// <summary>
/// What a brief is composed from, read on this machine (SESSCTX1b, D189 point 4; the session-context design §3): the line a new part
/// continues, one chain back by each record's <c>from</c>, each part's events as its record keeps them, the tree as it stands now,
/// the gates that make a call a check, the entry's other parts and what is still asked. <see cref="ContextBrief.Compose"/> is the
/// pure half; this reads what it is handed, and nothing here writes.
/// </summary>
/// <remarks>
/// <b>Never a part beside the line</b>: a record of the same entry that continues nothing on it is one of the entry's other parts,
/// one line each, never its words. <b>A part this machine holds no record of</b> (a teammate's, deleted) ends the line there.
/// </remarks>
internal static class ContextBriefs
{
    /// <summary>The most parts a line is followed back: a guard against a chain no person made.</summary>
    internal const int Depth = 50;

    /// <summary>The most of a list read for the brief to count: what it names, and that more were there.</summary>
    private const int ListRead = 500;

    /// <summary>
    /// The line <paramref name="session"/> ends, oldest part first: it and, by each record's <c>from</c>, the parts it continued. A
    /// link that comes back on itself, or names a record not held here, ends the line where it would repeat or break.
    /// </summary>
    internal static IReadOnlyList<LinePart> LineOf(IReadOnlyDictionary<string, PriorSession> records, string session)
    {
        var line = new List<LinePart>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var at = session;
        while (at is not null && line.Count < Depth && seen.Add(at) && records.TryGetValue(at, out var record))
        {
            line.Add(new LinePart(at, record.FromWhy, record));
            at = record.From is { Length: > 0 } from ? from : null;
        }

        line.Reverse();
        return line;
    }

    /// <summary>
    /// The brief of the line <paramref name="session"/> ends, for the new part that goes on from it now because of
    /// <paramref name="why"/> (SESSCTX1b). Read from the service's records, this machine's event files and the tree.
    /// </summary>
    /// <param name="conversation">A conversation's line, whose request is its first word; a driven line's is its quest.</param>
    /// <param name="note">
    /// The outgoing agent's own note (the design §3.1 item 6), as its claim: the seam MIXAGENT1c's <c>handover_propose</c> and SESSCTX1c's
    /// closing note hand in. Null where none was written.
    /// </param>
    /// <param name="tree">The tree the new part works in, read now; null for the one the part moved from ran in.</param>
    /// <param name="noteAsked">Whether the outgoing agent was asked for its closing note (SESSCTX1c part B): none written is said absent.</param>
    /// <param name="before">
    /// How many of the part moved from's events stood before its closing note was asked (SESSCTX1c part B): the brief reads those alone,
    /// so the closing turn is told once, as the note. Null reads them all.
    /// </param>
    internal static async Task<ComposedBrief> ComposeAsync(
        ServiceClient service, SessionEvents events, string home, string session, string why, bool conversation, string? note = null,
        DriverConfig? config = null, string? tree = null, CancellationToken ct = default, bool noteAsked = false, int? before = null)
    {
        // Records that could not be read leave a line of the one part moved from: its own words, never nothing.
        var json = await RecordsAsync(service, ct).ConfigureAwait(false);
        var records = ServiceClient.ReadRecords(json);
        var line = LineOf(records, session);
        IReadOnlyList<SessionEvent> Read(string part)
        {
            var kept = Events(events, home, part);
            return before is { } count && string.Equals(part, session, StringComparison.Ordinal) && count < kept.Count ? [.. kept.Take(count)] : kept;
        }

        var parts = line.Count == 0
            ? [new BriefPart(session, null, Read(session)) { Kept = Kept(home, session) }]
            : line.Select((part, at) => new BriefPart(part.Session, part.Record.Adapter, Read(part.Session))
            {
                Why = at == 0 ? null : part.Why,
                Kept = Kept(home, part.Session),
            }).ToList();

        var last = line.Count > 0 ? line[^1].Record : null;
        var named = tree ?? last?.Tree;
        var standing = named is { Length: > 0 } && Directory.Exists(named) ? named : null;
        return ContextBrief.Compose(new BriefLine(why, parts)
        {
            Conversation = conversation,
            Tree = named,
            State = standing is null ? null : await TreeAsync(standing, line.Count > 0 ? line[0].Record.BaseCommit : null, ct).ConfigureAwait(false),
            Gates = Gates(standing, last?.Repository, config ?? Config(home)),
            Note = note,
            NoteAsked = noteAsked,
            Others = Others(json, line),
            Open = Open(home, line),
        });
    }

    /// <summary>The ids of the parts a new part's line continues, for the reads its rules allow (design §3.1 item 5); none read, none.</summary>
    internal static async Task<IReadOnlyList<string>> LineIdsAsync(ServiceClient service, string session, CancellationToken ct) =>
        [.. LineOf(ServiceClient.ReadRecords(await RecordsAsync(service, ct).ConfigureAwait(false)), session).Select(part => part.Session)];

    /// <summary>The service's records, closed ones included; an answer that could not be had or read is none.</summary>
    private static async Task<string> RecordsAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return json;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return "[]";
        }
    }

    /// <summary>
    /// A part's events, oldest first, as its record keeps them; none for an id that names no file here, or one that does not read.
    /// A record that kept none of the agent's words has its transcript's last plain lines as its last message, as a parked session's
    /// card reads them (<see cref="SessionLastWords.ParkedWords"/>), so a carry-on keeps the last words it was handed before the brief.
    /// </summary>
    private static IReadOnlyList<SessionEvent> Events(SessionEvents events, string home, string session)
    {
        if (!SessionEventText.IsLocalId(session)) return [];
        IReadOnlyList<SessionEvent> kept;
        try
        {
            kept = events.After(session, 0).Events;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            kept = [];
        }

        if (kept.Any(e => e.Kind == SessionEventKind.Message)) return kept;
        return SessionLastWords.LastWords(DaorisHome.Entry(home, HomeEntry.Sessions, $"{session}.log")) is { } words
            ? [.. kept, new SessionEvent { Kind = SessionEventKind.Message, Text = words }]
            : kept;
    }

    /// <summary>Where a part's kept attachments lie, where it keeps any (<see cref="ChatFiles.Folder"/>).</summary>
    private static string? Kept(string home, string session) =>
        SessionEventText.IsLocalId(session) && ChatFiles.Folder(home, session) is var folder && Directory.Exists(folder) ? folder : null;

    /// <summary>
    /// The tree as it stands now (design §3.1 item 5), since a part's end commit is not recorded: its head, what it holds uncommitted,
    /// and the commits on it since the line's first part began. Null for the head where git cannot say.
    /// </summary>
    private static async Task<BriefTree> TreeAsync(string tree, string? since, CancellationToken ct)
    {
        try
        {
            var (headCode, head, _) = await WorkingTree.GitAsync(tree, ["log", "-1", "--format=%h %s"], ct).ConfigureAwait(false);
            if (headCode != 0 || head.Trim().Length == 0) return new BriefTree(null, [], []);
            var uncommitted = await ConversationTrees.UncommittedAsync(tree, ListRead, ct).ConfigureAwait(false);
            IReadOnlyList<string> commits = [];
            if (since is { Length: > 0 } && WorkingTree.IsCommitId(since))
            {
                var (code, log, _) = await WorkingTree.GitAsync(tree, ["log", $"--max-count={ListRead}", "--format=%h %s", $"{since}..HEAD"], ct)
                    .ConfigureAwait(false);
                if (code == 0) commits = [.. log.Split('\n').Select(entry => entry.TrimEnd('\r')).Where(entry => entry.Trim().Length > 0)];
            }

            return new BriefTree(head.Trim(), uncommitted, commits);
        }
        catch (Exception error) when (error is DriverException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new BriefTree(null, [], []);
        }
    }

    /// <summary>
    /// What makes a call a check (design §3.1 item 4): the gates the tree's <c>daoris.gates.json</c> declares, as <see cref="HeavyGates.Read"/>
    /// reads them, and this machine's heavy-gate mapping for the repository. A file that does not read declares none here.
    /// </summary>
    private static IReadOnlyList<HeavyGate> Gates(string? tree, string? repository, DriverConfig? config)
    {
        var gates = new List<HeavyGate>();
        if (tree is not null && File.Exists(Path.Combine(tree, HeavyGates.FileName)))
        {
            try
            {
                gates.AddRange(HeavyGates.Read(tree));
            }
            catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
            {
                // A file that does not read names no check: the brief says the calls, never a guess.
            }
        }

        if (config is not null && repository is { Length: > 0 } && HeavyMapping.For(config, repository) is { } mapped) gates.AddRange(mapped);
        return gates;
    }

    private static DriverConfig? Config(string home)
    {
        try
        {
            return DriverConfig.Load(DriverConfig.ResolvePath(home));
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The entry's other parts (design §3.1 item 7), beside the line: each record of its line of work that is not on it, oldest first,
    /// with its quest, how it began and how it stands.
    /// </summary>
    private static IReadOnlyList<string> Others(string json, IReadOnlyList<LinePart> line)
    {
        if (line.Count == 0) return [];
        var all = SessionRecords.Parse(json);
        var on = line.Select(part => part.Session).ToHashSet(StringComparer.Ordinal);
        var key = all.FirstOrDefault(record => string.Equals(record.Id, line[^1].Session, StringComparison.Ordinal))?.EntryKey;
        if (key is null) return [];
        return
        [
            .. all.Where(record => record.EntryKey == key && !on.Contains(record.Id))
                .OrderBy(record => record.Created).ThenBy(record => record.Id, StringComparer.Ordinal)
                .Select(record => $"session `{record.Id}`{(record.Quest is { } quest ? $" for #{quest}" : "")}, "
                                  + $"{(record.From is null ? PartBegan.Opened : SessionEntries.ByLink(record.FromWhy))}, {record.State}"),
        ];
    }

    /// <summary>The asks the line's parts made that no answer settled yet (design §3.1 item 5): a helper's ask still open.</summary>
    private static IReadOnlyList<string> Open(string home, IReadOnlyList<LinePart> line)
    {
        var on = line.Select(part => part.Session).ToHashSet(StringComparer.Ordinal);
        var helpers = new HelperFiles(home);
        try
        {
            return
            [
                .. helpers.Asks()
                    .Where(ask => on.Contains(ask.Asker) && helpers.ReadState(ask.Id)?.State is not (HelperStates.Answered or HelperStates.Refused))
                    .Select(ask => $"a helper's answer to session `{ask.Asker}`'s question: {TextBounds.Clip(TextBounds.OneLine(ask.Question), ContextBrief.StepLimit)}"),
            ];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return [];
        }
    }
}
