using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A repository's *Set up for agents* (LAYOUT8, D117 §6.1, §6.5; D170 point 1), the page's <c>bridge/setup.ts</c>: the screen's
/// half of <c>daoris-driver setup &lt;repository&gt; [--plan]</c>, over the same press (<see cref="SetupPress"/>). The plan
/// first, read and never written; then the press, which adds the rule its session needs and publishes one ask to it, as the
/// person's. Ask Daoris's <c>setup</c> card shows the same plan (<see cref="Shown"/>) and applies the same press.
/// </summary>
/// <remarks>
/// <para><b>Never Daoris writing into the repository</b> (D32): the press asks its own session, and writes a rule in Daoris's
/// own file. WSSETUP7's workspace plan and its <c>register</c> door join this domain.</para>
/// </remarks>
public sealed class SetupRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    // What a press would do today, `--plan`'s twin: what was read on the line, every refusal at once with its code, the rule the
    // press adds, how its work lands, the agent that carries it and the quest's whole text. It reads, and changes nothing.
    [DriverRoute("SETUP_PLAN")]
    private async Task<object?> SetupPlanAsync(IpcRequest request, CancellationToken cancellationToken) =>
        // RPC1e: `daoris-driver setup <repository> --plan` in the verb's own words.
        Flag(request, "said")
            ? await SaidAsync(request, plan: true, cancellationToken).ConfigureAwait(false)
            : Shown(await PlanAsync(request, cancellationToken).ConfigureAwait(false));

    // The press, as the terminal's: the plan read again now, then the rule into the repository's scope and one ask to it; a
    // refusal publishes nothing and adds nothing, and is an answer, its plan beside it. It declares the ask, its quest and the
    // rules (STATEHUB1h, D169).
    [DriverRoute("SETUP_PRESS")]
    [Publishes("SETUP_PRESS", StateScopes.Quests | StateScopes.Asks | StateScopes.Rules)]
    private async Task<object?> SetupPressAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        // RPC1e: `daoris-driver setup <repository>`, the verb's own press and words, a refusal its exit 1.
        if (Flag(request, "said"))
        {
            var said = await SaidAsync(request, plan: false, cancellationToken).ConfigureAwait(false);
            _loop.Nudge();
            return said;
        }

        var plan = await PlanAsync(request, cancellationToken).ConfigureAwait(false);
        var outcome = await SetupPress.ApplyAsync(plan, _context.SetupWorldOf(_loop.Service!), _loop.Home, cancellationToken)
            .ConfigureAwait(false);
        // The session the next look starts is handed the rule, so the look comes now.
        if (outcome.Published) _loop.Nudge();
        return new
        {
            outcome.Published,
            outcome.Message,
            outcome.AskId,
            outcome.QuestId,
            outcome.Added,
            Plan = Shown(plan),
        };
    }

    /// <summary>
    /// The verb's own lines and exit code for a set-up a terminal sent (RPC1e): <see cref="SetupCommand.RunAsync"/> with this Daoris's
    /// world, file, door and home, so the plan and the press say what they say alone.
    /// </summary>
    private async Task<object> SaidAsync(IpcRequest request, bool plan, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var service = _loop.Service ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);
        return await TerminalSaid.RunAsync(output => SetupCommand.RunAsync(
            SetupCommand.ArgsOf(repository, Builds(request), plan), output, _context.SetupWorldOf(service), config, _context.Door(config), _loop.Home,
            DateOnly.FromDateTime(DateTime.Now), cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>The press's plan for the payload's repository, against the driver's file and the door its sessions ride.</summary>
    private async Task<SetupPlan> PlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var service = _loop.Service ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);
        return await SetupPress.PlanAsync(
            _context.SetupWorldOf(service), config, _context.Door(config), repository, DateOnly.FromDateTime(DateTime.Now), Builds(request), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>What the repository builds (AGENTSPLACE1a2), which shapes the quest's safe work: one of the four, or none named.</summary>
    /// <exception cref="DriverException">A word that is none of the four.</exception>
    private static string? Builds(IpcRequest request) =>
        Optional(request, "builds") is { } builds ? SetupBuilds.Is(builds) ? builds : throw new DriverException(SetupBuilds.Problem(builds)) : null;

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

    private static PlaceAsk PlaceAskOf(IpcRequest request) =>
        new(Names(request), Optional(request, "preset") ?? "")
        {
            Opinion = Flag(request, "opinion"),
            Look = Flag(request, "look"),
            Adoption = Flag(request, "adoption"),
            Builds = Optional(request, "builds"),
            Drive = Flag(request, "drive"),
        };

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

    /// <summary>
    /// A plan as the page reads it (LAYOUT8), the page's <c>SetupShown</c>, for the repository's page and Ask Daoris's card:
    /// the repository and its workspace; what was read on its line (the line, its commit to twelve, the layout, adopted,
    /// declares) and which set-up it asks (<c>whole</c>, <c>move</c>, <c>declare</c>, <c>index</c>); the agent that carries it and how its
    /// work lands (<c>merge</c> or <c>branch</c>, with the plugin a branch is handed to); the versions the tools a child finds
    /// answered; the rule the press adds; the quest's title and body; every refusal with its code; and whether a press
    /// publishes. Names and versions only, never a path (D47 §4).
    /// </summary>
    public static object Shown(SetupPlan plan) => new
    {
        plan.Repository,
        plan.Workspace,
        Line = plan.Facts?.Line,
        Commit = plan.Facts is { } facts ? TextBounds.ShortCommit(facts.Commit, 12) : null,
        Layout = plan.Facts?.Layout,
        Adopted = plan.Facts?.Adopted,
        Declares = plan.Facts?.Declares,
        Asks = plan.Case is { } kind and not SetupCase.Done ? kind.ToString().ToLowerInvariant() : null,
        Agent = plan.Adapter,
        Lands = plan.Landing is { } landing ? landing.Rule.Form == LandingForm.Branch ? "branch" : "merge" : null,
        LandsWith = plan.Landing?.Rule.Plugin,
        Node = plan.Tools?.NodeVersion?.Trim(),
        Daoris = plan.Tools is { } tools && SetupPressRules.IsVersion(tools.DaorisVersion) ? tools.DaorisVersion!.Trim() : null,
        Rules = SetupPressRules.RulesOf(plan),
        plan.Title,
        plan.Body,
        Refusals = plan.Refusals.Select(refusal => new { refusal.Code, refusal.Sentence }).ToArray(),
        plan.Pressable,
        plan.Builds,
    };
}
