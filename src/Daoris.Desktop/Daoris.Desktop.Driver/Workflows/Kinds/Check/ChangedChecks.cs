namespace Daoris.Driver;

/// <summary>
/// A check held <c>changed</c> (its work's own change alters a command the person has not accepted) is run again by itself once the
/// acceptance moves: each pass words its held attempt's <i>Run again</i> (<see cref="HeldResolutions.Again"/>), the one way a held check is
/// asked again, which the same pass applies. Its result keeps the blob accepted when it held (<see cref="VerifyResult.Accepted"/>); a result
/// naming none is run again once any blob is accepted. Under the same acceptance it would only hold again, and with none accepted nothing
/// would run, so in both it waits.
/// </summary>
public static class ChangedChecks
{
    /// <summary>
    /// Every held <c>changed</c> check with no word on it whose acceptance moved since it held, worded <i>Run again</i> at
    /// <paramref name="now"/>; <paramref name="acceptedOf"/> gives a unit's acceptance as its check would run under it now, or null where
    /// none is accepted or the unit is no run whose check reads it.
    /// </summary>
    public static IReadOnlyList<WorkPerformed> Again(NodeRecordFiles records, Func<string, string?> acceptedOf, DateTimeOffset now)
    {
        var done = new List<WorkPerformed>();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(Holds).ToList())
            {
                if (acceptedOf(record.Unit) is not { } accepted) continue;
                var result = CheckResults.Newest(records.Home, CheckExecution.RunOf(record.Unit), record, attempt.Node);
                if (result is not { Code: VerifyResult.Changed } || string.Equals(result.Accepted, accepted, StringComparison.Ordinal)) continue;
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
