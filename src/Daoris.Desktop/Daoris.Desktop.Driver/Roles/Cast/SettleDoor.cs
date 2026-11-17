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

    /// <summary>
    /// Why the word <paramref name="by"/> gives is not taken on a step its tenure <paramref name="current"/> holds, or null where it is. A step
    /// whose attempt holds no tenure yet refuses nothing: the pass casts its holder at the next look.
    /// </summary>
    public static SettleRefusal? Admit(StepTenure? current, SettleBy by)
    {
        if (by.Who == SettleBy.Observed || current is null) return null;
        if (by.Epoch is { } epoch && epoch != current.Epoch) return new(StepTenureFiles.EndedEpoch, StepTenureFiles.Said(current));
        if (current.Why == TenureWhy.Marked && by.Who is not (CastHolders.Person or SettleBy.Owner))
        {
            return new(Marked, $"{StepTenureFiles.Said(current)}, the owner's mark keeping it the owner role's");
        }

        var admitted = by.Who switch
        {
            SettleBy.Owner => true,
            SettleBy.Line => current.Holder == CastHolders.Person,
            var who => Holds(current.Holder, who),
        };
        return admitted ? null : new(NotHolder, StepTenureFiles.Said(current));
    }

    /// <summary>
    /// Whether the speaker <paramref name="who"/> is the holder <paramref name="holder"/>: the same word, or an agent by its name where the
    /// role's staffed agent holds it.
    /// </summary>
    public static bool Holds(string holder, string who) =>
        holder == who || (holder == CastHolders.Agent && who.StartsWith(CastHolders.AgentPrefix, StringComparison.Ordinal));

    /// <summary>Whether a hand-over to <paramref name="target"/> is refused at a step the place's cast keeps the owner's (<see cref="CastWhy.Marked"/>).</summary>
    public static bool HandRefused(CastNow cast, string target) => cast.Why == CastWhy.Marked && target != CastHolders.Person;
}
