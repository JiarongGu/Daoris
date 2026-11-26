namespace Daoris.Driver;

/// <summary>
/// A check red past its rounds (<see cref="CheckRounds.Bound"/>) as the risk policy decides it: a left attempt's recovery, the act
/// <c>work.recover</c> at the run's place. Its failure stays kept on the role a left attempt waits on (<see cref="HeldOn"/>, the place's
/// supervisor). Classed <c>role</c>, that role's holder as the place casts it holds it, its agent where nothing casts it, and the words name
/// that role and holder: an agent no live session serves makes it a vacancy (<see cref="StepVacancies"/>), listed, announced and past its
/// bound at the final exit, and one a live session serves but never answers reaches the exit past the same bound. Classed <c>owner</c>, or
/// with no policy read, the person holds it as their own. Where the policy asks nobody (the seed passes it) it reaches the owner's final exit
/// (<see cref="FinalExits"/>) at once: one notice with the rounds, the reds and what the session tried, one <c>risk.judged</c> line, and its row
/// in <i>What needs you</i> until the owner looks. Daoris never stops it as its own. The decision is kept in the same write as the work's
/// telling, so neither stands without the other, and it is judged again at each look until it reaches the exit: a policy
/// lowered or raised moves it, and a cast removed moves its holder.
/// </summary>
internal static class SpentChecks
{
    public static RiskVerdict? Verdict(DriverConfig? config, string? workspace, string? repository, NodeDeclaration? check = null) =>
        RiskStepGates.Verdict(RiskStepGates.At(config, workspace, repository), RiskActs.WorkRecover, check);

    /// <summary>The role a spent red's failure is kept on, which a role class names and its holder holds: the left attempt's (<see cref="StepWaiting.RoleOf"/>).</summary>
    public static string HeldOn => StepWaiting.RoleOf(PersonWaits.Left);

    /// <summary>
    /// Who the verdict gives a spent red: the role on a gate, held by its cast or else its agent, as every wait an agent holds is; the person
    /// where it is the owner's or nothing is read; the final exit where the policy asks nobody.
    /// </summary>
    public static string Decide(RiskVerdict? verdict) => verdict switch
    {
        { Outcome: RiskVerdict.Gate } => NodeTold.RoleHeld,
        { Outcome: RiskVerdict.Pass } => NodeTold.Exit,
        _ => NodeTold.OwnerHeld,
    };

    /// <summary>The words the work's session is told: the red, then who holds it now.</summary>
    public static string Words(string node, string? code, string? found, int reds, string decided, CastNow? holder) =>
        NodeRunKeeping.FailedWords(node, code, found) + $"\n\nThis is its red round {reds}, past the {CheckRounds.Bound} Daoris sends back on its own, so " + decided switch
        {
            NodeTold.RoleHeld when holder is { Why: CastWhy.Default } =>
                $"the role `{HeldOn}` holds it now, its agent's as no cast names a holder; if no session of it answers it, it is listed under Vacant and goes to the owner as Daoris's final exit past its bound",
            NodeTold.RoleHeld => $"the role `{HeldOn}` holds it now, held by {holder?.Holder}",
            NodeTold.Exit => "nobody else holds it and it goes to the owner as Daoris's final exit",
            _ => "the person sees it too",
        } + ": commit a fix and it is checked again, or, if you cannot make it pass, say so and give up with what you tried.";

    /// <summary>The final exit's story: the rounds, the red classes, what the session tried, and that the policy asks nobody else.</summary>
    public static string Notice(string run, string node, int reds, string? code, string? found, RiskVerdict? verdict, DateTimeOffset? first, DateTimeOffset now)
    {
        var last = found?.Trim().ReplaceLineEndings("\n").Split('\n')[0] is { Length: > 0 } line ? $"; the last red: {line}" : "";
        var held = new List<FinalExits.ExitHolder>
        {
            new("the work's session", first,
                $"each of its {reds} red rounds{(code is { } failed ? $" (`{failed}`)" : "")} went back to it as its next turn, and the check is still red{last}"),
            new("the risk policy", null, $"it asks nobody to recover it here ({verdict?.Why ?? "no rule"}), so no role holds it"),
        };
        return FinalExits.Notice($"run #{run}: step `{node}` is red past its {CheckRounds.Bound} rounds", held, "mend it and run the check again, or end this work", now);
    }

