namespace Daoris.Driver;

/// <summary>
/// Who a word on a run's step is from, as <see cref="SettleDoor"/> admits it: a holder by its channel's name (<see cref="CastHolders"/>: the
/// person at the window or the terminal, an agent), in the epoch its channel read; the owner role's own act; a supervisor's held decision;
/// or an observation.
/// </summary>
/// <param name="Who">A holder's word (<c>person</c>, <c>agent</c>, <c>agent:&lt;name&gt;</c>), or <see cref="Owner"/>, <see cref="Line"/> or <see cref="Observed"/>.</param>
/// <param name="Epoch">The epoch of the tenure its channel read; null names none, and is judged against the holder now.</param>
public sealed record SettleBy(string Who, int? Epoch = null)
{
    /// <summary>The working session whose own press the word is, whatever holder it speaks as; null for every other channel.</summary>
    public string? Session { get; init; }

    /// <summary>
    /// The owner role, the top of every line: its end of the work (a stop) and its answer to a decision, which confirms or overrules the
    /// proposal of the decision's holder. Held by the person, so the person's channels speak it for these two acts.
    /// </summary>
    public const string Owner = "owner";

    /// <summary>A supervisor's decision on the step (<see cref="NodeWordBy"/>), held for the owner's overrule: the line answering in its holder's place.</summary>
    public const string Line = "line";

    /// <summary>A fact observed by a plugin, or Daoris's own reading of its records: it settles the step whoever made it.</summary>
    public const string Observed = "observed";

    public static SettleBy Observation { get; } = new(Observed);

    /// <summary>The person's word at the window or the terminal, in the epoch their press read.</summary>
    public static SettleBy Person(int? epoch = null) => new(CastHolders.Person, epoch);

    /// <summary>
    /// Who a run step's word is from by its channel, where the channel names no other: a supervisor's (<paramref name="by"/>) the line's; a stop,
    /// and a decision's answer naming no proposer (<paramref name="holder"/>), the owner role's; any other the person's, a proposal among them,
    /// whose proposer is the record's words and never who speaks.
    /// </summary>
    public static SettleBy Of(string act, NodeWordBy? by, string? holder, int? epoch) =>
        by is not null ? new(Line, epoch)
        : act == NodeWordActs.Stop || (act == NodeWordActs.Decide && string.IsNullOrWhiteSpace(holder)) ? new(Owner, epoch)
        : Person(epoch);

    /// <summary>Ask Daoris's own agent, as its presses speak: never the person, whose words it acts on.</summary>
    public const string AskDaoris = CastHolders.AgentPrefix + "ask-daoris";

    /// <summary>
    /// Who a press speaks as by the channel it came by (<see cref="TerminalTierBy"/>), or null for the person's own (the window, a card the
    /// person confirmed, their terminal), which the act makes the person's or the owner role's (<see cref="Of"/>): a working session's own press
    /// is the agent its start ran, by the name its channel keeps (<paramref name="agent"/>, <c>agent:&lt;name&gt;</c>), Ask Daoris's its own
    /// agent's, and an appointed role's act an agent's. A channel that keeps no name a holder takes speaks the unnamed <c>agent</c>. A stop or a
    /// decision from an agent is its holder's word, never the owner role's.
    /// </summary>
    public static SettleBy? OfChannel(string by, bool appointed, int? epoch, string? agent = null, string? session = null) =>
        appointed ? new(CastHolders.Agent, epoch) { Session = session }
        : by == TerminalTierBy.Session
            ? new(agent is { } name && CastHolders.Is(CastHolders.AgentNamed(name)) ? CastHolders.AgentNamed(name) : CastHolders.Agent, epoch) { Session = session }
        : by == TerminalTierBy.AskDaoris ? new(AskDaoris, epoch)
        : null;
}

/// <summary>Why the door does not take a word: its code, and who holds the step now.</summary>
public sealed record SettleRefusal(string Code, string Detail);

