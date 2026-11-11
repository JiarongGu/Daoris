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
/// One attempt of an agent supervisor at a subject: its order less the choices its attempts spent walked with the subject's writers; none
/// ready said once by its kind's life (<see cref="ISuperviseLife"/>), else the attempt written, its turn run, its end kept, its outcome written.
/// </summary>
internal sealed class SupervisePass(LoopContext context, Driver driver)
{
    /// <summary>The bound of one turn, in minutes.</summary>
    public const int Minutes = 10;

    private static readonly ConcurrentDictionary<SupervisorSubject, string> Unavailable = new();

    /// <summary>The attempt's start, or why none: the subject closed, an attempt running or ended, no choice ready, its attempt not written.</summary>
    public async Task<string?> StartAsync(SupervisorWaiting waiting, ISupervisorDesk desk, CancellationToken ct)
    {
        var life = new SuperviseDecisionLife(context, desk);
        var (ask, said, token) = await BeginAsync(waiting, life, ct).ConfigureAwait(false);
        if (ask is not null) _ = Task.Run(() => RunAsync(ask, life, token), CancellationToken.None);
        return said;
    }

    /// <summary>One attempt run to its end on the caller's own task, never the look's: whether a turn ran, and its start or why none.</summary>
    public async Task<(bool Ran, string? Said)> RunToEndAsync(SupervisorWaiting waiting, ISuperviseLife life, CancellationToken ct)
    {
        var (ask, said, token) = await BeginAsync(waiting, life, ct).ConfigureAwait(false);
        if (ask is null) return (false, said);
        await RunAsync(ask, life, token).ConfigureAwait(false);
        return (true, said);
    }

    private async Task<(SuperviseRunAsk? Ask, string? Said, CancellationToken Token)> BeginAsync(SupervisorWaiting waiting, ISuperviseLife life, CancellationToken ct)
    {
        var (attempts, runs) = (context.Supervisor.Attempts, context.Supervisor.Runs);
        var subject = waiting.Subject;
        if (context.Supervisor.Closed(subject) || runs.Running(subject) || attempts.Running(subject) || attempts.Ended(subject)) return (null, null, ct);
        if (runs.Lines.Contains(subject.Session)) return (null, null, ct);

        var repository = waiting.Repository is { Length: > 0 } named ? named : null;
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = new StartPlan(
                ParticipantWork.Supervise, context.Config, context.Harnesses, context.Adapters, new PassMemory(), catalogue: RoleCatalog.ReadHome(context.Home),
                place: new RolePlace(Repository: repository), role: waiting.Role) { Subject = ParticipantWork.Supervise }.Order;
        }
        catch (Exception error) when (error is ActorUnreadableException or DriverException)
        {
            return (null, SaidUnavailable(waiting, life, error.Message), ct);
        }

        var spent = attempts.Spent(subject);
        IReadOnlyList<ActorEffective> left = order.Where((_, at) => !spent.Contains(at + 1)).ToList();
        if (left.Count == 0) return (null, SaidUnavailable(waiting, life, "every choice of its order was tried and none decided it."), ct);
        if (waiting.PacketOnly)
        {
            var (held, passed) = PacketOnlyRules.Readers(context.Adapters, left);
            if (held.Count == 0) return (null, SaidUnavailable(waiting, life, $"no choice left of its order holds its reader packet-only ({string.Join(", ", passed)}: not enforced)."), ct);
            left = held;
        }

        var choice = await ChooseAsync(waiting, left, repository, ct).ConfigureAwait(false);
        if (!choice.Chosen || choice.Effective is not { } entry) return (null, SaidUnavailable(waiting, life, choice.Sentence), ct);

        Unavailable.TryRemove(subject, out _);
        var number = order.ToList().IndexOf(entry) + 1;
        if (await life.PromptAsync(waiting, choice, ct).ConfigureAwait(false) is not { } prompt) return (null, null, ct);
        if (attempts.Begin(subject, number < 1 ? spent.Count + 1 : number, DateTimeOffset.UtcNow) is not { } attempt) return (null, null, ct);
        if (runs.Start(subject, ct) is not { } source) return (null, null, ct);

        var revision = RoleCatalog.ReadHome(context.Home).RevisionOf(waiting.Role);
        var said = $"supervisor `{waiting.Role}` took {waiting.Kind} `{subject.Id}` of session {subject.Session} on {entry.Agent} · {entry.Model ?? "account default"}";
        return (new SuperviseRunAsk(waiting, choice, attempt, prompt, revision), said, source.Token);
    }

    private async Task RunAsync(SuperviseRunAsk ask, ISuperviseLife life, CancellationToken ct)
    {
        var (attempts, subject) = (context.Supervisor.Attempts, ask.Waiting.Subject);
        var outcome = SupervisorAttemptOutcomes.Failed;
        try
        {
            var end = context.SuperviseRuns is { } run
                ? await run(ask, ct).ConfigureAwait(false)
                : await new SuperviseTurn(context, driver).RunAsync(ask, ct).ConfigureAwait(false);
            outcome = end.State is "limited" or "transient" ? SupervisorAttemptOutcomes.Limited : await life.SettleAsync(ask, end, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is OperationCanceledException or DriverException or IOException or HttpRequestException)
        {
            context.Log?.Warn("supervisor.attempt-failed", ("role", ask.Waiting.Role), ("why", error.GetType().Name));
        }
        finally
        {
            // A subject left running because its outcome would not write would hold its whole line until a restart.
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

    private Task<ReviewerChoice> ChooseAsync(SupervisorWaiting waiting, IReadOnlyList<ActorEffective> left, string? repository, CancellationToken ct) =>
        context.SuperviseChoices is { } choose
            ? choose(waiting, left, ct)
            : ReviewerChoice.ChooseAsync(
                context.Harnesses, OpinionRule.None, waiting.Writers, context.Config, workspace: null, ct, actor: ParticipantWork.Supervise, order: left,
                catalogue: () => RoleCatalog.ReadHome(context.Home), role: waiting.Role, repository: repository);

    internal static void Forget(IReadOnlyCollection<SupervisorSubject> waiting)
    {
        foreach (var subject in Unavailable.Keys.Where(each => !waiting.Contains(each) && each.Kind != SupervisorKinds.AskDaoris)) Unavailable.TryRemove(subject, out _);
    }

    private string SaidUnavailable(SupervisorWaiting waiting, ISuperviseLife life, string sentence)
    {
        if (Unavailable.TryGetValue(waiting.Subject, out var said) && said == sentence) return $"supervisor `{waiting.Role}` unavailable: {sentence}";
        Unavailable[waiting.Subject] = sentence;
        context.Log?.Info("supervisor.unavailable", ("role", waiting.Role), ("kind", waiting.Kind), ("why", sentence));
        life.Unavailable(waiting, sentence);
        return $"supervisor `{waiting.Role}` unavailable: {sentence}";
    }
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
