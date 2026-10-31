using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A repository's memory as a session meets it (KNOWCENTRAL2b, 2d; the memory coordinator design §1, §2): <c>memory_search</c> and
/// <c>memory_get</c> read, <c>memory_write</c> and <c>memory_end</c> change the caller's own repository's, and <c>memory_propose</c> asks
/// an entry's owner. Every door answers from the repositories the registry places in the caller's workspace (<see cref="MemoryScope"/>),
/// and every engine is opened with no reinforcement, so a read changes nothing. A change is kept <c>staged</c> under its session, seen by
/// that session and a session continuing it alone, until the session's commit point releases it; no tool here calls an engine's write.
/// </summary>
/// <param name="service">The registry each call reads its workspace's repositories from.</param>
/// <param name="ambient">An unnamed caller's workspace, from where it runs.</param>
/// <param name="intake">The session this connector names, and the role it started in; none names none, a person's own agent's.</param>
/// <param name="ledger">Where the named session's record is read and its changes staged.</param>
/// <param name="engines">Each repository's memory; none where memory is off.</param>
/// <param name="time">The clock a change's instant is read from.</param>
[McpServerToolType]
public sealed class MemoryTools(
    KnowledgeService service, AmbientWorkspace ambient, IntakeScope? intake = null, SessionLedger? ledger = null, IMemoryEngines? engines = null,
    TimeProvider? time = null)
{
    /// <summary>What every tool answers where memory is off, after its checks.</summary>
    public const string NotOn = "Memory is not on in this Daoris yet, so nothing was read or kept.";

    /// <summary>What <c>memory_write</c> answers to a replacement, until Lyntai applies one as one change (the design §4, §6 A).</summary>
    public const string SupersedesWaits =
        "Refused: replacing an entry in one change waits on Lyntai's atomic supersession, requested as §6 A of the memory coordinator "
        + "design. Write the new fact with `relation` `disputes` and `target` the old entry, then end the old one with memory_end: two "
        + "decisions, both current until the end is approved. Nothing was kept.";

    private const int Limit = 10;

    private static readonly string[] Relations = ["disputes", "amends"];

    private static readonly string[] Changes = ["disputes", "amends", "end"];

    private readonly MemoryScope _scope = new(service, ambient, intake, ledger);

    [McpServerTool(Name = "memory_search")]
    [Description(
        "Search the memory the repositories of this session's workspace keep: facts earlier sessions confirmed and wrote down, each "
        + "answered with its reference (`repository:entry`) and its revision. Use the words the entry would use. Read-only: a search "
        + "changes nothing in the memory.")]
    public async Task<string> SearchAsync(
        [Description("What to look for, in the words an entry would use.")] string query,
        CancellationToken ct = default)
    {
        var caller = await _scope.ReadAsync(ct).ConfigureAwait(false);
        if (caller.Refused is { } refused) return refused;
        if (engines is null) return NotOn;

        var found = new List<(MemoryReference Reference, MemoryEntry Entry)>();
        foreach (var repository in caller.Members)
        {
            if (engines.Open(repository, MemoryReinforcement.None) is not { } engine) continue;
            var entries = await engine.SearchAsync(query, Limit, ct).ConfigureAwait(false);
            found.AddRange(entries.Select(entry => (new MemoryReference(repository, entry.Id), entry)));
        }

        // This session's own additions, and those of the sessions it continues, before their commit point: no other session sees them.
        var staged = caller.Record is { } record && ledger is not null
            ? (await ledger.Memory.StagedForAsync(record.Id, ct).ConfigureAwait(false))
                .Where(op => op.Change is { Kind: MemoryOpKind.Write, Text: { } written } && Matches(written, query))
                .ToList()
            : [];
        if (found.Count == 0 && staged.Count == 0) return $"No memory entry in workspace `{caller.Workspace}` matches `{query}`.";
        var text = new StringBuilder($"Memory in workspace `{caller.Workspace}`:").AppendLine().AppendLine();
        foreach (var (reference, entry) in found.Take(Limit)) text.AppendLine($"- `{reference}` (revision {entry.Revision}): {entry.Text}");
        foreach (var op in staged.Take(Limit))
        {
            text.AppendLine($"- staged for `{op.Repository}` by this session's work (operation `{op.Id}`), applied when it is accepted: {op.Change.Text}");
        }

        return text.AppendLine().Append("Call `memory_get` with a reference to read it whole.").ToString();
    }

    private static bool Matches(string text, string query) =>
        query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

    [McpServerTool(Name = "memory_get")]
    [Description(
        "Read one memory entry whole by the reference memory_search answered: its text, its revision, and why it ended if it did. With "
        + "`expand`, also the entries it disputes, amends or otherwise relates to. Read-only.")]
    public async Task<string> GetAsync(
        [Description("The entry's reference, `repository:entry`, as memory_search names it.")] string reference,
        [Description("Also read the entries this one relates to.")] bool expand = false,
        CancellationToken ct = default)
    {
        if (MemoryReference.Parse(reference) is not { } named) return Words.Malformed(reference);
        var caller = await _scope.ReadAsync(ct).ConfigureAwait(false);
        if ((caller.Refused ?? caller.Outside(named)) is { } refused) return refused;
        if (engines is null) return NotOn;

        var at = caller.Placed(named);
        if (engines.Open(at.Repository, MemoryReinforcement.None) is not { } engine
            || await engine.GetAsync(at.Entry, ct).ConfigureAwait(false) is not { } entry)
        {
            return $"No memory entry `{at}`. References come from memory_search.";
        }

        var text = new StringBuilder($"`{at}`, revision {entry.Revision}");
        if (entry.Ended is { } why) text.Append($", ended: {why}");
        text.AppendLine().AppendLine().AppendLine(entry.Text);
        if (!expand) return text.ToString();

        var related = await engine.ExpandAsync(at.Entry, ct).ConfigureAwait(false);
        text.AppendLine().AppendLine(related.Count == 0 ? "It relates to no other entry." : "It relates to:");
        foreach (var each in related)
        {
            text.AppendLine($"- {each.Link.Kind} `{at with { Entry = each.Entry.Id }}` (revision {each.Entry.Revision}): {each.Entry.Text}");
        }

        return text.ToString();
    }

    [McpServerTool(Name = "memory_write")]
    [Description(
        "Write one entry into the memory of the repository this session works in, for later sessions to find: a fact you confirmed, in "
        + "a sentence or two. Cite a record by its knowledge id rather than copying it. With `relation` and `target`, the entry disputes "
        + "or amends another entry of this repository's memory, which stays as it is. The write is kept with this session's work and "
        + "applies when that work is accepted.")]
    public async Task<string> WriteAsync(
        [Description("The fact, as later sessions should read it.")] string text,
        [Description("`disputes` or `amends`, given with `target`; omit for an entry that relates to none.")] string? relation = null,
        [Description("The reference of the entry this one disputes or amends.")] string? target = null,
        [Description("Not taken yet: a replacement waits on Lyntai. Dispute the old entry and end it instead.")] string? supersedes = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Write the entry's text: one fact, in a sentence or two.";
        if (!string.IsNullOrWhiteSpace(supersedes)) return SupersedesWaits;
        MemoryReference? related = null;
        if ((relation ?? target) is not null
            && (relation is not { } kind || !Relations.Contains(kind.Trim().ToLowerInvariant()) || (related = MemoryReference.Parse(target)) is null))
        {
            return "A relation is `disputes` or `amends`, given with `target`, the reference `repository:entry` of the entry it relates to.";
        }

        var caller = await _scope.ReadAsync(ct).ConfigureAwait(false);
        if ((caller.Refused ?? (related is null ? null : caller.Outside(related)) ?? caller.Unchanging("memory_write")
                ?? (related is null ? null : caller.NotOwnRelation(related))) is { } refused)
        {
            return refused;
        }

        if (engines is null || ledger is null || caller.Record is not { } record) return NotOn;
        var op = await ledger.Memory.StageAsync(
            caller.OwnPlaced!,
            new MemoryChange(MemoryOpKind.Write, Text: text.Trim(), Target: related is null ? null : caller.Placed(related).Entry,
                Relation: relation?.Trim().ToLowerInvariant()),
            Author(record), ct).ConfigureAwait(false);
        return $"Kept as operation `{op.Id}`, staged: `{op.Repository}`'s memory takes this entry when this session's work is accepted. "
            + "Until then only this session, and a session continuing it, finds it with memory_search.";
    }

    [McpServerTool(Name = "memory_end")]
    [Description(
        "End an entry of this repository's memory that is no longer true, saying why, at the revision you read. The end waits for its "
        + "approver and never applies by itself. An entry of another repository is not yours to end: ask its owner with memory_propose.")]
    public async Task<string> EndAsync(
        [Description("The entry's reference, `repository:entry`, as memory_search names it.")] string reference,
        [Description("The revision you read, as memory_get answered it.")] long revision,
        [Description("Why the entry is no longer true, citing what shows it.")] string reason,
        CancellationToken ct = default)
    {
        if (MemoryReference.Parse(reference) is not { } named) return Words.Malformed(reference);
        if (string.IsNullOrWhiteSpace(reason)) return "Say why the entry is no longer true, citing what shows it.";
        var caller = await _scope.ReadAsync(ct).ConfigureAwait(false);
        if ((caller.Refused ?? caller.Outside(named) ?? caller.Unchanging("memory_end") ?? caller.NotOwn(named)) is { } refused) return refused;
        if (engines is null || ledger is null || caller.Record is not { } record) return NotOn;
        var at = caller.Placed(named);
        var op = await ledger.Memory.StageAsync(
            at.Repository, new MemoryChange(MemoryOpKind.End, Text: reason.Trim(), Target: at.Entry, Revision: revision), Author(record), ct)
            .ConfigureAwait(false);
        return $"Kept as operation `{op.Id}`, staged: the end of `{at}` at revision {revision} waits for its approver once this session's "
            + "work is accepted. Nothing changes before.";
    }

    /// <summary>Who makes a change, from the caller's record and the role its connector was started in (§1), at this instant.</summary>
    private MemoryAuthor Author(Session record) => new(
        record.Id, record.Workspace, record.Quest, record.Adapter, record.HarnessVersion, intake?.Role, intake?.RoleRevision,
        (time ?? TimeProvider.System).GetUtcNow());

    [McpServerTool(Name = "memory_propose")]
    [Description(
        "Ask the repository that owns a memory entry to correct it: whether your finding disputes it, amends it or ends it, why, and the "
        + "revision you read. It reaches the owner as a quest, whose session decides; nothing changes until it does.")]
    public async Task<string> ProposeAsync(
        [Description("The entry's reference, `repository:entry`, as memory_search names it.")] string reference,
        [Description("`disputes`, `amends` or `end`.")] string change,
        [Description("What you found, citing what shows it.")] string reason,
        [Description("The revision you read, as memory_get answered it.")] long revision,
        CancellationToken ct = default)
    {
        if (MemoryReference.Parse(reference) is not { } named) return Words.Malformed(reference);
        if (change is not { } kind || !Changes.Contains(kind.Trim().ToLowerInvariant())) return "`change` is `disputes`, `amends` or `end`.";
        if (string.IsNullOrWhiteSpace(reason)) return "Say what you found, citing what shows it.";
        var caller = await _scope.ReadAsync(ct).ConfigureAwait(false);
        if ((caller.Refused ?? caller.Outside(named)) is { } refused) return refused;
        return NotOn;
    }

    private static class Words
    {
        public static string Malformed(string? text) =>
            $"`{text}` is no memory reference: write `repository:entry`, as memory_search names one.";
    }
}

