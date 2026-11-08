namespace Daoris.Driver;

/// <summary>Which of the six choices a start takes.</summary>
public enum SlotChoices
{
    /// <summary>The line's own slot, still on its branch: no switch.</summary>
    Own = 1,

    /// <summary>The line's own last slot, free: switched back to its branch.</summary>
    Back = 2,

    /// <summary>A free detached slot.</summary>
    Detached = 3,

    /// <summary>A free slot whose line may come back.</summary>
    MayReturn = 4,

    /// <summary>A new slot, made by today's open.</summary>
    New = 5,

    /// <summary>A tree of the line's own, as today, off the pool's books.</summary>
    Past = 6,
}

/// <summary>Why the pool is not offered at all.</summary>
public enum SlotWithheld
{
    None,

    /// <summary>The start's confirmation is not settled, and a take waits for it.</summary>
    Unsettled,

    /// <summary>The repository tracks a submodule at the start's commit.</summary>
    Submodules,

    /// <summary>No job that ends a claim's git processes with its process.</summary>
    NoJob,

    /// <summary>A git without the hook command a take needs.</summary>
    OldGit,
}

/// <summary>One slot as the choice sees it.</summary>
/// <param name="Line">The line it serves; empty when it is detached.</param>
public sealed record SlotCandidate(int Number, string Line)
{
    public string State { get; init; } = KeptStates.Kept;

    /// <summary>Its HEAD is on <see cref="Line"/>'s branch.</summary>
    public bool OnItsBranch { get; init; }

    /// <summary>Nothing holds it (the judgement's first step).</summary>
    public bool Free { get; init; }

    /// <summary>Held only by its line's own work or a person's terminal there.</summary>
    public bool HeldOnlyByLine { get; init; }

    /// <summary>The judgement of its safety to be taken.</summary>
    public SlotVerdict Verdict { get; init; } = SlotVerdict.Passed;

    /// <summary>Its line is a closed quest's, a conversation not spoken to within the hour, or an unlanded line nobody holds.</summary>
    public bool MayComeBack { get; init; }

    /// <summary>Its line has a start due this look or a claim in flight.</summary>
    public bool LineBusy { get; init; }

    /// <summary>The moment its last line's base commit was made, newest first among detached slots.</summary>
    public DateTimeOffset? BaseAt { get; init; }

    /// <summary>The moment its line let it go, longest ago first among slots that may come back.</summary>
    public DateTimeOffset? LetGoAt { get; init; }
}

/// <summary>What the choice reads.</summary>
/// <param name="Line">The returning or new line.</param>
public sealed record SlotChoiceFacts(string Line, IReadOnlyList<SlotCandidate> Slots)
{
    /// <summary>The repository's count of slots; unset reads as none.</summary>
    public int Count { get; init; }

    /// <summary>The slot the line last let go.</summary>
    public int? LastSlot { get; init; }

    public SlotWithheld Withheld { get; init; }
}

/// <summary>The choice made: <see cref="Number"/> is the slot, or the number a new slot takes, and null for a tree past the pool.</summary>
public sealed record SlotChosen(SlotChoices Choice, int? Number, SlotWithheld Withheld = SlotWithheld.None);

/// <summary>
/// The choice of a slot for a start, pure: the first of six that stands. A slot is chosen only where the pool is offered, only if it is kept
/// (not retiring or stuck) and, but for the line's own, only if the judgement found it free and clean.
/// </summary>
public static class SlotChoice
{
    public static SlotChosen Choose(SlotChoiceFacts facts)
    {
        if (facts.Withheld != SlotWithheld.None || facts.Count <= 0) return new(SlotChoices.Past, null, facts.Withheld);

        // The line's own slot may be retiring: a retirement takes no new line, and this one is not new.
        if (facts.Slots.FirstOrDefault(each => each.Line == facts.Line && each.OnItsBranch && each.State != KeptStates.Stuck && (each.Free || each.HeldOnlyByLine))
            is { } own)
        {
            return new(SlotChoices.Own, own.Number);
        }

        if (facts.LastSlot is { } last && Takeable(facts.Slots.FirstOrDefault(each => each.Number == last), facts.Line) is { } back)
        {
            return new(SlotChoices.Back, back.Number);
        }

        var detached = facts.Slots.Where(each => each.Line.Length == 0 && Takeable(each, facts.Line) is not null)
            .OrderByDescending(each => each.BaseAt ?? DateTimeOffset.MinValue).ThenBy(each => each.Number).FirstOrDefault();
        if (detached is not null) return new(SlotChoices.Detached, detached.Number);

        var comeBack = facts.Slots.Where(each => each.Line.Length > 0 && each.MayComeBack && Takeable(each, facts.Line) is not null)
            .OrderBy(each => each.LetGoAt ?? DateTimeOffset.MaxValue).ThenBy(each => each.Number).FirstOrDefault();
        if (comeBack is not null) return new(SlotChoices.MayReturn, comeBack.Number);

        return facts.Slots.Count < facts.Count
            ? new(SlotChoices.New, facts.Slots.Select(each => each.Number).DefaultIfEmpty(0).Max() + 1)
            : new(SlotChoices.Past, null);
    }

    /// <summary>The slot where it is kept, free, judged clean and its present line, if another's, not busy; else null.</summary>
    private static SlotCandidate? Takeable(SlotCandidate? slot, string line) =>
        slot is { State: KeptStates.Kept, Free: true, Verdict.Clean: true } && (slot.Line.Length == 0 || slot.Line == line || !slot.LineBusy) ? slot : null;
}
