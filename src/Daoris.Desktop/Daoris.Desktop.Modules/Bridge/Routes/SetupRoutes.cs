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
        string[] args = plan ? [repository, "--plan"] : [repository];
        return await TerminalSaid.RunAsync(output => SetupCommand.RunAsync(
            args, output, _context.SetupWorldOf(service), config, _context.Door(config), _loop.Home, DateOnly.FromDateTime(DateTime.Now),
            cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>The press's plan for the payload's repository, against the driver's file and the door its sessions ride.</summary>
    private async Task<SetupPlan> PlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var service = _loop.Service ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);
        return await SetupPress.PlanAsync(
            _context.SetupWorldOf(service), config, _context.Door(config), repository, DateOnly.FromDateTime(DateTime.Now), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A plan as the page reads it (LAYOUT8), the page's <c>SetupShown</c>, for the repository's page and Ask Daoris's card:
    /// the repository and its workspace; what was read on its line (the line, its commit to twelve, the layout, adopted,
    /// declares) and which set-up it asks (<c>whole</c>, <c>move</c>, <c>declare</c>); the agent that carries it and how its
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
        Rules = SetupPressRules.Rules,
        plan.Title,
        plan.Body,
        Refusals = plan.Refusals.Select(refusal => new { refusal.Code, refusal.Sentence }).ToArray(),
        plan.Pressable,
    };
}
