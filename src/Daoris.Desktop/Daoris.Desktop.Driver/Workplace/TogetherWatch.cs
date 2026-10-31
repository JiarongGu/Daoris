namespace Daoris.Driver;

/// <summary>
/// The look's pass over the operations bringing sessions together (LINESTOGETHER1d, the review-flow design §4): one whose sessions
/// were deleted is dropped; one at <i>landing</i> or <i>uncertain</i> is settled under its head tree's landing hold, which a landing
/// under way keeps, then the record lock; every other open one is stepped (<see cref="TogetherLook"/>) by what the records say of its
/// head now.
/// </summary>
/// <param name="config">The settings the line's accepted rules are read from; none reads no rule.</param>
/// <param name="keep">Where a member's note is kept once its operation lands after a restart: the session and the event.</param>
internal sealed class TogetherWatch(string home, Func<DateTimeOffset> now, Func<DriverConfig>? config = null, Action<string, SessionEvent>? keep = null)
{
    /// <summary>A repository's line as its checkout reads it: the branch a landing replays from; null where none is known.</summary>
    internal delegate Task<string?> LineOf(string root, string repository, CancellationToken ct);

    private static readonly HashSet<string> HandOvers = new(StringComparer.Ordinal)
    {
        HandOverWhy.Press, HandOverWhy.Near, HandOverWhy.Limit, HandOverWhy.Agent, HandOverWhy.Curve,
    };

    /// <summary>Each operation as this look left it; none where none is kept.</summary>
    /// <exception cref="IOException">The operations do not read.</exception>
    public async Task<IReadOnlyList<TogetherOperation>> LookAsync(IReadOnlyList<SessionRecord> records, LineOf lineOf, CancellationToken ct)
    {
        var store = new TogetherOperations(home);
        var all = store.All();
        if (all.Count == 0) return [];
        var byId = records.GroupBy(each => each.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var looked = new List<TogetherOperation>(all.Count);
        foreach (var operation in all)
        {
            if (Deleted(operation, byId))
            {
                store.Drop(operation.Id);
                continue;
            }

            if (!operation.IsOpen || await TreeGates.CheckoutOfTreeAsync(operation.Head.Tree, ct).ConfigureAwait(false) is not { } root)
            {
                looked.Add(operation);
                continue;
            }

            var line = await lineOf(root, operation.Repository, ct).ConfigureAwait(false);
            if (operation.Phase is TogetherPhase.Landing or TogetherPhase.Uncertain)
            {
                var workspace = byId.TryGetValue(operation.Head.Record, out var head) ? head.Workspace : RemoteTarget.DefaultWorkspace;
                looked.Add(await SettleAsync(root, operation, ct, workspace, line).ConfigureAwait(false) ?? operation);
                continue;
            }

            if (FactsOf(operation, byId, line) is not { } facts)
            {
                looked.Add(operation);
                continue;
            }

            // Only what the person accepted for integration settles a path by rule, as the landing's replay reads it.
            var rules = config is not null && operation.Phase == TogetherPhase.Checking
                ? await LineRules.AcceptedAsync(config(), root, operation.Repository, facts.LineTip, ct).ConfigureAwait(false)
                : null;
            looked.Add(await new TogetherLook(home, now).StepAsync(root, operation.Id, facts, rules, ct).ConfigureAwait(false) ?? operation);
        }

        return looked;
    }

    /// <summary>
    /// A landing attempt settled by its records and its git effects, while no landing of the head's tree is under way; null where one
    /// is, or nothing moved.
    /// </summary>
    internal async Task<TogetherOperation?> SettleAsync(
        string root, TogetherOperation operation, CancellationToken ct, string workspace = RemoteTarget.DefaultWorkspace, string? line = null)
    {
        if (operation.Attempt is not { } attempt) return null;
        using var hold = await LandingHolds.TakeAsync(home, operation.Head.Tree, ct, TimeSpan.Zero).ConfigureAwait(false);
        if (hold is null) return null;
        var integration = new IntegrationStore(home);
        var evidence = await TogetherSettle.ReadAsync(root, operation, each => Recorded(home, operation.Repository, each),
            integration.Request, integration.Of, ct).ConfigureAwait(false);
        var decided = TogetherSettle.Decide(attempt, evidence);
        // Git made the branch and the process ended before its record: the record is written now, the members listed in it.
        if (decided == TogetherSettled.Landed) TogetherSettle.WriteMissed(home, operation, evidence, workspace, line, now());
        var settled = new TogetherOperations(home).Settle(operation.Id, decided, now(), evidence.Request?.Id);
        if (settled is { Phase: TogetherPhase.Landed } && keep is not null)
        {
            foreach (var brought in TogetherInclusion.Of(settled)) keep(brought.Session, LandingRules.BroughtIntoNote(brought));
        }

        return settled;
    }

    /// <summary>
    /// Whether a landing record or advance of the attempt's target was written at or after it; null where the records do not read.
    /// </summary>
    internal static bool? Recorded(string home, string repository, TogetherAttempt attempt)
    {
        if (attempt.Form == TogetherForms.Merge) return false;
        try
        {
            return new LandedBranches(home).All().Any(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                && string.Equals(each.Branch, attempt.Target, StringComparison.Ordinal)
                && (each.LandedAt >= attempt.At || each.Advances.Any(advance => advance.At >= attempt.At)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// What the records say of the head now: the record its line ends at, whether its own keyed word opened that one (a successor of
    /// the head's record opened by words while the operation was still <i>asked</i>, its word waiting there), why it moved otherwise,
    /// and whether its turn runs. Null where the head's record is not read.
    /// </summary>
    internal static TogetherFacts? FactsOf(TogetherOperation operation, IReadOnlyDictionary<string, SessionRecord> records, string? line)
    {
        if (line is null || !records.TryGetValue(operation.Head.Record, out var record)) return null;
        var at = record.Head is { Length: > 0 } head && records.TryGetValue(head, out var ahead) ? ahead : record;
        var moved = !string.Equals(at.Id, operation.Head.Record, StringComparison.OrdinalIgnoreCase);
        var handed = at.FromWhy is { } why && HandOvers.Contains(why);
        var byOwnWord = moved && operation.Phase == TogetherPhase.Asked && !handed
            && string.Equals(at.From, operation.Head.Record, StringComparison.OrdinalIgnoreCase);
        return new TogetherFacts(at.Id, byOwnWord, moved && handed ? TogetherWhy.HandedOver : null,
            at.Live && at.State != "awaiting-person", line);
    }

    /// <summary>Whether the head's line or a member's session is gone from the records: deleted, nothing of it to bring together.</summary>
    internal static bool Deleted(TogetherOperation operation, IReadOnlyDictionary<string, SessionRecord> records) =>
        (!records.ContainsKey(operation.Head.Record) && !records.ContainsKey(operation.Head.Line))
        || operation.Members.Any(member => !records.ContainsKey(member.Session));
}