/// <summary>
/// Who calls a memory tool (the memory coordinator design §2): its workspace and the repositories the registry places in it, read
/// again at every call, and its own repository. A named session's workspace is its record's, bound or not; an unnamed caller's is the
/// ambient one. A call names neither.
/// </summary>
/// <param name="Workspace">The caller's workspace; null where none resolved, which refuses every door.</param>
/// <param name="Members">The repositories of <paramref name="Workspace"/>, by the registry's names.</param>
/// <param name="Named">Whether the connector names a session Daoris started.</param>
/// <param name="Own">The repository the named session works in.</param>
public sealed record MemoryCaller(string? Workspace, IReadOnlyList<string> Members, bool Named, string? Own)
{
    /// <summary>The named session's record, as read at this call; null for an unnamed caller.</summary>
    public Session? Record { get; init; }

    /// <summary>The caller's own repository as the registry spells it.</summary>
    public string? OwnPlaced => Own is null ? null : Member(Own) ?? Own;

    /// <summary>The refusal of a relation to another repository's entry: a relation joins entries of one memory.</summary>
    public string? NotOwnRelation(MemoryReference reference) =>
        Own is not null && string.Equals(Member(reference.Repository), Member(Own), StringComparison.Ordinal)
            ? null
            : $"Refused: a relation joins entries of one memory, and `{reference}` is repository `{reference.Repository}`'s. To dispute or "
              + "amend it, ask its owner with memory_propose. Nothing was kept.";