    /// <summary>
    /// The spent red decided and told, in that order: who holds it, the work's session told through <paramref name="tell"/>, then the decision
    /// kept on the run's record with its telling in one write. A decision kept already is judged again: where it moved it is kept anew and
    /// the session is not told again; one at the exit stays there. At the final exit the pass is journaled and its notice handed up once,
    /// only once kept (<paramref name="noticed"/>).
    /// </summary>
    /// <param name="tell">Tells the work's session the words: the session told and where the words reach it, or why none is.</param>
    /// <returns>Whether the decision is kept; a refused write keeps neither, and both are tried again at the next look.</returns>
    public static async Task<bool> SettleAsync(
        NodeRecordFiles records, string unit, string run, string node, int attempt, int reds, string? code, string? found, DriverConfig? config, string? workspace,
        string? repository, DateTimeOffset now, Func<string, Task<(string? Session, string? Why, string? Reaches)>> tell, List<string> said, List<LookLine> noticed,
        NodeDeclaration? check = null)
    {
        var verdict = Verdict(config, workspace, repository, check);
        var decided = Decide(verdict);
        var record = records.Read(unit);
        var before = record?.ToldOf(node, attempt) is { Spent: true } spent ? spent : null;
        // Judged again at its release: the same decision, or one at the exit, which is the person's from then on, moves nothing.
        if (before?.Decided is { } was && (was == decided || was == NodeTold.Exit)) return true;
        var holder = decided == NodeTold.RoleHeld ? CastReading.Now(records.Home, workspace, repository)(HeldOn) : null;
        var first = record?.Told.FirstOrDefault(each => each.Node == node)?.At;
        // Told already, by an older build that kept no decision or under the decision judged again now: decided now, never told twice.
        var (session, why, reaches) = before is not null ? (before.Session, before.Why, before.Reaches) : await tell(Words(node, code, found, reds, decided, holder)).ConfigureAwait(false);
        var told = (before ?? new NodeTold(node, attempt, session, why, now) { Reaches = reaches }) with { Spent = true, Decided = decided, Role = decided == NodeTold.RoleHeld ? HeldOn : null };
        var (refused, _) = records.Edit(unit, kept => (kept with { Told = [.. kept.Told.Where(each => each.Node != node || each.Attempt != attempt).TakeLast(NodeRecord.ToldKept - 1), told] }, true));
        var key = $"{records.Home}|{unit}/{node}#{attempt}";
        if (refused is not null)
        {
            if (Refusals.TryAdd(key, 0))
            {
                said.Add($"run #{run}: step `{node}` is past its red rounds, and its decision could not be kept ({refused}), so Daoris decides it again at each look.");
            }

            return false;
        }

        Refusals.TryRemove(key, out _);
        if (decided != NodeTold.Exit || verdict is null) return true;
        var place = RiskPlace.Of(config, workspace, repository);
        try
        {
            RiskJournal.Append(records.Home, new RiskJudged(now, RiskJudged.Released, $"spent:{run}/{node}#{reds}", PersonWaits.Left, verdict.Kind, verdict.Qualifier,
                RiskPolicy.Key, place.Workspace, place.Repository, place.Mode, run, node, $"run {run}, step {node}, red round {reds}", verdict.Outcome, verdict.Class,
                verdict.By, verdict.Source, verdict.Revision, verdict.Reason, Today, true));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The journal records the exit, never a reason to keep it from the owner: the decision is kept.
        }

        noticed.Add(FinalExits.Line(run, node, attempt, workspace, Notice(run, node, reds, code, found, verdict, first, now)));
        return true;
    }

    /// <summary>The spent reds whose decision a refused write left unkept, each said once until it is kept.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> Refusals = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What today's gate did with a spent red at the final exit, as its <c>risk.judged</c> line says it.</summary>
    public const string Today = "the owner's final exit";
}
