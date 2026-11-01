namespace Daoris.Driver;

/// <summary>A choice's reported tokens today (§2.2): what was reported, and whether a turn on it has not reported yet, which is never zero.</summary>
public sealed record StaffTokens(long Reported, bool Unreported);

/// <summary>
/// What <see cref="StaffJudge"/> arranges by (the staff policy design §3.1), all values, read by the ledger and the line before it asks.
/// </summary>
public sealed record StaffFacts
{
    /// <summary>The choices of the role's last new starts since its policy's <c>since</c>, oldest first; at most <see cref="StaffJudge.Window"/> are read.</summary>
    public IReadOnlyList<StaffKey> Window { get; init; } = [];

    /// <summary>This look's reservations by choice: a pending start counts in the window and against a budget.</summary>
    public IReadOnlyDictionary<StaffKey, int> Reserved { get; init; } = new Dictionary<StaffKey, int>();

    /// <summary>The role's new starts by choice in the machine's local day.</summary>
    public IReadOnlyDictionary<StaffKey, int> Today { get; init; } = new Dictionary<StaffKey, int>();

    /// <summary>The role's reported tokens by choice in the machine's local day.</summary>
    public IReadOnlyDictionary<StaffKey, StaffTokens> Tokens { get; init; } = new Dictionary<StaffKey, StaffTokens>();

    /// <summary>Each agent marked not answering, until when (§4.2).</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> NotAnswering { get; init; } = new Dictionary<string, DateTimeOffset>();

    /// <summary>Each choice this line moved off, when.</summary>
    public IReadOnlyDictionary<StaffKey, DateTimeOffset> MovedOff { get; init; } = new Dictionary<StaffKey, DateTimeOffset>();

    /// <summary>The moment the marks and the hold are read at.</summary>
    public DateTimeOffset Now { get; init; }
}
