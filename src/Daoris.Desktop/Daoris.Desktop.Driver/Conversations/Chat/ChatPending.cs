using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A conversation's pending start (MODELROLE1d2, D205 §3.5(b), round 4): the owner of a Chat's or Ask Daoris's attempt from its
/// placement until it is dispatched or exhausted, keyed by the issued id, the id the press answered. Placement keeps its early return;
/// the asynchronous half that opens the door holds this. Refused at the open, that half ends the refused record
/// <c>choice-not-offered</c>, re-asks the walk with the refusing account passed and opens the replacement, in the same call: one press.
/// </summary>
/// <remarks>
/// <para><b>What it carries</b>: the effective order read once, the entry now asked, the passes so far, the account a pick named, and
/// how a replacement is opened (<see cref="Open"/>), the brief included where the start continues a line, since its words carry it.</para>
///
/// <para><b>Queued words are moved, not copied</b>: under this pending start's lock the refused record's list is handed out
/// (<see cref="ChatTurns.MoveOut"/>) before the replacement opens, and a word said meanwhile joins it (<see cref="Join"/>), so it reaches
/// the replacement exactly once. Re-keyed (<see cref="Moved"/>), the issued id answers for the current one.</para>
/// </remarks>
internal sealed class ChatPending
{
    private readonly object _gate = new();
    private readonly List<BuildPass> _passes = [];
    // Its records in the order they opened, the issued one first: a replacement moved to late never takes the current one back.
    private readonly List<string> _opened = [];
    private List<ChatMessage>? _moving;
    private volatile string _current;

    /// <param name="issued">The id the press answered.</param>
    /// <param name="actor">Whose order it walks: <c>chat</c> or <c>ask-daoris</c>.</param>
    /// <param name="order">The effective order its start read once; a pick's recovery order for a pick (<see cref="Recovering"/>).</param>
    /// <param name="entry">The entry its first open asked.</param>
    /// <param name="pinned">The account a pick named, which every attempt keeps; null walks each choice's accounts.</param>
    /// <param name="workspace">The workspace whose accounts it walks, and, for Ask Daoris, whose room it opens in.</param>
    /// <param name="open">Opens the replacement by a walk of its own over <paramref name="order"/>, linked to the refused record.</param>
    public ChatPending(
        string issued, string actor, IReadOnlyList<ActorEffective> order, ActorEffective entry, string? pinned, string? workspace,
        Func<ChatPending, CancellationToken, Task<ChatStart>> open)
    {
        Issued = issued;
        _current = issued;
        _opened.Add(issued);
        Actor = actor;
        Order = order;
        Entry = entry;
        Pinned = pinned;
        Workspace = workspace;
        Open = open;
    }

    public string Issued { get; }

    /// <summary>The record the start runs as now: the issued one, or the replacement it moved to.</summary>
    public string Current => _current;

    public string Actor { get; }

    public IReadOnlyList<ActorEffective> Order { get; }

    /// <summary>The entry its current attempt asks: moved by the same-account move (D205 §3.3) and by a replacement.</summary>
    public ActorEffective Entry { get; set; }

    public string? Pinned { get; }

    public string? Workspace { get; }

    /// <summary>How a replacement opens: Ask Daoris's run sets it, since only its run holds the machine's reading and its end's listener.</summary>
    public Func<ChatPending, CancellationToken, Task<ChatStart>> Open { get; set; }

    /// <summary>A pick, which is never walked afresh: only the picked agent's entries are tried (§3.5(d)).</summary>
    public bool Pick => Entry.From == "pick";

    /// <summary>The entry and account the last open refused, passed by the replacement's walk as if known before its spawn.</summary>
    public (ActorEffective Entry, string? Account)? Refusing { get; set; }

    /// <summary>What the re-ask answered where its answer is not the same session's: the replacement to open, or the wait's sentence.</summary>
    public ConversationWalk? Next { get; set; }

    /// <summary>Dispatched or exhausted: refusals are no longer this start's.</summary>
    public bool Over { get; private set; }

    /// <summary>Each entry passed so far in this press, the refused ones with their reason, which the replacement's actor line says.</summary>
    public IReadOnlyList<BuildPass> Passes
    {
        get
        {
            lock (_gate) return [.. _passes];
        }
    }

    public void Pass(BuildPass pass)
    {
        lock (_gate) _passes.Add(pass);
    }

    /// <summary>A replacement opened for it (<see cref="ChatPendings.Link"/>), after every record before it.</summary>
    public void Opened(string to)
    {
        lock (_gate)
        {
            if (!_opened.Contains(to, StringComparer.OrdinalIgnoreCase)) _opened.Add(to);
        }
    }