    /// <summary>The refusal of a caller no workspace places; null where one does.</summary>
    public string? Refused => Workspace is null
        ? "Refused: Daoris cannot say which workspace this session is in, so its memory tools answer nothing here. Nothing was done."
        : null;

    /// <summary>The refusal of <paramref name="reference"/> where its repository is not in the workspace; null where it is.</summary>
    public string? Outside(MemoryReference reference) => Member(reference.Repository) is null
        ? $"Refused: `{reference}` is repository `{reference.Repository}`'s memory, and `{reference.Repository}` is not in workspace "
          + $"`{Workspace}`, whose memory alone this session reads. Nothing was done."
        : null;

    /// <summary>The refusal of a change by a caller with no repository of its own in the workspace; null where it has one.</summary>
    public string? Unchanging(string tool) =>
        !Named
            ? $"Refused: `{tool}` changes the memory of the repository a session Daoris started works in, and this connector names no "
              + "such session. Read with memory_search and memory_get, or ask an entry's owner with memory_propose. Nothing was kept."
            : Own is null || Member(Own) is null
                ? $"Refused: this session works in no repository of workspace `{Workspace}`, so it changes no memory there. Ask an "
                  + "entry's owner with memory_propose. Nothing was kept."
                : null;

    /// <summary>The refusal of an end of another repository's entry; null where the entry is the caller's own repository's.</summary>
    public string? NotOwn(MemoryReference reference) =>
        Own is not null && string.Equals(Member(reference.Repository), Member(Own), StringComparison.Ordinal)
            ? null
            : $"Refused: `{reference}` is repository `{reference.Repository}`'s, and this session ends entries of `{Own}` alone. Ask its "
              + "owner with memory_propose. Nothing was done.";

