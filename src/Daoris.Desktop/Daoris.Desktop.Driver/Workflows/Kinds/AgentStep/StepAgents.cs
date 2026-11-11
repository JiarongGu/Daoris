namespace Daoris.Driver;

/// <summary>
/// The port an agent-performed plugin step is sent to: a step whose manifest declares a <c>goal</c>. An attempt's ask is kept beside it, its
/// session started off the pass (<see cref="IStepSessions"/>) handed the plugin's <c>daoris-plugins</c> server for this step alone (its
/// tools and <c>step_answer</c>), and the answer read as the plugin's at the performer's reconcile. A session that ends unanswered, or an
/// ask a restart left with no session here, fails the step <see cref="StepAnswer.NoAnswer"/>.
/// </summary>
/// <param name="askOf">The ask of an attempt, composed from its run's record; null where the run names nothing to work from.</param>
/// <param name="program">The desktop's program the step's server runs as; null where none is found, and nothing is sent.</param>
/// <param name="wake">Asks a pass once an answer is kept.</param>
public sealed class StepAgents(
    string home, IStepSessions sessions, Func<string, NodeAttempt, NodeDeclaration, CancellationToken, Task<StepAgentAsk?>> askOf, string? program,
    Action? wake = null) : IStepAgents
{
    /// <summary>The key the door is kept under among the doors, beside the kinds' own.</summary>
    public const string Door = "plugin.step";

    private readonly NodeDoorRuns _runs = new();

    public async Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
    {
        var invocation = attempt.Call.Invocation;
        if (_runs.Runs(invocation)) return;
        if (program is null) throw new DriverException("the desktop's program that serves a step's tools is not found");
        var ask = await askOf(unit, attempt, node, ct).ConfigureAwait(false) ?? throw new DriverException($"{unit} names no work for `{node.Id}` to be performed on");
        var place = new PluginPlace(ask.Repository, ask.Workspace);
        ServerEntry Tools(string session) => PluginToolsHand.StepEntry(home, new PluginToolsAt(session, place, "en"), program, ask.Plugin, invocation);
        Directory.CreateDirectory(StepAnswer.Folder(home));
        AtomicFile.WriteText(StepAnswer.AskPath(home, invocation), ask.Write());
        _runs.Start(new StepAgentStart(invocation) { Authority = StepAuthorities.Of(unit, attempt) }, async _ =>
        {
            try
            {
                await sessions.RunAsync(ask, Tools, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                StepAnswer.Unanswered(home, invocation);
                wake?.Invoke();
            }
        }, _ => false);
    }

    public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct)
    {
        var invocation = attempt.Call.Invocation;
        if (StepAnswer.Read(home, invocation) is { } answer) return Task.FromResult(answer);
        if (!Holds(invocation)) return Task.FromResult(new WorkReconcileAnswer(ReconcileStates.NotStarted));
        if (_runs.Runs(invocation)) return Task.FromResult(new WorkReconcileAnswer(ReconcileStates.Running));
        StepAnswer.Unanswered(home, invocation);
        return Task.FromResult(StepAnswer.Read(home, invocation) ?? new WorkReconcileAnswer(ReconcileStates.Unknown));
    }

    public bool Answered(string invocation) => File.Exists(StepAnswer.AnswerPath(home, invocation));

    public bool Holds(string invocation) => File.Exists(StepAnswer.AskPath(home, invocation));

    public void Stop(string invocation) => sessions.Stop(invocation);

    public Task IdleAsync() => _runs.IdleAsync();
}
