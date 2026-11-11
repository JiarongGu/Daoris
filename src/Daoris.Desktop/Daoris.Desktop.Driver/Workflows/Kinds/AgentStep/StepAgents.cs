using System.Collections.Concurrent;

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
    string home, IStepSessions sessions, Func<string, NodeAttempt, NodeDeclaration, StepAgentAsk?> askOf, string? program, Action? wake = null) : IStepAgents
{
    /// <summary>The key the door is kept under among the doors, beside the kinds' own.</summary>
    public const string Door = "plugin.step";

    private readonly ConcurrentDictionary<string, Task> _running = new(StringComparer.Ordinal);

    public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
    {
        var invocation = attempt.Call.Invocation;
        if (_running.ContainsKey(invocation)) return Task.CompletedTask;
        var ask = askOf(unit, attempt, node) ?? throw new DriverException($"{unit} names no work for `{node.Id}` to be performed on");
        var at = new PluginToolsAt(invocation, new PluginPlace(ask.Repository, ask.Workspace), "en");
        var tools = PluginToolsHand.StepEntry(home, at, program, ask.Plugin) ?? throw new DriverException("the desktop's program that serves a step's tools is not found");
        Directory.CreateDirectory(StepAnswer.Folder(home));
        AtomicFile.WriteText(StepAnswer.AskPath(home, invocation), ask.Write());
        using (ExecutionContext.SuppressFlow())
        {
            _running[invocation] = Task.Run(async () =>
            {
                try
                {
                    await sessions.RunAsync(ask, tools, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    StepAnswer.Unanswered(home, invocation);
                    _running.TryRemove(invocation, out _);
                    wake?.Invoke();
                }
            });
        }

        return Task.CompletedTask;
    }

    public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct)
    {
        var invocation = attempt.Call.Invocation;
        if (StepAnswer.Read(home, invocation) is { } answer) return Task.FromResult(answer);
        if (!Holds(invocation)) return Task.FromResult(new WorkReconcileAnswer(ReconcileStates.NotStarted));
        if (_running.ContainsKey(invocation)) return Task.FromResult(new WorkReconcileAnswer(ReconcileStates.Running));
        // An ask whose session no longer runs here (the loop that started it ended) has ended unanswered.
        StepAnswer.Unanswered(home, invocation);
        return Task.FromResult(StepAnswer.Read(home, invocation) ?? new WorkReconcileAnswer(ReconcileStates.Unknown));
    }

    public bool Answered(string invocation) => File.Exists(StepAnswer.AnswerPath(home, invocation));

    public bool Holds(string invocation) => File.Exists(StepAnswer.AskPath(home, invocation));

    public void Stop(string invocation) => sessions.Stop(invocation);

    /// <summary>Completes once every session started here has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => Task.WhenAll(_running.Values);
}