/// <summary>
/// The one door a run step's settling word passes: a word is taken from the holder the step's tenure names now
/// (<see cref="StepTenureFiles"/>), in its epoch, or from an observation; the owner role ends the work and answers a decision whoever holds
/// it; a supervisor's held decision stands in for a step the person holds. A step at a destination the owner marked
/// (<see cref="TenureWhy.Marked"/>) is the owner role's alone. It reads the tenure, the speaker and the mark, never what the step is: what
/// settles a step is its kind's, who may say it is its holder's.
/// </summary>
public static class SettleDoor
{
    /// <summary>The word is from a holder that does not hold the step now.</summary>
    public const string NotHolder = "not-holder";

    /// <summary>The step is at a destination the owner marked: the owner role holds it, and no cast or hand-over moves it.</summary>
    public const string Marked = "marked";

    /// <summary>A role the owner holds (the owner role itself) is handed to nobody but the person.</summary>
    public const string OwnerRole = "owner-role";

    public const string OwnWork = "own-work";

    private static readonly string[] Yes = [NodeWordActs.Accept, NodeWordActs.Approve, NodeWordActs.Skip, NodeWordActs.Decide, NodeWordActs.Done];

    /// <summary>
    /// Why a yes to the hold <paramref name="why"/> is refused: its author, the working session its channel proved, is one of the work's
    /// <paramref name="makers"/>, or may be while they are not read. A step that is work and not a hold, and every other channel, pass.
    /// </summary>
    public static SettleRefusal? Own(NodeDeclaration node, string? why, string act, SettleBy by, IReadOnlyCollection<string>? makers) =>
        by.Session is not { } session || why is null || RiskStepHolds.KindOf(why, node) is null || !Yes.Contains(act, StringComparer.Ordinal) ? null
        : makers is null ? new(OwnWork, $"session {session} speaks for a working session, and the work's makers are not read yet")
        : makers.Contains(session, StringComparer.Ordinal) ? new(OwnWork, $"session {session} is one of the work's makers")
        : null;

    /// <summary>
    /// Why the word <paramref name="by"/> gives is not taken on a step its tenure <paramref name="current"/> holds, or null where it is. An
    /// observation, and the owner role's end of the work and answer to a decision, are taken whoever holds the step and in whichever epoch
    /// their row read. A step whose attempt holds no tenure yet refuses nothing: the pass casts its holder at the next look. Where the
    /// tenures do not read (<paramref name="unread"/>), who holds the step cannot be judged, so only the person's word is taken, whose press
    /// would take the step: a marked step's holder, and the top of every line.
    /// </summary>
    public static SettleRefusal? Admit(StepTenure? current, SettleBy by, bool unread = false)
    {
        if (by.Who is SettleBy.Observed or SettleBy.Owner) return null;
        if (unread) return by.Who == CastHolders.Person ? null : new(StepTenureFiles.Unread, "its holder cannot be read: the step's tenures do not read");
        if (current is null) return null;
        if (by.Epoch is { } epoch && epoch != current.Epoch) return new(StepTenureFiles.EndedEpoch, StepTenureFiles.Said(current));
        if (current.Why == TenureWhy.Marked && by.Who != CastHolders.Person)
        {
            return new(Marked, $"{StepTenureFiles.Said(current)}, the owner's mark keeping it the owner role's");
        }

        var admitted = by.Who == SettleBy.Line ? current.Holder == CastHolders.Person : Holds(current.Holder, by.Who);
        return admitted ? null : new(NotHolder, StepTenureFiles.Said(current));
    }

    /// <summary>
    /// Whether the speaker <paramref name="who"/> is the holder <paramref name="holder"/>: the same word, or an agent by its name where the
    /// role's staffed agent holds it.
    /// </summary>
    public static bool Holds(string holder, string who) =>
        holder == who || (holder == CastHolders.Agent && who.StartsWith(CastHolders.AgentPrefix, StringComparison.Ordinal));

    /// <summary>
    /// Why a hand-over to <paramref name="target"/> is refused at a step the place's cast keeps the owner's, or null: <see cref="Marked"/> at a
    /// destination the owner marked, <see cref="OwnerRole"/> for the owner role (a role-less step's), each handed to nobody but the person.
    /// </summary>
    public static string? HandRefused(CastNow cast, string target) =>
        target == CastHolders.Person ? null
        : cast.Why == CastWhy.Marked ? Marked
        : cast.Why == CastWhy.Owner ? OwnerRole
        : null;
}
