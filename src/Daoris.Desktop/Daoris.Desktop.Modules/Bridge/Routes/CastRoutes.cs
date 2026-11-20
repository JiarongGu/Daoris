using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A place's cast and a step's holder: the place's Settings read each role's holder now and why (<c>PLACE_CAST</c>), the owner casts a role
/// there (<c>SET_PLACE_CAST</c>, `daoris settings cast` its twin), and a step is taken, handed to another or given back (<c>WORKFLOW_HOLD</c>,
/// `daoris workflow take|hand|give-back` its twin), each journaled with its reason (<see cref="PlaceCastFiles"/>).
/// </summary>
public sealed class CastRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// A place's cast as its Settings draw it (<see cref="PlaceCastView"/>): <c>place</c>, <c>risk</c>, <c>riskSource</c>, <c>marks</c>, <c>roles</c>
    /// (each <c>{role, holder, why, source, words}</c>), the <c>roster</c> a cast may name and the reporting <c>line</c>. Read-only.
    /// </summary>
    [DriverRoute("PLACE_CAST", PipeClass.Open)]
    [ByEvent("PLACE_CAST", Untold = "the role store's casts, driver.json's risk and marks and the roster, which no scope or slice tells")]
    private object? PlaceCast(PlaceCastRead press) => View(press.Workspace, press.Repository);

    /// <summary>
    /// The owner's cast of <c>role</c> at the place to <c>holder</c> (<c>person</c>, <c>agent</c>, <c>agent:&lt;name&gt;</c>), or back to its default
    /// where none is named, journaled with <c>why</c>; answers the place's view, or throws the refusal's code with nothing written.
    /// </summary>
    [DriverRoute("SET_PLACE_CAST", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_PLACE_CAST", Repository = "repository", Workspace = "workspace")]
    [GroundRead("SET_PLACE_CAST", nameof(PipeReads.PlaceCast))]
    [NotAlready("SET_PLACE_CAST", "each cast is journaled with its reason, so the same holder cast again keeps the new reason")]
    [Publishes("SET_PLACE_CAST", StateScopes.Driver)]
    private object? SetPlaceCast(SetPlaceCast press)
    {
        var files = new PlaceCastFiles(_loop.Home);
        if (AgentSettles.CastAt(files, PlaceRiskBook.KeyOf(press.Workspace, press.Repository), press.Role.Trim(), press.Holder?.Trim(), press.Why, DateTimeOffset.UtcNow) is { } refused)
        {
            throw new DriverException($"the cast of `{press.Role}` was refused: {refused}{(refused == PlaceCastFiles.Unreadable ? $" ({files.Problem()})" : "")}.");
        }

        return View(press.Workspace, press.Repository);
    }

    /// <summary>
    /// <i>Take</i>, <i>Hand to…</i> or <i>Give back</i> on <c>node</c> of <c>run</c> (<see cref="StepHolding"/>): answers
    /// <c>{run, node, act, done, refused, holder, epoch, message}</c>, a refusal as an answer with its code. Asked for its words (<c>said</c>) by the
    /// terminal's verb, it runs that verb's own path.
    /// </summary>
    [DriverRoute("WORKFLOW_HOLD", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("WORKFLOW_HOLD", Quest = "run")]
    [GroundWait("WORKFLOW_HOLD", "workflow-node", "run", "node")]
    [Says("WORKFLOW_HOLD", "run:quest", "act:form", "why:words")]
    [Publishes("WORKFLOW_HOLD", StateScopes.Driver)]
    private async Task<object?> WorkflowHoldAsync(WorkflowHoldPress press, CancellationToken cancellationToken)
    {
        var records = new NodeRecordFiles(_loop.Home);
        PlaceRiskBook Risk() => DriverConfig.Load(_loop.ConfigPath).PlaceRisk;
        if (press.Said)
        {
            var ask = new WorkflowHoldAsk(press.Act, press.Run, press.Node, press.Holder, press.Why) { Epoch = EpochOf(press.Epoch), By = SpeakerOf(press.Epoch) };
            return await TerminalSaid.RunAsync(output =>
                WorkflowHoldCommand.RunAsync(ask, records, Risk, () => DateTimeOffset.UtcNow, output, instance: null, _ => null, ct: cancellationToken))
                .ConfigureAwait(false);
        }

        var held = AgentSettles.Hold(
            records, new PlaceCastFiles(_loop.Home), Risk(), press.Run, press.Node, press.Act, press.Holder, press.Why, DateTimeOffset.UtcNow, EpochOf(press.Epoch), SpeakerOf(press.Epoch));
        if (held.Done) _loop.Nudge();
        return new
        {
            held.Run, held.Node, held.Act, held.Done, held.Refused, held.Tenure?.Holder, held.Tenure?.Epoch, Message = AgentSettles.Said(held),
        };
    }

    /// <summary>The holder's epoch a settling press names (<c>epoch</c>, a whole number), as its door takes it; null where it names none.</summary>
    public static int? EpochOf(long? epoch) => epoch is { } given ? (int)Math.Clamp(given, 0, int.MaxValue) : null;

    /// <summary>
    /// Who the press this call runs for speaks as, by the channel it came by (<see cref="PipeDoor.By"/>, <see cref="SettleBy.OfChannel"/>), in
    /// the epoch it names: null for the person's own (the window, a card they confirmed, their terminal), an agent's for a working session's own
    /// press (its start's, <see cref="PipeDoor.WorkerAgent"/>), Ask Daoris's and an appointed role's. Never read from the payload.
    /// </summary>
    public static SettleBy? SpeakerOf(long? epoch) =>
        SettleBy.OfChannel(PipeDoor.By.By, PipeDoor.Role is not null, EpochOf(epoch), PipeDoor.WorkerAgent, PipeDoor.Worker);

    private object View(string? workspace, string? repository)
    {
        var read = new PlaceCastFiles(_loop.Home).Read();
        var view = PlaceCastView.Of(read.Book, DriverConfig.Load(_loop.ConfigPath).PlaceRisk, workspace, repository,
            ProviderRegistry.Built().Agents, RoleCatalog.ReadHome(_loop.Home).Slots, read.Problem);
        return new
        {
            view.Place, view.Risk, view.RiskSource, view.Marks, view.Roster, view.Unreadable,
            Roles = view.Roles.Select(role => new { role.Role, role.Holder, role.Why, role.Source, role.Words }).ToArray(),
            Line = view.Line.Select(level => new { level.Level, level.Role, level.Holder, level.Why }).ToArray(),
        };
    }
}

/// <summary>The place a cast is read for: neither named is the machine's.</summary>
public sealed record PlaceCastRead(string? Workspace, string? Repository);

/// <summary>The owner's cast of a role at a place: no holder is its default.</summary>
public sealed record SetPlaceCast(string? Workspace, string? Repository, string Role, string? Holder, string? Why);

/// <summary>A step's holder changed: <c>take</c>, <c>hand</c> (naming the holder) or <c>give-back</c>, with the reason and the epoch its row read.</summary>
public sealed record WorkflowHoldPress(string Run, string Node, string Act, string? Holder = null, string? Why = null, bool Said = false, long? Epoch = null);
