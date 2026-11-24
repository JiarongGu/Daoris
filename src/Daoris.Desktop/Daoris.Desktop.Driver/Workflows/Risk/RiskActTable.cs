namespace Daoris.Driver;

/// <summary>Who answers a <c>role</c> row at a place: the role and the holder the place's cast gives it.</summary>
public sealed record RiskAnswer(string Role, string Holder);

/// <summary>
/// One row of the act table at a place: a kind of the census, its class, where its rule comes from, who answers it by the cast, the last seven
/// days' passes and gates at the place, and whether it is locked (<c>policy.change</c>, which no row changes).
/// </summary>
public sealed record RiskActRow(string Kind, string Class, string? By, string Source, string Reason, RiskAnswer? Answers, int Passes, int Gates, bool Locked);

/// <summary>The act table at a place, with the policy it reads.</summary>
public sealed record RiskActsAt(string Place, string Mode, string Revision, string? Fault, IReadOnlyList<string> Unknown, IReadOnlyList<RiskActRow> Rows);

/// <summary>
/// The act table, read once and shown twice: the place's <i>Risk and your marks</i> row in the window and
/// <c>daoris settings risk</c> print the same <see cref="RiskActRow"/>s, so they cannot disagree on a home. Per kind: its class there, the rule it
/// comes from, who answers by the cast, seven days of the judge's passes and gates at the place, <c>policy.change</c> locked; a pending edit's
/// waits are previewed by <see cref="Preview"/> before it is saved.
/// </summary>
public static class RiskActTable
{
    /// <summary>How far back the passes and gates are counted.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    /// <summary>The table at the place <paramref name="workspace"/> and <paramref name="repository"/> name (both none: the machine).</summary>
    public static RiskActsAt Of(DriverConfig config, string home, DateTimeOffset now, string? workspace, string? repository)
    {
        var policy = RiskPolicy.Of(config);
        var place = RiskPlace.Of(config, workspace, repository);
        var risk = config.PlaceRisk.At(workspace, repository);
        var casts = new PlaceCastFiles(home).Book();
        var held = RiskJournal.Read(home).Where(line => line.Stage == RiskJudged.Opened && line.At > now - Window && line.At <= now && At(line, workspace, repository)).ToList();
        var rows = RiskGate.Table(policy, place).Select(verdict =>
        {
            var seen = held.Where(line => line.Kind == verdict.Kind).ToList();
            RiskAnswer? answers = verdict.By is { } role ? new(role, casts.At(role, workspace, repository, risk).Holder) : null;
            return new RiskActRow(
                verdict.Kind, verdict.Class, verdict.By, verdict.Source, verdict.Reason, answers,
                seen.Count(line => line.Outcome == RiskVerdict.Pass), seen.Count(line => line.Outcome != RiskVerdict.Pass), verdict.Kind == RiskActs.PolicyChange);
        }).ToList();
        return new(PlaceRiskBook.KeyOf(workspace, repository), place.Mode, policy.Revision, policy.Fault, policy.Unknown, rows);
    }

    /// <summary>The rows alone, as the window reads them.</summary>
    public static IReadOnlyList<RiskActRow> Rows(DriverConfig config, string home, DateTimeOffset now, string? workspace, string? repository) =>
        Of(config, home, now, workspace, repository).Rows;

    /// <summary>The waits <paramref name="edits"/> would open, release or move at <paramref name="place"/>, judged before and after, nothing written.</summary>
    public static IReadOnlyList<PolicyShift> Preview(DriverConfig config, string place, string? workspace, IReadOnlyList<PolicyEdit> edits)
    {
        var before = RiskPolicy.Of(config);
        if (before.Fault is not null || edits.Any(edit => edit.Fault() is not null)) return [];
        return PolicyChanges.Shifts(config, PolicyChanges.AfterConfig(config, edits), before, PolicyChanges.After(config, edits), edits, _ => place.StartsWith("repository/", StringComparison.Ordinal) ? workspace : null);
    }

    /// <summary>The table as <c>daoris settings risk</c> prints it.</summary>
    public static IReadOnlyList<string> Lines(RiskActsAt table)
    {
        var lines = new List<string>
        {
            table.Fault is { } fault
                ? $"  The risk policy does not read ({fault}): every act reads `owner` until it is mended."
                : $"  Each act at {table.Place} under {table.Mode} access, by the risk policy (revision {table.Revision}); passes and gates are of the last seven days:",
        };
        foreach (var row in table.Rows)
        {
            var by = row.By is { } role ? $", by `{role}`" : "";
            var answers = row.Answers is { } answer ? $" → {answer.Holder}" : "";
            var from = row.Locked ? ", locked" : row.Source switch
            {
                RiskVerdict.Seed => row.Kind == RiskActs.CheckNarrow ? ", said in one line" : ", the seed",
                RiskVerdict.Fixed => ", fixed",
                RiskVerdict.Unreadable => "",
                var key => $", set at `{key}`",
            };
            lines.Add($"    {row.Kind.PadRight(17)}{row.Class}{by}{answers}{from}  ({row.Passes} passed, {row.Gates} gated)");
        }

        if (table.Unknown.Count > 0) lines.Add($"  Rows naming no act this build knows, not read: {string.Join(", ", table.Unknown)}.");
        return lines;
    }

    private static bool At(RiskJudged line, string? workspace, string? repository) =>
        (repository is null || string.Equals(line.Repository, repository, StringComparison.OrdinalIgnoreCase))
        && (workspace is null || string.Equals(line.Workspace, workspace, StringComparison.OrdinalIgnoreCase));
}
