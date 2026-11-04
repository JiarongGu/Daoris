using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The wiring a test builds the chat runner's executors from without a runner: a fake runner's start, run and walk, the context, and the
/// executors the test adds, each reached by its type as the runner's are. A door the fake has no answer for is refused.
/// </summary>
internal sealed class ExecutorStand(IChatRunning runner, ChatContext context) : IExecutorWiring
{
    private readonly List<IExecutor> _executors = [];

    public ChatContext Context => context;

    /// <summary>An executor the others reach from now: the one the test made, handed back.</summary>
    public T Add<T>(T executor)
        where T : IExecutor
    {
        _executors.Add(executor);
        return executor;
    }

    public T Of<T>()
        where T : IExecutor => _executors.OfType<T>().Single();

    public bool Say(string sessionId, string message, IReadOnlyList<ChatUpload>? files = null, string? preface = null) =>
        throw new NotSupportedException();

    public bool Finish(string sessionId) => throw new NotSupportedException();

    public Task ConcludeAsync(string sessionId, string state, Noted note, Func<string, string, Task>? onEnded) =>
        throw new NotSupportedException();

    public RotatedOpening.Carried? LimitCarried(string? carries) => throw new NotSupportedException();

    Task<ChatStart> IChatRunning.StartAsync(
        string repository, string adapter, DriverConfig config, Func<string, string, Task>? onEnded, string? profile, bool? ownTree,
        CancellationToken ct, string? tree, ContinuedFrom? from, IReadOnlyList<ActorEffective>? order, EarlierMaker? leftBy,
        HarnessSelection? settled, bool picked, bool ownFirst, MoveWords? moves) =>
        runner.StartAsync(repository, adapter, config, onEnded, profile, ownTree, ct, tree, from, order, leftBy, settled, picked, ownFirst, moves);

    Task<HelpPlaced> IChatRunning.PlaceHelpAsync(
        string adapter, DriverConfig config, string? workspace, CancellationToken ct, string? carries, IReadOnlyList<ActorEffective>? order) =>
        runner.PlaceHelpAsync(adapter, config, workspace, ct, carries, order);

    Task<ChatStart> IChatRunning.RunHelpAsync(
        HelpPlaced placed, DriverConfig config, Func<CancellationToken, Task<HelpMachine?>> machine, Func<string, string, Task>? onEnded,
        CancellationToken ct) =>
        runner.RunHelpAsync(placed, config, machine, onEnded, ct);

    Task<ChatStart> IChatRunning.RunAsync(
        string actor, string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
        ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn, ActorEffective? chosen, ChatPending? pending) =>
        runner.RunAsync(actor, sessionId, message, resolved, selection, config, place, onEnded, ct, goOn, chosen, pending);

    ChatPlace IChatRunning.RepositoryPlace(
        DriverConfig config, IReadOnlyList<RepoView> registry, RepoView known, string repository, string workTree, IReadOnlyList<string>? continued,
        bool answers) =>
        runner.RepositoryPlace(config, registry, known, repository, workTree, continued, answers);

    ChatPlace IChatRunning.HelpPlace(string room, IReadOnlyList<HelpRead> reads, string? workspace) => runner.HelpPlace(room, reads, workspace);

    Task<bool> IChatRunning.AnswersFindingsAsync(string sessionId, string tree, DriverConfig config) =>
        runner.AnswersFindingsAsync(sessionId, tree, config);

    bool IChatRunning.Taking(string sessionId) => runner.Taking(sessionId);

    ChatMessage IChatRunning.WithProcess(string sessionId, ChatMessage message) => runner.WithProcess(sessionId, message);

    Task<ConversationWalk> IChatRunning.WalkAsync(
        string actor, IReadOnlyList<ActorEffective> order, DriverConfig config, string? workspace, string? profile, CancellationToken ct,
        (ActorEffective Entry, string? Account)? refused, bool counts, long except) =>
        runner.WalkAsync(actor, order, config, workspace, profile, ct, refused, counts, except);
}
