namespace Daoris.Driver;

/// <summary>
/// What today's look says about a stalled timer's subject, read as the watchdog marks it stalled: the comparer's other side.
/// One port, so each look's reader joins without the watchdog; one a look has no reader for says <see cref="BusLookStates.Unknown"/>.
/// </summary>
public interface IBusLook
{
    /// <summary>The look's reading of <paramref name="timer"/>'s subject now; never throws for a reading it cannot make, which is unknown.</summary>
    Task<BusLookReading> ReadAsync(BusTimer timer, CancellationToken ct);
}

/// <summary>A look's reading: whether it holds the obligation open or handled, and a code saying what it read.</summary>
public sealed record BusLookReading(string State, string? Says)
{
    public static readonly BusLookReading None = new(BusLookStates.Unknown, "no-reader");
}

/// <summary>What a look says of a subject the bus says is past due.</summary>
public static class BusLookStates
{
    /// <summary>The look holds it open too (still working, still waiting): the two agree it is unmet, and only the bus said it past due.</summary>
    public const string Open = "open";

    /// <summary>The look says it is handled: the bus missed its acknowledgement, or its deadline or ends are wrong.</summary>
    public const string Handled = "handled";

    /// <summary>The look has no reading of it: counted apart, never as an agreement.</summary>
    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = [Open, Handled, Unknown];
}

/// <summary>
/// The comparer's verdict on one stall beside its look's reading, counted by kind and kept with both sides on the stall itself: what step 3
/// reads before any look is deleted.
/// </summary>
public static class BusCompare
{
    public const string Agree = "agree";

    public const string Disagree = "disagree";

    public const string Unknown = "unknown";

    /// <summary>The verdict of a stall whose look read <paramref name="look"/>.</summary>
    public static string Verdict(string? look) => look switch
    {
        BusLookStates.Open => Agree,
        BusLookStates.Handled => Disagree,
        _ => Unknown,
    };
}
