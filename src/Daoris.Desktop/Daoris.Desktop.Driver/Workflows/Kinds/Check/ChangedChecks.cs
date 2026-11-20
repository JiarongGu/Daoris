namespace Daoris.Driver;

/// <summary>What a unit's check would run under now: its acceptance (null where none) and whether a command change waits (<see cref="DeclarationMode.Waits"/>).</summary>
public sealed record ChecksStanding(string? Accepted, bool Waits);

/// <summary>
/// A check held <c>changed</c> (its work's own change alters a command the person has not accepted) is run again by itself once the
/// acceptance moves or its repository takes full access: each pass words its held attempt's <i>Run again</i> (<see cref="HeldResolutions.Again"/>),
/// which the same pass applies. Its result keeps the blob accepted when it held (<see cref="VerifyResult.Accepted"/>), and one naming none
/// runs once any is accepted; under the same acceptance in restricted access, or with none accepted, it waits.
/// </summary>
public static class ChangedChecks
{
    /// <summary>
    /// Every held <c>changed</c> check with no word on it whose repository takes full access or whose acceptance moved since it held,
    /// worded <i>Run again</i> at <paramref name="now"/>; <paramref name="standingOf"/> gives a unit's standing, or null where the unit is no
    /// run whose check reads it.
    /// </summary>
    public static IReadOnlyList<WorkPerformed> Again(NodeRecordFiles records, Func<string, ChecksStanding?> standingOf, DateTimeOffset now)
    {
        var done = new List<WorkPerformed>();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(Holds).ToList())
            {
                if (standingOf(record.Unit) is not { Accepted: not null } standing) continue;
                var result = CheckResults.Newest(records.Home, CheckExecution.RunOf(record.Unit), record, attempt.Node);
                if (result is not { Code: VerifyResult.Changed } || (standing.Waits && string.Equals(result.Accepted, standing.Accepted, StringComparison.Ordinal))) continue;
                var (_, worded) = records.Edit<bool>(record.Unit, current =>
                    current.Attempts.FirstOrDefault(each => each.Node == attempt.Node && each.Attempt == attempt.Attempt) is { } held && Holds(held)
                        ? (WorkPerformer.With(current, held, held with { Resolution = HeldResolutions.Again, ResolvedAt = now }), true)
                        : (null, false));
                if (worded) done.Add(new WorkPerformed(record.Unit, attempt.Node, PerformedWhat.Worded));
            }
        }

        return done;
    }

    private static bool Holds(NodeAttempt attempt) =>
        attempt is { Plugin: NodeKinds.Check, State: AttemptStates.Held, Code: VerifyResult.Changed, Resolution: null };
}
