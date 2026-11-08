using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>One turn of an agent supervisor as its runner is asked it: the subject, its choice, its attempt and its prompt.</summary>
public sealed record SuperviseRunAsk(SupervisorWaiting Waiting, ReviewerChoice Choice, SupervisorAttempt Attempt, string Prompt, long Revision);

/// <summary>
/// How the turn ended: <c>completed</c> with its last message, <c>failed</c> (a timeout among it), <c>limited</c> or <c>transient</c>
/// (a pass of the walk), or <c>stopped</c> (the person's word, or the driver's); its session, the posture that held it and its tokens.
/// </summary>
public sealed record SuperviseRunEnd(string State, string? LastWords, string? Session, string Posture, long? Tokens = null, string? Ran = null);

/// <summary>
/// One attempt of an agent supervisor at a subject: its order walked less the choices its attempts spent; with none ready, the subject
/// left to the person and said once; else the attempt written, its turn run, the block read from its last message and kept through its
/// kind's own keeping, and the outcome written beside the attempt. A limited or transient end is a pass, the next choice taken next.
/// </summary>
internal sealed class SupervisePass(LoopContext context, Driver driver)
{
    /// <summary>The bound of one turn, in minutes.</summary>
    public const int Minutes = 10;

    private static readonly ConcurrentDictionary<SupervisorSubject, string> Unavailable = new();

    /// <summary>The attempt's start, or why none: the subject closed, an attempt running or ended, no choice ready, its attempt not written.</summary>
    public async Task<string?> StartAsync(SupervisorWaiting waiting, ISupervisorDesk desk, CancellationToken ct)
    {
        var (attempts, runs) = (context.Supervisor.Attempts, context.Supervisor.Runs);
        var subject = waiting.Subject;
        if (context.Supervisor.Closed(subject) || runs.Running(subject) || attempts.Running(subject) || attempts.Ended(subject)) return null;
        if (runs.Lines.Contains(subject.Session)) return null;

        IReadOnlyList<ActorEffective> order;
        try
        {
            // The one walk's order of the supervisor the start is bound to, never the stored order read apart from it.
            order = new StartPlan(
                ParticipantWork.Supervise, context.Config, context.Harnesses, context.Adapters, new PassMemory(), catalogue: RoleCatalog.ReadHome(context.Home),
                place: new RolePlace(Repository: waiting.Repository), role: waiting.Role) { Subject = ParticipantWork.Supervise }.Order;
        }
        catch (Exception error) when (error is ActorUnreadableException or DriverException)
        {
            return SaidUnavailable(waiting, error.Message);
        }

        var spent = attempts.Spent(subject);
        var left = order.Where((_, at) => !spent.Contains(at + 1)).ToList();
        if (left.Count == 0) return null;
        var choice = await ChooseAsync(waiting, left, ct).ConfigureAwait(false);
        if (!choice.Chosen || choice.Effective is not { } entry) return SaidUnavailable(waiting, choice.Sentence);

        Unavailable.TryRemove(subject, out _);
        var number = order.ToList().IndexOf(entry) + 1;
        var world = new LookSupervisorWorld(context.Service, context.Home, context.Config);
        if (await SupervisorInstruction.ComposeAsync(world, desk, waiting, ct).ConfigureAwait(false) is not { } prompt) return null;
        if (attempts.Begin(subject, number < 1 ? spent.Count + 1 : number, DateTimeOffset.UtcNow) is not { } attempt) return null;
        if (runs.Start(subject, ct) is not { } source) return null;

        var revision = RoleCatalog.ReadHome(context.Home).RevisionOf(waiting.Role);
        var ask = new SuperviseRunAsk(waiting, choice, attempt, prompt, revision);
        _ = Task.Run(() => RunAsync(ask, desk, source.Token), CancellationToken.None);
        return $"supervisor `{waiting.Role}` took {waiting.Kind} `{subject.Id}` of session {subject.Session} on {entry.Agent} · {entry.Model ?? "account default"}";
    }

    private async Task RunAsync(SuperviseRunAsk ask, ISupervisorDesk desk, CancellationToken ct)
    {
        var (attempts, subject) = (context.Supervisor.Attempts, ask.Waiting.Subject);
        var outcome = SupervisorAttemptOutcomes.Failed;
        try
        {
            var end = context.SuperviseRuns is { } run
                ? await run(ask, ct).ConfigureAwait(false)
                : await new SuperviseTurn(context, driver).RunAsync(ask, ct).ConfigureAwait(false);
            outcome = await SettleAsync(ask, end, desk, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is OperationCanceledException or DriverException or IOException or HttpRequestException)
        {
            context.Log?.Warn("supervisor.attempt-failed", ("role", ask.Waiting.Role), ("why", error.GetType().Name));
        }
        finally
        {
            // The turn is freed even where its outcome cannot be written (the session's folder gone, a full disk): an attempt with no
            // outcome is closed failed at the bound, but a subject left running would hold its whole line until a restart.
            try
            {
                attempts.End(ask.Attempt, outcome, DateTimeOffset.UtcNow);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                context.Log?.Warn("supervisor.attempt-failed", ("role", ask.Waiting.Role), ("why", error.GetType().Name));
            }
            finally
            {
                context.Supervisor.Runs.Ended(subject);
            }
        }
    }

    // The block of a completed turn kept through its kind's row; anything else an outcome of its own, said.
    private async Task<string> SettleAsync(SuperviseRunAsk ask, SuperviseRunEnd end, ISupervisorDesk desk, CancellationToken ct)
    {
        var (waiting, log) = (ask.Waiting, context.Log);
        if (end.State is "limited" or "transient") return SupervisorAttemptOutcomes.Limited;
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

    private Task<ReviewerChoice> ChooseAsync(SupervisorWaiting waiting, IReadOnlyList<ActorEffective> left, CancellationToken ct) =>
        context.SuperviseChoices is { } choose
            ? choose(waiting, left, ct)
            : ReviewerChoice.ChooseAsync(
                context.Harnesses, OpinionRule.None, [], context.Config, workspace: null, ct, actor: ParticipantWork.Supervise, order: left,
                catalogue: () => RoleCatalog.ReadHome(context.Home), role: waiting.Role, repository: waiting.Repository);

    // No choice ready: the person's rules stand as they acted, and the subject waits for the person, said once until a choice is ready again.
    private string SaidUnavailable(SupervisorWaiting waiting, string sentence)
    {
        if (Unavailable.TryGetValue(waiting.Subject, out var said) && said == sentence) return $"supervisor `{waiting.Role}` unavailable: {sentence}";
        Unavailable[waiting.Subject] = sentence;
        context.Log?.Info("supervisor.unavailable", ("role", waiting.Role), ("kind", waiting.Kind), ("why", sentence));
        Said(waiting.Subject.Session, $"The supervisor's agents cannot run here now: {sentence} This waits for you.");
        return $"supervisor `{waiting.Role}` unavailable: {sentence}";
    }

    private void Said(string session, string text) =>
        context.Events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = text }, say: null);
}

/// <summary>The facts' world a look reads a subject from: the service, the home and its choices; no instance, no key.</summary>
internal sealed class LookSupervisorWorld(ServiceClient service, string home, DriverConfig config) : ISupervisorWorld
{
    public ServiceClient Service => service;

    public string Home => home;

    public DriverConfig Config => config;

    public InstanceWorld? Instance => null;

    public Func<string?> HolderKey => () => null;
}