    /// <summary><paramref name="reference"/> with its repository spelled as the registry spells it.</summary>
    public MemoryReference Placed(MemoryReference reference) => reference with { Repository = Member(reference.Repository) ?? reference.Repository };

    private string? Member(string repository) =>
        Members.FirstOrDefault(member => string.Equals(member, repository, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Reads a memory tool's caller from the session its connector names, or from where an unnamed caller runs.</summary>
public sealed class MemoryScope(KnowledgeService service, AmbientWorkspace ambient, IntakeScope? intake = null, SessionLedger? ledger = null)
{
    public async Task<MemoryCaller> ReadAsync(CancellationToken ct)
    {
        var named = intake?.Session is { Length: > 0 };
        string? workspace;
        Session? record = null;
        if (named)
        {
            record = await RecordAsync(intake!.Session!, ct).ConfigureAwait(false);
            workspace = record?.Workspace;
        }
        else
        {
            workspace = await ambient.ResolveAsync(service, ct).ConfigureAwait(false);
        }

        if (workspace is null) return new(null, [], named, record?.Repository) { Record = record };
        var members = await service.RegistryAsync(workspace, ct).ConfigureAwait(false);
        return new(workspace, [.. members.Select(member => member.Repository).Order(StringComparer.Ordinal)], named, record?.Repository)
        {
            Record = record,
        };
    }

    /// <summary>This machine's record of <paramref name="session"/>; null where none is kept or it cannot be read.</summary>
    private async Task<Session?> RecordAsync(string session, CancellationToken ct)
    {
        if (ledger is null) return null;
        try
        {
            return await ledger.FindAsync(session, ct).ConfigureAwait(false) is { Origin: null } record ? record : null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return null;
        }
    }
}
