using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A place's quick set-up (AGENTSPLACE1a2, AGENTSPLACE1b2), the one door that adopts a repository (WORKFLOWCLEAN1 1f): <c>PLACE_PLAN</c> reads,
/// <c>PLACE_APPLY</c> writes, and its adoption part is <see cref="SetupPress"/>'s one publisher.
/// </summary>
/// <remarks>
/// <para><b>Never Daoris writing into the repository</b> (D32): the press asks its own session, and writes a rule in Daoris's
/// own file.</para>
/// </remarks>
public sealed class SetupRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    // A place's quick set-up (AGENTSPLACE1a2, the Agents-place design §4), `daoris-driver place … --plan`'s twin: {repositories, preset,
    // opinion, look, adoption, builds, drive}. The parts are applied to a copy of driver.json through the doors' own edits and read before and
    // after through the start's resolvers; every refusal at once, what lowers the person's part, the reach, the work driving releases and each
    // declaration with its acceptances. It reads the registry, the quests and each line's tip as git objects, and writes and probes nothing.
    [DriverRoute("PLACE_PLAN")]
    [ByEvent("PLACE_PLAN", Untold = "the service's registry and quests, driver.json, the saved workflows and each line's tip in its checkout")]
    private async Task<object?> PlacePlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var ask = PlaceAskOf(request);
        var config = DriverConfig.Load(_loop.ConfigPath);
        var plugins = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names);
        var world = new PlaceWorld(service);
        if (Flag(request, "said"))
        {
            return await TerminalSaid.RunAsync(output => PlaceCommand.RunAsync(
                PlaceCommand.ArgsOf(ask), output, world, config, _loop.Home, plugins, _context.Door(config), instance: null, ct: cancellationToken)).ConfigureAwait(false);
        }

        return await PlacePlans.PlanAsync(world, config, _loop.Home, plugins, _context.Door(config), ask, cancellationToken).ConfigureAwait(false);
    }

    // A place's apply (AGENTSPLACE1b2, the Agents-place design §4), the plan's press: the plan's payload and its `digest`, and `accept`, the
    // acceptances chosen, each `{repository, purpose}`. It plans again and refuses `moved` where the digest differs, reads the tips against the
    // chosen blobs, writes every change and acceptance in one edit of driver.json, and asks each adoption quest last, keyed by the digest, its
    // receipt written before the first effect (`PlaceApplies`). A press of a digest that holds a receipt goes on from it. Answers `{ done, refused,
    // message, digest, operations }`, each operation `{ id, kind, repository, said, outcome, note, quest }`; a refusal is an answer, nothing written.
    [DriverRoute("PLACE_APPLY")]
    [Publishes("PLACE_APPLY", StateScopes.Driver | StateScopes.Quests | StateScopes.Asks | StateScopes.Rules)]
    private async Task<object?> PlaceApplyAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        // The owner row and the grounds read `repositories` alone: a `repository` would name a place they never judged.
        if (request.Payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("repository", out _))
        {
            throw new DriverException("a place's apply names its repositories as `repositories`, never a single `repository`.");
        }

        var service = _loop.Service ?? throw NotReady();
        var digest = Optional(request, "digest") ?? "";
        var config = DriverConfig.Load(_loop.ConfigPath);
        var doors = new PlaceApplyDoors(
            new PlaceWorld(service), _context.SetupWorldOf(service), _loop.Home, PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names), _context.Door(config),
            () => DriverConfig.Load(_loop.ConfigPath), _context.Change, DateOnly.FromDateTime(DateTime.Now));
        // AGENTSPLACE1b3: `daoris-driver place … --apply` in the verb's own words, which plans now and applies that plan; the person's Confirm
        // on the window's card has already pinned the plan between the terminal's ask and here (`PipeGrounds`).
        if (Flag(request, "said"))
        {
            var said = await TerminalSaid.RunAsync(output => PlaceCommand.RunAsync(
                PlaceCommand.ApplyArgsOf(PlaceAskOf(request), AcceptedOf(request)), output, doors.Place, config, _loop.Home, doors.Plugins, doors.Door,
                instance: null, apply: doors, ct: cancellationToken)).ConfigureAwait(false);
            _loop.Nudge();
            return said;
        }

        var result = await PlaceApplies.ApplyAsync(doors, PlaceAskOf(request), digest, AcceptedOf(request), cancellationToken).ConfigureAwait(false);
        // The look starts what the configuration now allows, and the quest the press asked.
        _loop.Nudge();
        return new
        {
            result.Done,
            result.Refused,
            result.Message,
            result.Digest,
            Operations = result.Operations.Select(each => new { each.Id, each.Kind, each.Repository, each.Said, each.Outcome, each.Note, each.Quest }).ToArray(),
        };
    }

    // A repository's reconcile with the shared doctrine (the context-one design §3 B, D230 point 2), the set-up press's own case, planned:
    // {repository} answers {repository, offered, status, refusals, pressable, title}. `status` is what the doctrine tool's `status --json` said of
    // its checkout (`synced`, `update` `behind` or `differs`, `carried` and `locked`, the files a sync would `changed`, `added` and `retired`, the
    // keeps `reopened`, a `newerLock`), absent where it was not asked or printed none; `offered` is where it says the canon moved or a keep
    // reopened. It reads the registry, the quests, the line and the tool, and writes nothing.
    [DriverRoute("RECONCILE_PLAN")]
    [ByEvent("RECONCILE_PLAN", Untold = "the service's registry and quests, driver.json, the repository's line and the doctrine tool's status for its checkout")]
    private async Task<object?> ReconcilePlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (plan, _) = await ReconcileOfAsync(request, cancellationToken).ConfigureAwait(false);
        return new
        {
            plan.Repository,
            Offered = plan.Doctrine?.Offers == true,
            Status = plan.Doctrine is { } status
                ? new { status.Synced, status.Update, status.Carried, status.Locked, status.Changed, status.Added, status.Retired, status.Reopened, status.NewerLock }
                : null,
            Refusals = plan.Refusals.Select(each => new { each.Code, each.Sentence }).ToArray(),
            plan.Pressable,
            plan.Title,
        };
    }

    // The reconcile's press (D230 point 2: the person's own), {repository}: it plans again and presses that plan as the set-up press presses one, the
    // rule into the repository's scope, then one ask to it as the person's, its quest admitted alone where the repository is not driven here. It
    // answers {done, refused, message, quest, ask}; a plan that is refused is an answer by its first refusal's code, nothing published or allowed.
    [DriverRoute("RECONCILE_APPLY")]
    [Publishes("RECONCILE_APPLY", StateScopes.Driver | StateScopes.Quests | StateScopes.Asks | StateScopes.Rules)]
    private async Task<object?> ReconcileApplyAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (plan, world) = await ReconcileOfAsync(request, cancellationToken).ConfigureAwait(false);
        if (!plan.Pressable)
        {
            return new { Done = false, Refused = plan.Refusals.FirstOrDefault()?.Code ?? SetupRefusals.Open, Message = plan.Refusals.FirstOrDefault()?.Sentence ?? "nothing to ask." };
        }

        var outcome = await SetupPress.ApplyAsync(plan, world, _loop.Home, cancellationToken).ConfigureAwait(false);
        // The look starts the quest the press asked.
        _loop.Nudge();
        return new { Done = outcome.Published, Refused = outcome.Published ? null : "refused", outcome.Message, Quest = outcome.QuestId, Ask = outcome.AskId };
    }

    /// <summary>The reconcile's plan for the payload's <c>repository</c>, and the world it was read from, which its press asks through.</summary>
    private async Task<(SetupPlan Plan, ISetupWorld World)> ReconcileOfAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository") ?? throw new DriverException("a reconcile names its repository as `repository`.");
        var world = _context.SetupWorldOf(_loop.Service ?? throw NotReady());
        var config = DriverConfig.Load(_loop.ConfigPath);
        var plan = await SetupPress.PlanReconcileAsync(
            world, config, _context.Door(config), repository, DateOnly.FromDateTime(DateTime.Now), cancellationToken).ConfigureAwait(false);
        return (plan, world);
    }

    private static PlaceAsk PlaceAskOf(IpcRequest request) =>
        new(Names(request), Optional(request, "preset") ?? "")
        {
            Opinion = Flag(request, "opinion"),
            Look = Flag(request, "look"),
            Adoption = Flag(request, "adoption"),
            Builds = Optional(request, "builds"),
            Drive = Flag(request, "drive"),
            AtOnce = Count(request, "atOnce"),
            Pilot = Count(request, "pilot"),
        };

    /// <summary>A whole number the payload may carry, or null where it is absent or not one.</summary>
    private static int? Count(IpcRequest request, string name) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count)
            ? count
            : null;

    /// <summary>The acceptances a place's apply is asked to make: <c>accept</c>, a list of <c>{repository, purpose}</c>.</summary>
    private static IReadOnlyList<PlaceAccept> AcceptedOf(IpcRequest request) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty("accept", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? [.. listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.Object)
                .Select(each => new PlaceAccept(
                    each.TryGetProperty("repository", out var repository) && repository.ValueKind == JsonValueKind.String ? repository.GetString()! : "",
                    each.TryGetProperty("purpose", out var purpose) && purpose.ValueKind == JsonValueKind.String ? purpose.GetString()! : ""))]
            : [];

    /// <summary>The repositories a plan is asked for: <c>repositories</c>, a list of names, or one <c>repository</c>.</summary>
    private static IReadOnlyList<string> Names(IpcRequest request) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty("repositories", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? [.. listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
            : Optional(request, "repository") is { } one ? [one] : [];
}