    /// <summary>
    /// The refused record's words begin moving: its turns handed them out, and each said meanwhile joins them. A replacement refused in
    /// its turn adds its own to those still moving.
    /// </summary>
    public void Moving(IReadOnlyList<ChatMessage> words)
    {
        lock (_gate) _moving = [.. _moving ?? [], .. words];
    }

    /// <summary>Whether its words are moving to a replacement now: a word said meanwhile joins them (<see cref="Join"/>).</summary>
    public bool IsMoving
    {
        get
        {
            lock (_gate) return _moving is not null;
        }
    }

    /// <summary>A word said while the replacement opens joins the moved words; false where nothing is moving now.</summary>
    public bool Join(ChatMessage word)
    {
        lock (_gate)
        {
            if (_moving is null) return false;
            _moving.Add(word);
            return true;
        }
    }

    /// <summary>
    /// Re-keyed to the replacement (<paramref name="to"/>): under the lock, every moved word is handed to <paramref name="say"/> in the order
    /// said and the issued id answers for the replacement from now on, so a word said as it moves either joined them or reaches it after.
    /// </summary>
    /// <returns>The words <paramref name="say"/> did not take.</returns>
    /// <remarks>
    /// A replacement already refused in its turn takes its words onward into this same pending start (<see cref="Join"/>): they stay moving,
    /// for its own replacement, as they do wherever another replacement is owed (<see cref="Next"/>).
    /// </remarks>
    public IReadOnlyList<ChatMessage> Moved(string to, Func<ChatMessage, bool> say)
    {
        lock (_gate)
        {
            // A replacement of this one, refused and moved on first, stays the current one.
            if (_opened.FindIndex(id => string.Equals(id, to, StringComparison.OrdinalIgnoreCase))
                > _opened.FindIndex(id => string.Equals(id, _current, StringComparison.OrdinalIgnoreCase)))
            {
                _current = to;
            }

            var words = _moving ?? [];
            _moving = [];
            var left = words.Where(word => !say(word)).ToList();
            if (_moving.Count == 0 && Next is null) _moving = null;
            return left;
        }
    }

    /// <summary>Exhausted, or the app closing: the moved words handed back, for the refused record to keep as waiting.</summary>
    public IReadOnlyList<ChatMessage> Exhausted()
    {
        lock (_gate)
        {
            var left = _moving ?? [];
            _moving = null;
            Over = true;
            return left;
        }
    }

    /// <summary>The start's door opened and carried its choice: dispatched.</summary>
    public void Dispatched() => Over = true;

    /// <summary>
    /// A pick's order for its every attempt (§3.5(d), round 4, Codex 2): the picked entry, then the actor's other entries for the same
    /// agent, each still a pick. Any other order as it is.
    /// </summary>
    public static IReadOnlyList<ActorEffective> Recovering(string actor, IReadOnlyList<ActorEffective> order, DriverConfig config)
    {
        if (order is not [{ From: "pick" } picked]) return order;
        IReadOnlyList<ActorEffective> stored;
        try
        {
            stored = ActorChoices.Effective(actor, config);
        }
        catch (ActorUnreadableException)
        {
            return order;
        }

        return
        [
            picked,
            .. stored.Where(each => string.Equals(each.Agent, picked.Agent, StringComparison.OrdinalIgnoreCase) && each.Model != picked.Model)
                .Select(each => each with { From = "pick" }),
        ];
    }
}

/// <summary>
/// The pending starts by id (MODELROLE1d2): each made at placement under its issued id, and each replacement it moved to linked to it, so a
/// door asked by the issued id is answered for the current one, as a conversation that went on in another is today
/// (<see cref="WordsNever.ContinuedIn"/>).
/// </summary>
internal sealed class ChatPendings
{
    private readonly ConcurrentDictionary<string, ChatPending> _by = new(StringComparer.OrdinalIgnoreCase);

    public ChatPending Add(ChatPending pending)
    {
        _by[pending.Issued] = pending;
        return pending;
    }

    /// <summary>The pending start an id belongs to, issued or a replacement's; null where none.</summary>
    public ChatPending? For(string id) => _by.TryGetValue(id, out var pending) ? pending : null;

    /// <summary>A replacement opened for <paramref name="pending"/>: its id belongs to the same start.</summary>
    public void Link(ChatPending pending, string to)
    {
        pending.Opened(to);
        _by[to] = pending;
    }

    /// <summary>The record a door asked by <paramref name="id"/> answers for: the current one of its pending start, else itself.</summary>
    public string Current(string id) => For(id)?.Current ?? id;
}
