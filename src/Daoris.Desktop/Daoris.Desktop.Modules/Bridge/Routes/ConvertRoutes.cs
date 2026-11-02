using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// The conversion of the old records (WORKFLOWCLEAN1 1e; D227 point 3, the cleanup design §4): <c>CONVERT_PLAN</c> reads, <c>CONVERT_APPLY</c> and
/// <c>CONVERT_UNDO</c> are the person's presses. One plan: every steps, <c>handed</c> and <c>land</c>+<c>accept</c> newest saved as the next version
/// of its id, each place that follows Current given a saved workflow, the rules removed and the line policies moved, in one change.
/// </summary>
public sealed class ConvertRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    // The plan: `{composed?, landAutomatic?}`, the row's two choices, or `{undo: true}` for the undo's plan. It reads the driver's file, the registry,
    // the quests and the saved workflows, and writes and asks nothing. Answers the census, each operation and the digest the press names.
    [DriverRoute("CONVERT_PLAN")]
    [ByEvent("CONVERT_PLAN", Untold = "driver.json, the saved workflows, the bound runs and the service's registry and quests")]
    private async Task<object?> PlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        if (Flag(request, "undo"))
        {
            var undo = ConversionApplies.UndoPlan(_loop.Home, config);
            if (Flag(request, "said")) return Said(ConvertCommand.UndoLines(undo), undo.Refusal is null ? 0 : 1);
            return new { Undo = true, Digest = undo.Digest, Refusal = undo.Refusal, Lines = undo.Lines };
        }

        var choices = ChoicesOf(request);
        if (choices.Problem is { } problem) throw new DriverException(problem);
        var world = await ConvertWorlds.ReadAsync(new PlaceWorld(_loop.Service ?? throw NotReady()), config, _loop.Home, cancellationToken).ConfigureAwait(false);
        var plan = ConversionPlans.Plan(world, choices, new ConversionReceipts(_loop.Home).HeldPlaces());
        return Flag(request, "said") ? Said(ConvertCommand.Lines(plan), 0) : Answer(plan);
    }

    // The person's press of a plan: `{digest, composed?, landAutomatic?}`. It plans again and refuses `moved` where the digest differs, writes a
    // receipt before the first effect, saves the workflows, then makes the one change of driver.json, judging again inside it. A digest that
    // holds a receipt goes on from it. Answers `{ done, refused, message, digest, operations, plan }`.
    [DriverRoute("CONVERT_APPLY")]
    [Publishes("CONVERT_APPLY", StateScopes.Driver | StateScopes.Rules | StateScopes.Lines)]
    private async Task<object?> ApplyAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var place = new PlaceWorld(service);
        var quests = await place.QuestsAsync(cancellationToken).ConfigureAwait(false);
        var read = DateTimeOffset.UtcNow;
        var doors = new ConvertDoors(
            _loop.Home, token => ConvertWorlds.ReadAsync(place, DriverConfig.Load(_loop.ConfigPath), _loop.Home, token), _context.Change,
            () => DriverConfig.Load(_loop.ConfigPath), () => ConvertWorlds.OpenCurrentNow(_loop.Home, quests, read));
        var result = await ConversionApplies.ApplyAsync(doors, ChoicesOf(request), Optional(request, "digest") ?? "", cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return Flag(request, "said") ? Said(ConvertCommand.Lines(result), result.Done ? 0 : 1) : Answer(result);
    }

    // The person's press of the undo: `{digest}`, the undo plan's. It puts the removed rules, access entries and old choices back in one change,
    // takes away the default workflow and line policies the conversion wrote, and refuses `moved` where any of them changed since.
    [DriverRoute("CONVERT_UNDO")]
    [Publishes("CONVERT_UNDO", StateScopes.Driver | StateScopes.Rules | StateScopes.Lines)]
    private object? Undo(IpcRequest request)
    {
        var doors = new ConvertDoors(
            _loop.Home, _ => throw new InvalidOperationException("an undo reads no world"), _context.Change, () => DriverConfig.Load(_loop.ConfigPath), () => new HashSet<string>());
        var result = ConversionApplies.Undo(doors, Optional(request, "digest") ?? "");
        _loop.Nudge();
        return Flag(request, "said") ? Said(ConvertCommand.Lines(result), result.Done ? 0 : 1) : Answer(result);
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
        Operations = result.Operations.Select(each => new { each.Id, each.Kind, each.Said, each.Outcome, each.Note }).ToArray(),
        Plan = result.Plan is { } plan ? Answer(plan) : null,
    };

    private static object Answer(ConversionPlan plan) => new
    {
        plan.Digest,
        plan.Empty,
        Choices = new { plan.Choices.Composed, plan.Choices.LandAutomatic },
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
