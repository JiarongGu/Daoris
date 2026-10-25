namespace Daoris.Driver;

/// <summary>
/// The codes a standing choice's refusal is said in (PUSH1f2a, D194 point 4, the alone-path design §1.3): the page's catalogue keys,
/// which the shell's <c>Refusals</c> declares as these, and the driver's verbatim refusal for every other. One table, read by the
/// route that answers the window and by <c>daoris-driver driver</c> acting alone, so a refusal is one line both ways.
/// </summary>
public static class ChoiceCodes
{
    /// <summary>*Try again* asked of a quest the verdicts neither parked nor held by the person's stop.</summary>
    public const string QuestNotHeld = "QUEST_NOT_HELD";

    /// <summary>*Try again* asked of a quest a pause holds: Resume is the press that moves it.</summary>
    public const string QuestPaused = "QUEST_PAUSED";

    /// <summary>A keep rule out of its kind's bounds, or a choice its kind does not take (KEEP1d, the cleanup design §2.2).</summary>
    public const string KeepOutOfRange = "KEEP_OUT_OF_RANGE";

    /// <summary>The shell's <c>DRIVER_REFUSED</c>: any other refusal, said in its own sentence.</summary>
    public const string Refused = "DRIVER_REFUSED";

    /// <summary>Each check an operation refuses by, and the code it is said in.</summary>
    public static IReadOnlyDictionary<string, string> Table { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ChoiceRefusal.NotHeld] = QuestNotHeld,
        [ChoiceRefusal.Paused] = QuestPaused,
        [ChoiceRefusal.KeepOutOfRange] = KeepOutOfRange,
    };

    /// <summary>The code a refusal is said in: its check's, else the verbatim refusal.</summary>
    public static string Of(DriverException refused) =>
        refused is ChoiceRefusal coded && Table.TryGetValue(coded.Check, out var code) ? code : Refused;
}

/// <summary>
/// A standing choice's operation refusing by a named check (PUSH1f2a, as <see cref="ToolRefusal"/> carries a tool's): its sentence,
/// and the facts the window's catalogue says it with. <see cref="ChoiceCodes.Of"/> gives its code.
/// </summary>
public sealed class ChoiceRefusal(string check, string message, params (string Key, string Value)[] facts) : DriverException(message)
{
    /// <summary>The quest is neither parked nor held by the person's stop: nothing to try again.</summary>
    public const string NotHeld = "quest-not-held";

    /// <summary>The quest is paused, on its own or with its ask.</summary>
    public const string Paused = "quest-paused";

    /// <summary>A keep rule out of its kind's bounds (KEEP1d): its <c>kind</c>, <c>min</c> and <c>max</c> said with it.</summary>
    public const string KeepOutOfRange = "keep-out-of-range";

    public string Check { get; } = check;

    public IReadOnlyList<(string Key, string Value)> Facts { get; } = facts;
}

/// <summary>What *Try again* did to a quest (SESSUX1b): <c>marked</c> at its failures, or <c>released</c> from a stop's session.</summary>
public sealed record RetryDecision(string Quest, string Did, string? Session, Func<DriverConfig, DriverConfig> Change)
{
    public const string Marked = "marked";
    public const string Released = "released";
}

/// <summary>What a review's procedure look found: the repositories of a workspace whose checkout lacks it, and whether it was looked for.</summary>
public sealed record ReviewLooked(IReadOnlyList<string> Lacking, bool Unchecked);

/// <summary>
/// The standing choices' operations (PUSH1f2a, D194 point 4, the alone-path design §1.3): each the judgement a route made inside its
/// handler, taken out over the facts it judges by, so the window's route and <c>daoris-driver driver</c> acting alone call one
/// implementation. The rest of the choices are already <see cref="DriverConfig"/>'s <c>With*</c> and each rule's <c>Apply</c>; their
/// write is <see cref="Write"/>, which the route makes through its context with the loop's nudge.
/// </summary>
public static class StandingChoices
{
    /// <summary>A standing choice written: the file read, the change made, and saved under its lock (CONFIG1); the choices it wrote.</summary>
    /// <exception cref="DriverConfigUnreadableException">The file does not read, so nothing was written.</exception>
    public static DriverConfig Write(string path, Func<DriverConfig, DriverConfig> change)
    {
        var next = change(DriverConfig.Load(path));
        next.Save(path);
        return next;
    }
}
