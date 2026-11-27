namespace Daoris.Driver;

/// <summary>
/// The port a plugin step declaring a <c>goal</c> is sent to: its ask kept, its session started on the door runner (<see cref="IStepSessions"/>)
/// with the plugin's <c>daoris-plugins</c> server for this step alone, its answer read as the plugin's. A session refused for its account's
/// sign-in goes on once on the walk's next account; with none left the step fails <see cref="StepAnswer.SignIn"/> (<see cref="StepSignIns"/>).
/// Any other unanswered end, or an ask a restart left with no session here, fails it <see cref="StepAnswer.NoAnswer"/>.
/// </summary>
/// <param name="askOf">The ask of an attempt, composed from its run's record; null where the run names nothing to work from.</param>
/// <param name="program">The desktop's program the step's server runs as; null where none is found, and nothing is sent.</param>
/// <param name="wake">Asks a pass once an answer is kept, where no bus log is held for the home to carry the turn's end as a fact (<see cref="StepBusFacts.StepTurnEnded"/>).</param>
public sealed class StepAgents(
    string home, IStepSessions sessions, Func<string, NodeAttempt, NodeDeclaration, CancellationToken, Task<StepAgentAsk?>> askOf, string? program,
    Action? wake = null) : IStepAgents, IDoorRuns
{
    /// <summary>The key the door is kept under among the doors, beside the kinds' own.</summary>
    public const string Door = "plugin.step";

    private readonly NodeDoorRuns _runs = new();

    NodeDoorRuns IDoorRuns.Runs => _runs;

    public async Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
    {
        var invocation = attempt.Call.Invocation;
        if (_runs.Running(invocation)) return;
        if (program is null) throw new DriverException("the desktop's program that serves a step's tools is not found");
        var ask = await askOf(unit, attempt, node, ct).ConfigureAwait(false) ?? throw new DriverException($"{unit} names no work for `{node.Id}` to be performed on");
        var place = new PluginPlace(ask.Repository, ask.Workspace);
        ServerEntry Tools(string session) => PluginToolsHand.StepEntry(home, new PluginToolsAt(session, place, "en"), program, ask.Plugin, invocation);
        Directory.CreateDirectory(StepAnswer.Folder(home));
        AtomicFile.WriteText(StepAnswer.AskPath(home, invocation), ask.Write());
        _runs.Start(new StepAgentStart(invocation) { Authority = StepAuthorities.Of(unit, attempt) }, async _ =>
        {
            List<StepSessionEnd> ends = [];
            try
            {
                ends.Add(await sessions.RunAsync(ask, Tools, null, CancellationToken.None).ConfigureAwait(false));
                if (ends[0].RefusedOn is { } refused && !Answered(invocation))
                {
                    ends.Add(await sessions.RunAsync(ask, Tools, refused, CancellationToken.None).ConfigureAwait(false));
                }
            }
            finally
            {
                StepAnswer.Unanswered(home, invocation, SignInWords(ends));
                if (!StepBusFacts.Teed(home, () => StepBusFacts.StepTurnEnded(unit, node.Id, invocation))) wake?.Invoke();
            }
        }, _ => true);
    }

    private string? SignInWords(IReadOnlyList<StepSessionEnd> ends) =>
        ends.Select(end => end.Agent).OfType<string>().FirstOrDefault() is { } agent ? StepSignIns.Words(ends, AccountNames.Of(home, agent)) : null;

    public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct)
    {
        var invocation = attempt.Call.Invocation;
        if (StepAnswer.Read(home, invocation) is { } answer) return Task.FromResult(answer);
        if (!Holds(invocation)) return Task.FromResult(new WorkReconcileAnswer(ReconcileStates.NotStarted));
        if (_runs.Running(invocation)) return Task.FromResult(new WorkReconcileAnswer(ReconcileStates.Running));
        // An ask whose session no longer runs here (the loop that started it ended) has ended unanswered.
        StepAnswer.Unanswered(home, invocation);
        return Task.FromResult(StepAnswer.Read(home, invocation) ?? new WorkReconcileAnswer(ReconcileStates.Unknown));
    }

    public bool Answered(string invocation) => File.Exists(StepAnswer.AnswerPath(home, invocation));

    public bool Holds(string invocation) => File.Exists(StepAnswer.AskPath(home, invocation));

    public void Stop(string invocation) => sessions.Stop(invocation);

    public void Supersede(string invocation, string why) => sessions.Stop(invocation);

    /// <summary>Completes once every session started here has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => _runs.IdleAsync();
}
