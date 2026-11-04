using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The workflow domain's answers as the page draws them: a drawn workflow, a route's effective graph and one run.</summary>
internal static class WorkflowAnswers
{
    /// <summary>
    /// The answer: each step as the shared table's cell, its settings in the order the version reads them, and each limit's
    /// sentence by its code, for a page with no words of its own for one; and the start node.
    /// </summary>
    public static object Answer(
        CurrentWorkflow drawn, string? repository, string workspace, bool? registered, IReadOnlyList<object>? repositories, object start,
        WorkflowEffective? effective)
    {
        var limits = new JsonObject();
        foreach (var code in drawn.Steps.Select(step => step.Limit).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            limits[code] = WorkflowLimits.Says(code);
        }

        // only a caller that asked for a route is answered `effective`; an older one is answered as it was.
        if (effective is not null)
        {
            return new
            {
                Repository = repository,
                Workspace = workspace,
                Registered = registered,
                drawn.StartHolds,
                Steps = drawn.Steps.Select(step => JsonDocument.Parse(WorkflowSteps.ToJson(step)).RootElement.Clone()).ToArray(),
                drawn.Version,
                Limits = limits,
                Repositories = repositories,
                Start = start,
                Effective = EffectiveAnswer(effective),
            };
        }

        return new
        {
            Repository = repository,
            Workspace = workspace,
            Registered = registered,
            drawn.StartHolds,
            Steps = drawn.Steps.Select(step => JsonDocument.Parse(WorkflowSteps.ToJson(step)).RootElement.Clone()).ToArray(),
            drawn.Version,
            Limits = limits,
            Repositories = repositories,
            Start = start,
        };
    }

    /// <summary>
    /// A route's effective graph: the workflow it follows and how it was chosen, its version or Current's digest, the graph to
    /// draw, and each node's cell as <c>WORKFLOW_RUN</c> answers a step, with each limit a cell carries in the driver's words.
    /// </summary>
    private static object EffectiveAnswer(WorkflowEffective effective)
    {
        var limits = new JsonObject();
        foreach (var code in effective.Limits) limits[code] = WorkflowLimits.Says(code);
        return new
        {
            effective.Route,
            effective.Workflow,
            effective.Name,
            effective.Version,
            effective.Digest,
            effective.Schema,
            Source = new { effective.Source.Level, effective.Source.Kind, effective.Source.Label, effective.Source.Paths },
            effective.Verifies,
            effective.Graph,
            Cells = effective.Cells.Select(cell => new { cell.Node, Step = JsonDocument.Parse(WorkflowSteps.ToJson(cell.Step)).RootElement.Clone() }).ToArray(),
            Limits = limits,
            effective.Problem,
            // A copy of a shipped default this build ships changed since: left out, as null, where it is up to date.
            Newer = effective.Newer is null ? null : new { effective.Newer.Default, effective.Newer.Name, effective.Newer.Graph },
        };
    }

    /// <summary>
    /// One run as the page draws it: its chosen graph's digest and bound version identity, the plugins that may hold a start,
    /// each limit's sentence, and each step beside where it stands and its facts. A problem has no replacement graph.
    /// </summary>
    public static object RunAnswer(WorkflowRun run)
    {
        var limits = new JsonObject();
        foreach (var code in run.Steps.Select(step => step.Step.Limit).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            limits[code] = WorkflowLimits.Says(code);
        }

        return new
        {
            run.Repository,
            run.Workspace,
            run.Ask,
            run.Quests,
            run.Session,
            run.At,
            run.Problem,
            // a chat run's key and its checks, which a conversation's execution is held to;
            // null for a quest's run, and from a driver before it.
            Chat = run.ChatRun is { } chat ? new { Run = chat, Checks = run.ChatChecks, Enforced = true } : null,
            // The workflow's wait, in the shape the Trees domain's landing gate answers it.
            WorkflowGate = TreesRoutes.WorkflowWaits(run.WorkflowGate),
            Workflow = new { run.Current.Version, run.Current.StartHolds, Limits = limits,
                Id = run.Process?.Named == true ? run.Process.Binding!.Workflow : null,
                BoundVersion = run.Process?.Named == true ? run.Process.Binding!.Version : null,
                // the route that chose its pipeline, as its binding keeps it; the page says it in its own words.
                Route = run.Process?.Binding is { } bound ? new { bound.Level, bound.Kind, bound.Label } : null },
            Steps = run.Steps.Select(step => new
            {
                Step = JsonDocument.Parse(WorkflowSteps.ToJson(step.Step)).RootElement.Clone(),
                step.State,
                step.Detail,
                step.Added,
                step.Session,
                step.Quest,
                step.Agent,
                step.At,
                step.Environment,
                step.Commit,
                step.Branch,
                step.PullRequest,
                step.Plugin,
                step.Count,
                step.Of,
                step.GoAhead,
                step.Words,
                step.Code,
                // a graph run's node's own kind and the subject it observes; null on a steps run's step.
                step.Node,
                step.On,
                // what the person may say of a graph run's node now (`skip`, `stop`), and their word kept on it and
                // not applied yet; null on a steps run's step and where none.
                step.Acts,
                // a failed check's gate, and its attempt held unknown for the person or settled by their stop.
                step.Gate,
                Held = step.Held is { } held ? new { held.Unit, held.Attempt, held.Words, held.Resolution } : null,
                Asked =step.Asked is { } asked ? new { asked.Node, asked.Act, asked.Words, asked.At } : null,
                // the quests the work's sessions published and the helpers they asked, on the work step; null where none.
                Requests = step.Requests?.Select(request => new
                {
                    request.Kind, request.Id, request.To, request.State, request.Session, request.Awaits, request.Words, request.At,
                }).ToArray(),
            }).ToArray(),
            // a run bound to a graph version, drawn from its record: its graph's wires and what its record says of it.
            // a `when` wire's outcome on it alone, the `back` wires apart, the nodes no route takes and each decision's outcome.
            Graph = run.Graph is { } graph
                ? new
                {
                    graph.Run, graph.Entries, graph.End,
                    Wires = graph.Wires.Select(wire => wire.Outcome is { } outcome
                        ? (object)new { wire.From, wire.To, wire.Kind, Outcome = outcome }
                        : new { wire.From, wire.To, wire.Kind }).ToArray(),
                    graph.Quest, graph.Branch, graph.Judged, graph.Unread, graph.Ended,
                    Backs = graph.Backs.Select(wire => new { wire.From, wire.To, wire.On }).ToArray(),
                    graph.NotTaken,
                    graph.Outcomes,
                }
                : null,
        };
    }
}
