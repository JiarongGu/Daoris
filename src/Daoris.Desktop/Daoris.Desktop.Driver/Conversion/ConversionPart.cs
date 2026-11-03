namespace Daoris.Driver;

/// <summary>
/// One area's part of the home's conversion (ONEWAY1ae; the oneway design §1.1): its census, its plan and digest, its effects, its Undo, all
/// through the one door. A part answers only for its own old shape; the door composes the parts, keeps their outcomes in one receipt and
/// applies them in <see cref="ConversionParts.Order"/>.
/// </summary>
public interface IConversionPart
{
    /// <summary>Its name in the plan and the receipt: one of <see cref="ConversionParts.Order"/>.</summary>
    string Name { get; }

    /// <summary>The census now. Throws <see cref="ConversionUnread"/> where its source does not answer.</summary>
    Task<PartCensus> CensusAsync(CancellationToken ct);

    /// <summary>The plan now under the row's choices; writes nothing. Throws <see cref="ConversionUnread"/> where its source does not answer.</summary>
    Task<PartPlan> PlanAsync(ConversionChoices choices, CancellationToken ct);

    /// <summary>Its entry in a new receipt from <paramref name="plan"/>: every operation pending, its <i>before</i> taken.</summary>
    PartReceipt Begin(PartPlan plan);

    /// <summary>A restart's read of its entry: each uncertain operation settled by its postcondition, never replayed blind.</summary>
    PartReceipt Settle(PartReceipt entry);

    /// <summary>
    /// Its converting operations, from where the entry stands, each outcome handed to <paramref name="keep"/> as it lands; the code its effect
    /// was refused by (<see cref="ConversionRefusals.Moved"/>), or null.
    /// </summary>
    (PartReceipt Entry, string? Refused) Convert(PartReceipt entry, Action<PartReceipt> keep);

    /// <summary>Its clears, run last and only once no part's converting operation is unsettled: finished history, never undone.</summary>
    PartReceipt Clear(PartReceipt entry, Action<PartReceipt> keep);

    /// <summary>Why its done entry cannot be undone (<c>moved: …</c>, naming the write that moved), or the lines Undo puts back.</summary>
    PartUndo UndoPlan(PartReceipt entry);

    /// <summary>Its done entry undone in one change. Throws <see cref="DriverException"/> <c>moved: …</c> where a write moved since.</summary>
    void Undo(PartReceipt entry);

    /// <summary>Set once this build deleted the part's own reads (its ONEWAY1af step): Undo would put back a shape it cannot read.</summary>
    string? ReadsGone => null;
}

/// <summary>A part's census: how many old records it holds, and whether it has ended (nothing old left to convert, hold or end).</summary>
public sealed record PartCensus(string Part, int Count, bool Ended);

/// <summary>A clear a part plans: finished history its store's remover takes, counted, dated and sized, and never undone.</summary>
public sealed record PartClear(string Said, int Count, DateTimeOffset? From = null, DateTimeOffset? To = null, long? Bytes = null);

/// <summary>
/// A part's plan: its own digest over its inputs, operations and choices; whether it writes nothing; its census; the lines the terminal says;
/// its clears; and <see cref="Detail"/>, the part's own plan for its review.
/// </summary>
public sealed record PartPlan(string Part, string Digest, bool Empty, PartCensus Census, IReadOnlyList<string> Lines, IReadOnlyList<PartClear> Clears)
{
    public object? Detail { get; init; }
}

/// <summary>A part the plan could not read, with why: its source did not answer.</summary>
public sealed record PartUnread(string Part, string Said);

/// <summary>Why a part's done entry is not undone, or the lines its Undo puts back.</summary>
public sealed record PartUndo(string? Refusal, IReadOnlyList<string> Lines)
{
    /// <summary>What is back once it is undone, said after the press.</summary>
    public string? Said { get; init; }
}

/// <summary>A part's source did not answer: the plan leaves the part out and says so, and an apply writes it <c>failed</c> in its receipt.</summary>
public sealed class ConversionUnread(string said) : Exception(said);

/// <summary>The parts' names, in the order the door applies them (the oneway design §1.1); the home part is never in a receipt with another.</summary>
public static class ConversionParts
{
    public const string Workflows = "workflows";

    private static readonly string[] Ordered = ["settings", "accounts", "staffing", "git", Workflows, "quests", "notes"];

    /// <summary>Apply order: every part's converting operations in this order, then every part's clears.</summary>
    public static IReadOnlyList<string> Order => Ordered;

    /// <summary>A part's place in <see cref="Order"/>; a name it does not hold goes last.</summary>
    public static int Rank(string part) => Array.IndexOf(Ordered, part) is var at and >= 0 ? at : Ordered.Length;

    /// <summary>The parts the door composes from <paramref name="doors"/>, in apply order.</summary>
    public static IReadOnlyList<IConversionPart> Of(ConvertDoors doors) => [.. doors.Others.OrderBy(each => Rank(each.Name))];
}
