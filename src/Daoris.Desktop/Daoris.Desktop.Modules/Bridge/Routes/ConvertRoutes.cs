using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// The home's one conversion door (ONEWAY1ae; D228, the oneway design §1.1, the cleanup design §4): <c>CONVERT_PLAN</c> reads every part's plan,
/// <c>CONVERT_APPLY</c> and <c>CONVERT_UNDO</c> are the person's presses. Today the workflow part: every steps, <c>handed</c> and
/// <c>land</c>+<c>accept</c> newest saved as the next version of its id, each place that follows Current given a saved workflow, the rules removed
/// and the line policies moved, in one change.
/// </summary>
public sealed class ConvertRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    // The plan: `{composed?, landAutomatic?}`, the row's two choices, or `{undo: true}` for the undo's plan. It reads every part's sources (today
    // driver.json, the saved workflows, the bound runs and the service's registry and quests) and writes and asks nothing; a part whose source does
    // not answer is left out and said. Answers each part's plan, the digest the press names and the parts not read.
    [DriverRoute("CONVERT_PLAN")]
    [ByEvent("CONVERT_PLAN", Untold = "driver.json, the saved workflows, the bound runs and the service's registry and quests")]
    private async Task<object?> PlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var doors = ConversionDoors.Of(_loop);
        if (Flag(request, "undo"))
        {
            var undo = ConversionApplies.UndoPlan(doors);
            if (Flag(request, "said")) return Said(ConvertCommand.UndoLines(undo), undo.Refusal is null ? 0 : 1);
            return new { Undo = true, Digest = undo.Digest, Refusal = undo.Refusal, Lines = undo.Lines };
        }

        var choices = ChoicesOf(request);
        if (choices.Problem is { } problem) throw new DriverException(problem);
        var plan = await ConversionApplies.PlanAsync(doors, choices, cancellationToken).ConfigureAwait(false);
        return Flag(request, "said") ? Said(ConvertCommand.Lines(plan), 0) : Answer(plan);
    }

    // The person's press of a plan: `{digest, composed?, landAutomatic?}`, or `{digest, leave}` to close a failed part of its receipt. It plans
    // every part again and refuses `moved` where one that reads differs, writes a part that does not answer `failed`, writes a receipt before the
    // first effect, then each part's effects in apply order. A digest that holds a receipt goes on from it, a failed part retried. Answers
    // `{ done, refused, message, digest, operations, parts, plan }`.
    [DriverRoute("CONVERT_APPLY")]
    [Publishes("CONVERT_APPLY", StateScopes.Driver | StateScopes.Rules | StateScopes.Lines)]
    private async Task<object?> ApplyAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var read = DateTimeOffset.UtcNow;
        var doors = ConversionDoors.Of(_loop, _context.Change, await QuestsAsync(cancellationToken).ConfigureAwait(false), read);
        var digest = Optional(request, "digest") ?? "";
        var result = Optional(request, "leave") is { } leave
            ? ConversionApplies.Leave(doors, digest, leave)
            : await ConversionApplies.ApplyAsync(doors, ChoicesOf(request), digest, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return Flag(request, "said") ? Said(ConvertCommand.Lines(result), result.Done ? 0 : 1) : Answer(result);
    }

    // The person's press of the undo: `{digest}`, the undo plan's. It puts each done part of the latest receipt back, last applied first, and
    // refuses `moved` where a write it made changed since.
    [DriverRoute("CONVERT_UNDO")]
    [Publishes("CONVERT_UNDO", StateScopes.Driver | StateScopes.Rules | StateScopes.Lines)]
    private object? Undo(IpcRequest request)
    {
        var result = ConversionApplies.Undo(ConversionDoors.Of(_loop, _context.Change), Optional(request, "digest") ?? "");
        _loop.Nudge();
        return Flag(request, "said") ? Said(ConvertCommand.Lines(result), result.Done ? 0 : 1) : Answer(result);
    }

    /// <summary>The quests now, for the Current units open at the press; null where the service does not answer, so every Current run counts open.</summary>
    private async Task<IReadOnlyList<QuestView>?> QuestsAsync(CancellationToken ct)
    {
        if (_loop.Service is not { } service) return null;
        try
        {
            return await service.EveryQuestAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    private static ConversionChoices ChoicesOf(IpcRequest request) =>
        new(Optional(request, "composed") ?? ConversionChoices.AsLanded, Optional(request, "landAutomatic") ?? ConversionChoices.WithReview);

    private static object Said(IReadOnlyList<string> lines, int code) => new { Said = new TreesSaid(lines, [], code).ToWire() };

    private static object Answer(ConvertResult result) => new
    {
        result.Done,
        result.Refused,
        result.Message,
        result.Digest,
        Operations = result.Operations.Select(Answer).ToArray(),
        Parts = result.Parts.Select(each => new { each.Part, each.Digest, each.Outcome, each.Said, Operations = each.Operations.Select(Answer).ToArray() }).ToArray(),
        Plan = result.Plan is { } plan ? Answer(plan) : null,
    };

    private static object Answer(ConversionOp each) => new { each.Id, each.Kind, each.Said, each.Outcome, each.Note, each.Clear };

    private static object Answer(HomePlan plan) => new
    {
        plan.Digest,
        plan.Empty,
        plan.Ended,
        plan.Count,
        Choices = new { plan.Choices.Composed, plan.Choices.LandAutomatic },
        Parts = plan.Parts.Select(each => new
        {
            each.Part,
            each.Digest,
            each.Empty,
            each.Census.Count,
            each.Census.Ended,
            each.Lines,
            Clears = each.Clears.Select(clear => new { clear.Said, clear.Count, clear.From, clear.To, clear.Bytes }).ToArray(),
            Workflows = each.Detail is WorkflowPartPlan workflows ? Answer(workflows.Plan) : null,
        }).ToArray(),
        Unread = plan.Unread.Select(each => new { each.Part, each.Said }).ToArray(),
    };

    /// <summary>The workflow part's own plan, as its review draws it.</summary>
    private static object Answer(ConversionPlan plan) => new
    {
        Saves = plan.Saves.Select(each => new { each.Id, each.Name, each.Mode, each.Shape, each.Said, each.Base, each.Places }).ToArray(),
        Uses = plan.Uses.Select(each => new { each.Place, each.IsWorkspace, each.Workflow, each.Kind, each.Said }).ToArray(),
        plan.DefaultWorkflow,
        Policies = plan.Policies.Select(each => new { each.Place, each.IsWorkspace }).ToArray(),
        Removals = plan.Removals.Select(each => new { each.Place, each.IsWorkspace, each.Keys }).ToArray(),
        Kept = plan.Kept.Select(each => new { each.Code, each.Subject, each.Said }).ToArray(),
        plan.Lowers,
        Census = new
        {
            Ended = plan.Census.Ended,
            Versions = plan.Census.Versions.Select(each => new { each.Workflow, each.Version, each.Shape }).ToArray(),
            Places = plan.Census.Places.Select(each => new { each.Place, each.IsWorkspace, each.Workspace, each.Shapes, each.WorkCycle, each.OpenUnits }).ToArray(),
            Runs = plan.Census.Runs.Select(each => new { each.Run, each.Repository, each.Workflow, each.Version, each.Engine, each.Door }).ToArray(),
            Leftovers = plan.Census.Leftovers.Select(each => new { each.Place, each.IsWorkspace, each.Keys, each.Why }).ToArray(),
        },
    };
}
