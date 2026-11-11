namespace Daoris.Driver;

/// <summary>
/// A decision's life around the one walk: its prompt the shared one over the facts its kind composes (<see cref="SupervisorInstruction"/>),
/// its block kept through its kind's own keeping at the decision door, and every end that keeps nothing said on its line, where the subject
/// waits for the person.
/// </summary>
internal sealed class SuperviseDecisionLife(LoopContext context, ISupervisorDesk desk) : ISuperviseLife
{
    public Task<string?> PromptAsync(SupervisorWaiting waiting, ReviewerChoice choice, CancellationToken ct) =>
        SupervisorInstruction.ComposeAsync(new LookSupervisorWorld(context.Service, context.Home, context.Config), desk, waiting, ct);

    public async Task<string> SettleAsync(SuperviseRunAsk ask, SuperviseRunEnd end, CancellationToken ct)
    {
        var (waiting, log) = (ask.Waiting, context.Log);
        if (end.State == "stopped" || ct.IsCancellationRequested)
        {
            log?.Info("supervisor.dropped", ("role", waiting.Role), ("why", "the person answered first"));
            return SupervisorAttemptOutcomes.Failed;
        }

        var (block, why) = end.State == "completed" ? SupervisorBlock.Read(end.LastWords) : (null, $"its turn ended {end.State}");
        if (block is not null && (block.Kind != waiting.Kind || block.Subject != waiting.Subject)) (block, why) = (null, "its block names another subject");
        if (block is null)
        {
            log?.Info("supervisor.attempt-failed", ("role", waiting.Role), ("why", why ?? ""));
            Said(waiting.Subject.Session, $"The supervisor's turn did not decide this ({why}), so it waits for you.");
            return SupervisorAttemptOutcomes.Failed;
        }

        var effective = ask.Choice.Effective!;
        var holder = SupervisorHolders.OfAgent(new SupervisorAgentHolder(
            waiting.Role, ask.Revision, effective.Agent, effective.Model, effective.Effort, end.Ran, end.Posture, end.Session ?? "") { Tokens = end.Tokens });
        var kept = await desk.KeepAsync(waiting, block, holder, ct).ConfigureAwait(false);
        if (!kept.Kept)
        {
            log?.Info("supervisor.dropped", ("role", waiting.Role), ("why", kept.Why ?? ""));
            if (kept.Why == SupervisorWhy.RoleChanged)
            {
                Said(waiting.Subject.Session, "The supervisor's role changed while it was deciding, so it decides this again.");
                return SupervisorAttemptOutcomes.RoleChanged;
            }

            Said(waiting.Subject.Session, $"The supervisor's decision was not kept ({kept.Why}), so it waits for you.");
            return SupervisorAttemptOutcomes.Failed;
        }

        return block.Leave is null ? SupervisorAttemptOutcomes.Decided : SupervisorAttemptOutcomes.Left;
    }

    public void Unavailable(SupervisorWaiting waiting, string sentence) =>
        Said(waiting.Subject.Session, $"The supervisor's agents cannot run here now: {sentence} This waits for you.");

    private void Said(string session, string text) =>
        context.Events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = text }, say: null);
}
