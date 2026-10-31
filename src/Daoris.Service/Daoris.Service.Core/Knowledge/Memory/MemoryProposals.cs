using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Knowledge;

/// <summary>A proposal published or joined: the row kept, the quest it travels on and whether it joined one; or why none was.</summary>
public sealed record MemoryProposed(MemoryOp? Op, Quest? Quest, bool Joined, string? Refused = null);

/// <summary>
/// A reader's proposal (KNOWCENTRAL2e; the memory coordinator design §4): a quest to the entry's owner, joined while nobody has taken it by
/// every further proposal from the same repository, each its own row and its own decision. The owner's session decides through its own
/// operations or declines; how the quest closes settles each proposal, which applies nothing. Settling is read from the quest and the
/// target at each look, so whichever door closed the quest, and on whichever machine, the rows say the same.
/// </summary>
public sealed class MemoryProposals(MemoryOpStore ops, QuestStore quests, QuestExchange? exchange = null, IMemoryEngines? engines = null)
{
    /// <summary>The kind and version of the packet a proposal quest's body carries.</summary>
    public const string PacketKind = "daoris.memory-proposal";

    public const int PacketVersion = 1;

    /// <summary>A test's seam: awaited after the look found no open quest, before the publish.</summary>
    internal Func<Task>? Looked { get; init; }

    /// <summary>
    /// <paramref name="change"/> on <paramref name="target"/>, proposed from <paramref name="from"/>: joined to the open quest from that
    /// repository carrying proposals to the owner, or published as a new one.
    /// </summary>
    public async Task<MemoryProposed> ProposeAsync(
        string from, MemoryReference target, MemoryChange change, MemoryAuthor by, DateTimeOffset now, CancellationToken ct = default)
    {
        var id = MemoryOpStore.NewId();
        // The look for an open quest and the publish or join are one step per pair, so two at one moment cannot both publish.
        using var turn = await ops.ProposingAsync(from, target.Repository, ct).ConfigureAwait(false);
        if (await OpenAsync(from, target.Repository, ct).ConfigureAwait(false) is { } open)
        {
            return new(await ops.ProposeAsync(id, target.Repository, change, by, open.Id, ct).ConfigureAwait(false), open, Joined: true);
        }

        if (Looked is { } looked) await looked().ConfigureAwait(false);
        if (exchange is null) return new(null, null, false, "No quest can be published from here, so nothing was proposed.");
        // A quest's id is read from its sender, receiver and title, so the title names its first proposal: a later quest is its own.
        var outcome = await exchange.PublishAsync(
            new QuestAsk(from, target.Repository, $"Memory proposals for `{target.Repository}` from `{from}` ({id})", Body(from, target, id, change, by))
            {
                Short = "Memory proposals",
                PublishedBy = by.Session,
                ByAgent = true,
            },
            now, ct).ConfigureAwait(false);
        if (outcome is not { Refusal: QuestPublishRefusal.None, Quest: { } quest }) return new(null, null, false, outcome.Message);
        return new(await ops.ProposeAsync(id, target.Repository, change, by, quest.Id, ct).ConfigureAwait(false), quest, Joined: false);
    }

    /// <summary>The open quest from <paramref name="from"/> to <paramref name="owner"/> that carries proposals; null where none does.</summary>
    private async Task<Quest?> OpenAsync(string from, string owner, CancellationToken ct)
    {
        var carriers = (await ops.ProposalsAsync(MemoryOpState.Awaiting, ct).ConfigureAwait(false))
            .Where(op => op.Repository == owner && op.Carried is not null)
            .Select(op => op.Carried!)
            .Distinct(StringComparer.Ordinal);
        foreach (var carried in carriers)
        {
            if (await quests.FindAsync(carried, ct).ConfigureAwait(false) is { Status: QuestStatus.Open } quest
                && string.Equals(quest.From, from, StringComparison.Ordinal) && string.Equals(quest.To, owner, StringComparison.Ordinal))
            {
                return quest;
            }
        }

        return null;
    }

    /// <summary>
    /// Every awaiting proposal settled where its quest closed (declined, or <c>answered</c> on a done, each with the owner's note), is
    /// gone (<c>dropped</c>), or its target moved past the revision read while it was open (<c>stale</c>).
    /// </summary>
    public async Task SettleAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var op in await ops.ProposalsAsync(MemoryOpState.Awaiting, ct).ConfigureAwait(false))
        {
            if (op.Carried is not { } carried) continue;
            var quest = await quests.FindAsync(carried, ct).ConfigureAwait(false);
            (string State, string? Answer)? settled = quest switch
            {
                null => (MemoryOpState.Dropped, null),
                { Status: QuestStatus.Declined } => (MemoryOpState.Declined, quest.Note),
                { Status: QuestStatus.Done } => (MemoryOpState.Answered, quest.Note),
                _ when await MovedAsync(op, ct).ConfigureAwait(false) => (MemoryOpState.Stale, null),
                _ => null,
            };
            if (settled is { } to) await ops.SettleProposalAsync(op.Id, to.State, to.Answer, now, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Whether the proposal's target is gone, ended or at another revision than the one its proposer read.</summary>
    private async Task<bool> MovedAsync(MemoryOp op, CancellationToken ct)
    {
        if (engines?.Open(op.Repository, MemoryReinforcement.None) is not { } engine) return false;
        try
        {
            return await engine.GetAsync(op.Change.Target ?? "", ct).ConfigureAwait(false) is not { Ended: null } entry
                   || entry.Revision != op.Change.Revision;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>The quest's body: what the owner's session does with it, then the first proposal's versioned packet.</summary>
    private static string Body(string from, MemoryReference target, string id, MemoryChange change, MemoryAuthor by)
    {
        var packet = new JsonObject
        {
            ["kind"] = PacketKind,
            ["version"] = PacketVersion,
            ["proposals"] = new JsonArray(Packet(id, target, change, by, from)),
        };
        return new StringBuilder()
            .Append($"`{from}` proposes changes to entries of `{target.Repository}`'s memory. Each proposal is its own decision, made by ")
            .Append("this repository's session through its own memory operations: a dispute or an amendment with memory_write, naming ")
            .Append("`relation` and `target`; an end with memory_end, which waits for its approver. Or decline this quest with your reason, ")
            .Append("which reaches each proposer. Closing it applies nothing itself; its handback names what you decided. Further proposals ")
            .Append($"from `{from}` join it until it is taken, and quest_list names every proposal it carries with its standing.")
            .AppendLine().AppendLine().AppendLine("```json")
            .AppendLine(packet.ToJsonString(new JsonSerializerOptions { WriteIndented = true }))
            .Append("```")
            .ToString();
    }

    private static JsonObject Packet(string id, MemoryReference target, MemoryChange change, MemoryAuthor by, string from) => new()
    {
        ["operation"] = id,
        ["target"] = target.ToString(),
        ["revision"] = change.Revision,
        ["change"] = change.Relation,
        ["reason"] = change.Text,
        ["citations"] = change.Recheck is { } cited ? JsonNode.Parse(cited) : new JsonArray(),
        ["proposer"] = new JsonObject
        {
            ["repository"] = from, ["session"] = by.Session, ["adapter"] = by.Adapter, ["role"] = by.Role,
        },
    };
}
