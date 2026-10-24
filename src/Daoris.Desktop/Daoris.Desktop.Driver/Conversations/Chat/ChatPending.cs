namespace Daoris.Driver;

/// <summary>
/// A new conversation's start from its placement until it is dispatched or exhausted (MODELROLE1d2, D205 §3.5(b), round 4): what the
/// asynchronous half that opens its door holds, since the press that placed it has answered already (<c>START_HELP</c>, a Chat's
/// <c>RunAsync</c>). Keyed by the issued id, the one the press answered: refused at the open, the refused record ends
/// <c>choice-not-offered</c>, the walk is asked again with that account passed, and its answer is opened in the same call, the words said
/// meanwhile moved to it, never copied, and every door asked by the issued id answered for the current one.
/// </summary>
/// <remarks>
/// <para><b>The words move under one lock</b>: <see cref="Moving"/> takes the refused one's waiting words out of its turns and holds every
/// word said after, <see cref="Arrived"/> hands them to the replacement's turns in the order said, and <see cref="Take"/> judges a word
/// under the same lock, so a word said while the replacement opens reaches it exactly once.</para>
/// <para><b>A pick is never walked afresh</b> (§3.5(b), Codex 2): its order is the pick, and its alternatives are its agent's other entries on
/// the same account (<see cref="ActorChoices.PickAlternatives"/>); nothing else runs in its place.</para>
/// </remarks>
/// <param name="issued">The id the press answered.</param>
/// <param name="actor">The actor whose order it walks: <c>chat</c> or <c>ask-daoris</c>.</param>
/// <param name="order">The effective order its start read, once.</param>
/// <param name="workspace">The workspace its accounts are walked in.</param>
/// <param name="profile">The account the person picked, or null for the walk's.</param>
/// <param name="replace">Opens the replacement of the refused record named, on the walk's answer with this start's passes.</param>
internal sealed class ChatPending(
    string issued, string actor, IReadOnlyList<ActorEffective> order, string? workspace, string? profile,
    Func<ChatPending, string, Task<ChatStart>> replace)
{
    private readonly object _gate = new();
    private readonly List<(ActorEffective Entry, string Account)> _passed = [];
    private string _current = issued;
    private List<ChatMessage>? _moving;

    /// <summary>The id the press answered, which every door asked by it is answered for <see cref="Current"/>.</summary>
    public string Issued => issued;

    public string Actor => actor;

    public IReadOnlyList<ActorEffective> Order => order;

    public string? Workspace => workspace;

    public string? Profile => profile;

    /// <summary>Whether its start is the person's pick (§3.5(d)): its recovery never leaves the picked agent and account.</summary>
    public bool Picked => order is [{ From: "pick" }];

    /// <summary>The record its start is on now: the issued one, or the replacement it went on in.</summary>
    public string Current
    {
        get
        {
            lock (_gate) return _current;
        }
    }

    /// <summary>Whether its words are held here, between the refused record and its replacement.</summary>
    public bool Between
    {
        get
        {
            lock (_gate) return _moving is not null;
        }
    }

    /// <summary>The accounts this start passed for <paramref name="entry"/> at an open: the walk's per-start set (§4.1), never stored.</summary>
    public IReadOnlyCollection<string> PassedFor(ActorEffective entry)
    {
        lock (_gate) return [.. _passed.Where(each => each.Entry == entry).Select(each => each.Account)];
    }

    /// <summary>
    /// The entry refused at an open on <paramref name="account"/>, passed for the rest of this start, and the refused open's count, which
    /// is given back before its replacement is chosen.
    /// </summary>
    public void Refused(ActorEffective entry, string? account, long number)
    {
        lock (_gate)
        {
            _passed.Add((entry, account ?? AgentOffers.OwnHome));
            (RefusedEntry, RefusedOn, Number) = (entry, account, number);
        }
    }

    /// <summary>The entry last refused at an open, the account it was refused on, and that open's count.</summary>
    public ActorEffective? RefusedEntry { get; private set; }

    public string? RefusedOn { get; private set; }

    public long Number { get; private set; }

    /// <summary>
    /// What the refused record held to hand its first words (ASKHIST1: an earlier conversation's words), taken before its run let it go, so
    /// the replacement hands it instead.
    /// </summary>
    public ChatRunner.Handing? Handing { get; set; }

    /// <summary>Opens the replacement of <paramref name="refused"/>, on the walk's answer with this start's passes.</summary>
    public Task<ChatStart> ReplaceAsync(string refused) => replace(this, refused);

    /// <summary>
    /// A word said to this start, judged under its lock: held here while it moves, else handed by <paramref name="take"/> to the current
    /// record's turns.
    /// </summary>
    public TurnReach? Take(ChatMessage message, Func<string, TurnReach?> take)
    {
        lock (_gate)
        {
            if (_moving is null) return take(_current);
            _moving.Add(message);
            return TurnReach.TurnEnd;
        }
    }

    /// <summary>The refused record's turns give up their words (<paramref name="yield"/>, under this lock): from now on every word is held here.</summary>
    public void Moving(Func<IReadOnlyList<ChatMessage>> yield)
    {
        lock (_gate) _moving = [.. _moving ?? [], .. yield()];
    }

    /// <summary>
    /// The replacement's turns are there: the moved words go to them by <paramref name="into"/>, in the order said, and the start is on
    /// <paramref name="id"/> from now on. Nothing where nothing moves.
    /// </summary>
    public void Arrived(string id, Action<ChatMessage> into)
    {
        lock (_gate)
        {
            if (_moving is null) return;
            _current = id;
            foreach (var word in _moving) into(word);
            _moving = null;
        }
    }

    /// <summary>Nothing replaces it: the words that moved, handed back for the refused record to keep, and none held here after.</summary>
    public IReadOnlyList<ChatMessage> Exhausted()
    {
        lock (_gate)
        {
            var left = _moving ?? [];
            _moving = null;
            return left;
        }
    }
}
