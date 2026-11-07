namespace Daoris.Driver;

/// <summary>
/// The considerations the loop's last look made, whole: the planner's verdicts <see cref="SessionGroups"/> reads, so the list says
/// what the loop decided rather than a second plan of its own.
/// </summary>
/// <remarks>Kept as <see cref="ParkedQuests"/> is, and replaced whole each look, so nothing older than the last look is read.</remarks>
public sealed class LastLook
{
    private volatile Looked? _looked;

    /// <summary>One look's verdicts and the cool-offs it held starts on, replaced together, and when.</summary>
    private sealed record Looked(IReadOnlyList<Consideration> Considered, IReadOnlyList<AccountWait> Waits, DateTimeOffset At);

    /// <summary>What the last look considered, in its order; null before any look, which is when a fresh plan is read instead.</summary>
    public IReadOnlyList<Consideration>? Latest => _looked?.Considered;

    /// <summary>When the last look recorded its verdicts: this Daoris's *when it last looked*; null before any look.</summary>
    public DateTimeOffset? At => _looked?.At;

    /// <summary>
    /// The starts the last look held on a cooling account: a record whose words such a start holds says its reset. And those it held
    /// on signed-out accounts with none cooling, with no reset. None before any look.
    /// </summary>
    public IReadOnlyList<AccountWait> Waits => _looked?.Waits ?? [];

    /// <param name="waits">The look's waits (<see cref="TickReport.Waits"/>); none where it held nothing on an account.</param>
    public void Record(IEnumerable<Consideration> considered, IEnumerable<AccountWait>? waits = null) =>
        _looked = new Looked([.. considered], [.. waits ?? []], DateTimeOffset.UtcNow);
}
