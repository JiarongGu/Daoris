namespace Daoris.Driver;

/// <summary>
/// A step cast to the role's agent handed to the run's working session: when a run's step waits on a task its holder's tool settles, or on
/// its role's act whose receipts settle it (<see cref="AgentSettles.Handed"/>), and an agent holds it, the run's newest session of this machine is told it as its next turn through the
/// say door, once per attempt and epoch, so a record awaiting the run's step wakes with it and an ended one goes on with it. The words say what
/// the step is, its outcomes, the evidence its settle gives and the tool that settles it (<see cref="StepSettles.Tool"/>).
/// </summary>
public static class StepHandOffs
{
    /// <summary>The key a hand-off is said under: told again under it, the say door answers with the word kept.</summary>
    public static string KeyOf(string run, string node, int attempt, int? epoch) => $"step-hand:{run}:{node}:{attempt}:{epoch ?? 0}";

    /// <summary>
    /// The session a run's step is handed to: the newest of this machine's records awaiting the run's step (<see cref="RunWaiters"/>), else the
    /// newest that ran the run's work quest (<paramref name="executors"/>, the work's own sessions), resumed where it ended; null where none.
    /// </summary>
    public static string? SessionOf(string run, IReadOnlyList<string> executors, IReadOnlyList<SessionRecord>? records)
    {
        if (records is not null && RunWaiters.Of(run, records) is { Count: > 0 } waiting) return waiting[^1];
        var own = records?.Where(record => !record.Teammate && executors.Contains(record.Id, StringComparer.Ordinal))
            .OrderBy(record => record.Updated).Select(record => record.Id).LastOrDefault();
        return own ?? executors.LastOrDefault();
    }

    /// <summary>
    /// Tells the run's working session each step <paramref name="governed"/> holds for an agent that its tool settles, once per attempt and epoch.
    /// </summary>
    /// <param name="sessions">This machine's session records, read only where a step is to hand.</param>
    /// <param name="say">The say door: the session, the words and their key.</param>
    /// <param name="told">The keys this process told already, asked again of the say door only after a restart, which answers with the word kept.</param>
    public static async Task HandAsync(
        NodeRecordFiles records, GovernedUnit governed, RunScope scope, Func<CancellationToken, Task<IReadOnlyList<SessionRecord>?>>? sessions, PlaceCastBook? casts,
        PlaceRiskBook? risk, Func<string, string, string, CancellationToken, Task<SayAnswer>>? say, List<string> said, CancellationToken ct, ISet<string>? told = null)
    {
        if (say is null || records.Read(governed.Unit.Id) is not { Ended: null } kept) return;
        var steps = HeldSteps.Of([governed], _ => kept, new StepTenureFiles(records.Home, records.HoldsLease), casts, risk)
            .Where(step => step.Handed).ToList();
        if (steps.Count == 0) return;
        var executors = (IReadOnlyList<string>)[.. scope.Quest?.Executors ?? [], .. scope.Branch?.Executors ?? []];
        var read = sessions is null ? null : await sessions(ct).ConfigureAwait(false);
        if (SessionOf(governed.Run, executors, read) is not { } session) return;
        foreach (var step in steps)
        {
            if (governed.Graph.Nodes.FirstOrDefault(node => node.Id == step.Node) is not { } node) continue;
            var attempt = WorkPerformer.StatusOf(kept, node.Id).Attempt;
            var key = KeyOf(governed.Run, node.Id, attempt, step.Epoch);
            if (told?.Contains(key) == true) continue;
            var entry = StepReceipts.Knowledge(node);
            var lessons = entry is null ? [] : StepLessons.Open(records.Home, scope.Run.Binding.Repository, entry, governed.Run);
            var answer = await say(session, Words(governed.Run, node, step, attempt, entry is null ? "" : StepLessons.Words(entry, lessons)), key, ct).ConfigureAwait(false);
            if (!answer.Kept) continue;
            StepLessons.Carry(records.Home, lessons, governed.Run);
            told?.Add(key);
            said.Add($"step {node.Id} of #{governed.Run} handed to session {session}");
        }
    }

    /// <summary>The step's words as its session reads them: what it is, its outcomes, its evidence, the tool that settles it, and <see cref="StepLessons.Words"/>.</summary>
    public static string Words(string run, NodeDeclaration node, HeldStep step, int attempt, string lessons = "")
    {
        var decision = TaskDecisions.Is(node);
        var outcomes = decision ? $" Answer with one outcome: {string.Join(", ", node.Outcomes.Select(each => $"`{each}`"))}." : " Settle it done when it is done.";
        var evidence = TaskEvidence.Required(node) is { Count: > 0 } names
            ? $" Give its evidence by name: {string.Join(", ", names.Select(each => $"`{each}`"))}."
            : "";
        var epoch = step.Epoch is { } held ? $", epoch {held}" : "";
        var settles = StepReceipts.Mapped(node).Count > 0
            ? $"{StepReceipts.Words(node)} "
            : $"{evidence} Do it, then settle it with `{StepSettles.Tool}` (run `{run}`, node `{node.Id}`{(step.Epoch is { } e ? $", epoch {e}" : "")}), with your words. ";
        var waits = step is { Why: PersonWaits.Actor, Words.Length: > 0 } ? $" Its plugin says: {step.Words}{(step.Url is { } url ? $" ({url})" : "")}" : "";
        return $"Daoris hands you step `{node.Id}` of run #{run} (attempt {attempt}{epoch}), waiting on `{step.Role}`, which your agent holds.{waits}"
               + $"{(StepReceipts.Mapped(node).Count > 0 ? "" : outcomes)}{StepReceipts.KnowledgeWords(node)}{lessons}{settles}"
               + "Where you cannot (a sign-in, an approval, a yes, a change another's to make), ask the role that can through `question_ask`, naming "
               + $"it (`role`: `{StepRoles.CredentialsKeeper}` for a sign-in, with its `site`) and, for an act, the act (`act`), and say so in your last "
               + "message. Then end your turn: the answer, the next step, or why the settle was refused, is your next turn.";
    }
}
