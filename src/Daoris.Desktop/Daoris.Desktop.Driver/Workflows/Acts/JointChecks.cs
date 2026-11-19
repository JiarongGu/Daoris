namespace Daoris.Driver;

/// <summary>
/// Testing together as a joint constraint (D325, ACTORFLOW1d): a <c>task</c> step whose <c>joint</c> option maps each part of its run's ask's
/// plan to the fact that part's receipt gives (<c>{"app":"stage-ended","config":"config-written"}</c>), held by the tester or operations
/// role before the marked destination. It is handed to its holder once every part's newest receipt of its fact is reached
/// (<see cref="PartReceipts"/>), and again whenever a part acts after its last test; its own receipts settle it only on a test taken after
/// every part's newest receipt. A failed test naming the part it is on (the receipt's <c>part</c>) goes back to that part's holder
/// (<see cref="JointReturns"/>). Pure.
/// </summary>
public static class JointChecks
{
    /// <summary>The option mapping each part to the fact its receipt gives.</summary>
    public const string Option = "joint";

    /// <summary>The id a failure sent back to a part is journaled under on that part's run, before the failed receipt's own id.</summary>
    public const string Returned = "returned:";

    /// <summary>The facts <paramref name="node"/> holds over, by part; none for a step that is no joint check.</summary>
    public static IReadOnlyDictionary<string, string> Over(NodeDeclaration node) => StepReceipts.Map(node, Option);

    /// <summary>Each part's newest receipt of the fact <paramref name="node"/> holds over, null where its run journaled none or the plan is not read.</summary>
    public static IReadOnlyDictionary<string, StepActRow?> Receipts(string home, PartsReport? report, NodeDeclaration node) =>
        Over(node).ToDictionary(each => each.Key, each => report is null ? null : PartReceipts.Newest(home, report, each.Key, each.Value), StringComparer.Ordinal);

    /// <summary>The parts whose newest receipt is not reached: the check waits on them.</summary>
    public static IReadOnlyList<string> Missing(NodeDeclaration node, IReadOnlyDictionary<string, StepActRow?> parts) =>
        [.. Over(node).Keys.Where(part => parts.GetValueOrDefault(part) is not { State: StepReceipts.Reached })];

    /// <summary>Whether the check's own receipts (<paramref name="own"/>, on its attempt) pass it: each reached, after every part's newest receipt.</summary>
    public static bool Passed(NodeDeclaration node, IEnumerable<StepActRow> own, IReadOnlyDictionary<string, StepActRow?> parts)
    {
        if (Missing(node, parts).Count > 0) return false;
        var latest = Latest(parts);
        var rows = own.ToList();
        return StepReceipts.Mapped(node).Values.All(fact => rows.LastOrDefault(row => row.Fact == fact) is { State: StepReceipts.Reached } test && test.At >= latest!.At);
    }

    /// <summary>
    /// Whether the check is for its holder to take now: every part's receipt reached, and no test of its own taken since the newest of them (a
    /// failed test waits for the part's fix).
    /// </summary>
    public static bool Ready(NodeDeclaration node, IEnumerable<StepActRow> own, IReadOnlyDictionary<string, StepActRow?> parts)
    {
        if (Missing(node, parts).Count > 0) return false;
        var latest = Latest(parts)!;
        var facts = StepReceipts.Mapped(node).Values.ToHashSet(StringComparer.Ordinal);
        return !own.Any(row => facts.Contains(row.Fact) && row.At >= latest.At);
    }

    /// <summary>The newest of the parts' receipts: a test is of what stood then.</summary>
    public static StepActRow? Latest(IReadOnlyDictionary<string, StepActRow?> parts) =>
        parts.Values.OfType<StepActRow>().OrderBy(row => row.At).LastOrDefault();

    /// <summary>The parts' receipts the check passed over, as its evidence beside its own.</summary>
    public static string Evidence(IReadOnlyDictionary<string, StepActRow?> parts) =>
        string.Join("; ", parts.Where(each => each.Value is not null).Select(each => $"{each.Key}: {each.Value!.Message ?? each.Value.Url ?? each.Value.Fact}"));

    /// <summary>The hand-off's words for a joint check: the parts' receipts it holds over, and how a failure goes back.</summary>
    public static string Words(NodeDeclaration node) => Over(node) is { Count: > 0 } over
        ? $" It is the joint check over the parts' receipts ({string.Join(", ", over.Select(each => $"`{each.Key}`'s `{each.Value}`"))}): test them "
          + "together, and where the test fails name the part it is on in its receipt (`part`): Daoris asks that part's holder, and hands you the "
          + "check again on its fix."
        : "";

    /// <summary>The failure sent back to the part, open on <paramref name="rows"/> (a node's on its attempt) while no receipt reached after it.</summary>
    public static StepActRow? Open(IEnumerable<StepActRow> rows)
    {
        var listed = rows.ToList();
        var returned = listed.FindLastIndex(row => row.Id.StartsWith(Returned, StringComparison.Ordinal));
        return returned < 0 || listed.Skip(returned + 1).Any(row => row.State == StepReceipts.Reached) ? null : listed[returned];
    }

    /// <summary>What a part's fix came to, proposed into its delivery knowledge.</summary>
    public static string Lesson(StepActRow returned, StepActRow fix) =>
        $"The joint check `{returned.Fact}` failed on it ({returned.Message ?? returned.Fact}); its fix's `{fix.Fact}` reached: {fix.Message ?? fix.Url ?? fix.Fact}.";

    /// <summary>The joint check's newest test of its own on <paramref name="own"/> where it failed naming a part, or null.</summary>
    public static StepActRow? Failed(NodeDeclaration node, IEnumerable<StepActRow> own)
    {
        var facts = StepReceipts.Mapped(node).Values.ToHashSet(StringComparer.Ordinal);
        return own.LastOrDefault(row => facts.Contains(row.Fact)) is { State: not StepReceipts.Reached, Part: { } part } failed && Over(node).ContainsKey(part)
            ? failed
            : null;
    }
}
