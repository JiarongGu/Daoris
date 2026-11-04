namespace Daoris.Driver;

/// <summary>
/// A conversation started on words: those a session could not go on with (<see cref="StartFrom"/>), or a new conversation's first
/// (<see cref="StartWith"/>), each opened through the runner's own start and handed its first message there.
/// </summary>
[Executor("start-from", ExecutorPhase.Start)]
internal sealed class WordsStart(IExecutorWiring wiring) : IExecutor
{
    // Made at the first start that carries a request id, so a runner that is never asked one writes nothing under the home.
    private StartRequests? _starts;

    /// <summary>
    /// <see cref="StartFrom.RunAsync"/> on the adapter and account a new conversation takes. The record the words were written to goes
    /// on nowhere meanwhile: a word said to it while this runs waits rather than reopening it under the words handed on.
    /// </summary>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    /// <param name="order">Chat's effective order, walked where it holds more than one entry; null runs on <paramref name="adapter"/>.</param>
    public async Task<StartedFrom> StartFromAsync(
        string sessionId, string adapter, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct,
        IReadOnlyList<ActorEffective>? order)
    {
        var context = wiring.Context;
        if (context.Closing) return new StartedFrom(null, false, null, "the application is closing, so no conversation starts now.");

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!context.GoingOn.TryAdd(sessionId, going.Task))
        {
            return new StartedFrom(null, false, StartFrom.Running, $"conversation `{sessionId}` is going on with its words already.");
        }

        try
        {
            return await StartFrom.RunAsync(
                context.Service, context.Home, context.Events, context.Output, sessionId,
                (repository, moves, token) => wiring.StartAsync(repository, adapter, config, onEnded, ct: token, order: order, moves: moves),
                SayShown, ct).ConfigureAwait(false);
        }
        finally
        {
            context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary>
    /// Open a conversation on its first words, then hand it the words and files. A request id makes a retry answer the first start
    /// (<see cref="StartRequests"/>).
    /// </summary>
    /// <param name="ownTree">Absent is the driver's choice; true a tree of its own; false the checkout.</param>
    /// <exception cref="DriverException">The request id is malformed.</exception>
    public Task<StartedFrom> StartWithAsync(
        string repository, string adapter, DriverConfig config, string text, IReadOnlyList<ChatUpload> files, string? requestId,
        bool? ownTree, string? profile, Func<string, string, Task>? onEnded, CancellationToken ct, IReadOnlyList<ActorEffective>? order)
    {
        Task<StartedFrom> Run() => wiring.Context.Closing
            ? Task.FromResult(new StartedFrom(null, false, null, "the application is closing, so no conversation starts now."))
            : StartWith.RunAsync(
                token => wiring.StartAsync(
                    repository, adapter, config, onEnded, profile, ownTree, token, order: order, picked: !string.IsNullOrWhiteSpace(profile)),
                (id, words, attached, preface) => wiring.Say(id, words, attached, preface),
                text, files, ct);

        if (requestId is null) return Run();
        _starts ??= new StartRequests(wiring.Context.Home);
        return _starts.RunAsync(requestId, Run);
    }

    /// <summary>
    /// A new conversation's first message, showing the words its open moved each under its id (<see cref="ChatContext.Shown"/>): held for
    /// the turn that writes it, which records them before the write. A text-only door records no turn, so nothing is held for one.
    /// </summary>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    private bool SayShown(string sessionId, string message, IReadOnlyList<ChatUpload> files, string preface, IReadOnlyList<SessionEvent>? shown)
    {
        var context = wiring.Context;
        if (shown is not null && context.Turned.ContainsKey(sessionId)) context.Shown[sessionId] = (message, shown);
        var took = false;
        try
        {
            return took = wiring.Say(sessionId, message, files, preface);
        }
        finally
        {
            if (!took && shown is not null) context.Shown.TryRemove(sessionId, out _);
        }
    }
}
