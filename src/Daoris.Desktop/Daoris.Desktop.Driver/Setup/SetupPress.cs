namespace Daoris.Driver;

/// <summary>
/// The set-up press: what the repository's line and this
/// machine say, each refusal with its unadopted, one ask to one repository through the ask door with its receiver named,
/// as the person's (no intake session runs), and the rule its session needs to run the doctrine tool.
/// </summary>
/// <remarks>
/// <para><b>Never Daoris writing into the repository</b>: a set-up is the repository's own session's work, on its
/// branch, landed by the workspace's rule. The press only asks, and adds a rule, and where the repository is not driven its quest's
/// admission, in Daoris's own files.</para>
///
/// <para><b>The rule is exact verbs</b>, never a runner, since a session on the protocol door cannot ask for
/// another: no <c>upstream</c> for a set-up, and only <c>upstream --all</c> for a reconcile, which outside the canon's
/// own checkout composes a request's words and writes nothing; and no management verb, since a set-up registers
/// nothing. <c>sync --force</c> is in: on a set-up's branch, in a tree grown from the line, what it
/// replaces is committed on the line and can be taken back. As <c>Bash(…)</c>; <c>PowerShell(…)</c> waits on the
/// canary that says a driven session on Windows uses that tool.</para>
///
/// <para><b>"Already set up" reads the line, not <c>check</c></b>: Daoris runs no doctrine command into a repository,
/// and a second <c>check</c> would twin the CLI's hardest table. What the line shows of it is read
/// (<see cref="LayoutFacts.Clean"/>); the rest is that repository's own gate.</para>
///
/// <para><b>The use case, against its port</b>: the world comes through <see cref="ISetupWorld"/>, and the rules it applies
/// are <see cref="SetupPressRules"/>', which its callers name.</para>
/// </remarks>
public static class SetupPress
{
    /// <summary>What a press on <paramref name="repository"/> would do today. Reads, and writes nothing.</summary>
    /// <param name="unadopted">Whether this machine's driven sessions may drive a repository that has not adopted (<see cref="Adoption"/>).</param>
    public static Task<SetupPlan> PlanAsync(
        ISetupWorld world, DriverConfig config, bool unadopted, string repository, DateOnly day, CancellationToken ct = default) =>
        PlanAsync(world, config, unadopted, repository, day, builds: null, ct);

    /// <summary>
    /// What a press would do today, its quest's safe work shaped by what the repository builds (<see cref="SetupBuilds"/>),
    /// one of the four or null for none said. Reads, and writes nothing.
    /// </summary>
    public static async Task<SetupPlan> PlanAsync(
        ISetupWorld world, DriverConfig config, bool unadopted, string repository, DateOnly day, string? builds, CancellationToken ct = default)
    {
        var name = repository.Trim();
        var registry = await world.RegistryAsync(ct).ConfigureAwait(false);
        var row = registry.FirstOrDefault(repo => string.Equals(repo.Repository, name, StringComparison.OrdinalIgnoreCase));
        if (row is null) return new SetupPlan(name) { Refusals = [NotOnRegistry(name)] };

        var plan = new SetupPlan(row.Repository)
        {
            Workspace = row.Workspace,
            Adapter = ActorChoices.BuildAgent(config),
            Builds = builds,
            Alone = !config.Drivable.Contains(row.Repository, StringComparer.OrdinalIgnoreCase),
        };
        var refusals = ConfigurationRefusals(config, unadopted, row, world.IsolationOf(config, row) is not null).ToList();
        if (row.Root is null) return plan with { Refusals = refusals };

        var line = await world.ReadLineAsync(row, config, ct).ConfigureAwait(false);
        if (line.Facts is null)
        {
            refusals.Add(new(SetupRefusals.NoHistory,
                $"`{row.Repository}` has no git history to set up here: {line.Problem ?? "its line could not be read"}."));
        }

        var tools = await world.ToolsAsync(ct).ConfigureAwait(false);
        if (SetupPressRules.NodeRefusal(tools) is { } node) refusals.Add(node);
        if (SetupPressRules.ToolRefusal(tools) is { } tool) refusals.Add(tool);

        var facts = line.Facts;
        var kind = facts is null ? (SetupCase?)null : SetupPressRules.CaseOf(facts);
        if (kind == SetupCase.Done)
        {
            refusals.Add(new(SetupRefusals.AlreadySetUp,
                $"`{row.Repository}` is already set up: on its line it has taken up the doctrine on the agents layout, "
                + $"finished, declares what it owns, and names its index of where things are, `{facts!.DeclaredIndex}`."));
        }

        // The index quest names its generator from what the repository is built with, or there is none to ask.
        var description = row.Root is { } root ? world.Describe(root) : null;
        IndexForm? form = null;
        if (kind == SetupCase.Index)
        {
            var (found, problem) = IndexForms.For(description?.Stack ?? [], facts!.ToolFiles.Contains);
            form = found;
            if (problem is not null)
            {
                refusals.Add(new(SetupRefusals.NoIndexForm,
                    $"`{row.Repository}` is set up and names no index of where things are, and the press cannot name its "
                    + $"generator: {problem}. Ask it by hand, *{SetupQuests.Index}*, naming the generator's path and its two "
                    + "commands, and add those commands to its rules."));
            }
        }

        if (OpenRefusal(await world.QuestsAsync(ct).ConfigureAwait(false), row.Repository) is { } open) refusals.Add(open);

        plan = plan with { Facts = facts, Tools = tools, Case = kind, Index = form, Refusals = refusals };
        if (facts is null || kind is null or SetupCase.Done || (kind == SetupCase.Index && form is null)) return plan;

        var neighbours = registry
            .Where(repo => repo.Workspace == row.Workspace && !string.Equals(repo.Repository, row.Repository, StringComparison.OrdinalIgnoreCase))
            .Select(repo => repo.Repository)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var (title, body) = SetupBrief.Compose(new SetupBriefInput(
            kind.Value, day, row.Repository, facts, tools.DaorisVersion is { Length: > 0 } version ? version : "(no version was found)",
            description, await world.EntriesAsync(row.Repository, ct).ConfigureAwait(false), neighbours, form, builds));
        return plan with { Title = title, Body = body };
    }

    /// <summary>
    /// What a reconcile press on <paramref name="repository"/> would do today: the press's own refusals, in its order, and
    /// one open per repository, beside any set-up; then a quest whose session runs <c>daoris reconcile</c> and decides each item,
    /// the person's standing answer for it among them. Reads, and writes nothing.
    /// </summary>
    /// <remarks>
    /// For a repository the registry marks adopted. Whether the canon has moved is the doctrine tool's answer, its
    /// <c>status --json</c> for the checkout (<see cref="ISetupWorld.StatusAsync"/>), held on the plan and said in the body; the
    /// driver keeps no copy of the tool's table. A status that moved nothing refuses nothing, since the standing answer's
    /// lines are the reconcile's too: where it is offered is the page's door, by <see cref="DoctrineStatus.Offers"/>.
    /// </remarks>
    public static async Task<SetupPlan> PlanReconcileAsync(
        ISetupWorld world, DriverConfig config, bool unadopted, string repository, DateOnly day, CancellationToken ct = default)
    {
        var name = repository.Trim();
        var registry = await world.RegistryAsync(ct).ConfigureAwait(false);
        var row = registry.FirstOrDefault(repo => string.Equals(repo.Repository, name, StringComparison.OrdinalIgnoreCase));
        if (row is null) return new SetupPlan(name) { Refusals = [NotOnRegistry(name)] };

        var plan = new SetupPlan(row.Repository)
        {
            Workspace = row.Workspace,
            Adapter = ActorChoices.BuildAgent(config),
            Alone =!config.Drivable.Contains(row.Repository, StringComparer.OrdinalIgnoreCase),
            Case = SetupCase.Reconcile,
        };
        var refusals = ConfigurationRefusals(config, unadopted, row, world.IsolationOf(config, row) is not null).ToList();
        if (row.Root is null) return plan with { Refusals = refusals };

        var line = await world.ReadLineAsync(row, config, ct).ConfigureAwait(false);
        if (line.Facts is null)
        {
            refusals.Add(new(SetupRefusals.NoHistory,
                $"`{row.Repository}` has no git history to reconcile here: {line.Problem ?? "its line could not be read"}."));
        }
        else if (!row.Adopted || !line.Facts.Adopted || !line.Facts.Locked)
        {
            refusals.Add(new(SetupRefusals.NotAdopted,
                $"`{row.Repository}` has not taken up the shared doctrine{(row.Adopted ? " on its line" : "")}, so there is nothing to "
                + $"reconcile: its set-up comes first, *{SetupQuests.SetUp}*."));
        }

        var tools = await world.ToolsAsync(ct).ConfigureAwait(false);
        var toolRefusals = SetupPressRules.ToolRefusals(tools);
        refusals.AddRange(toolRefusals);

        // The tool's status, asked of an adopted, synced checkout the tool can be run in.
        DoctrineStatus? doctrine = null;
        if (row.Adopted && line.Facts is { Adopted: true, Locked: true } && toolRefusals.Count == 0)
        {
            var read = await world.StatusAsync(row, ct).ConfigureAwait(false);
            doctrine = read.Status;
            if (doctrine is null)
            {
                refusals.Add(new(SetupRefusals.StatusUnread,
                    $"this machine's doctrine tool printed no status for `{row.Repository}`: {(read.Problem ?? "it said nothing").TrimEnd('.')}."));
            }
            else if (doctrine.NewerLock is { } newer)
            {
                refusals.Add(new(SetupRefusals.NewerLock,
                    $"`{row.Repository}`'s lock was written by a newer shared doctrine, {newer}, than the {doctrine.Carried ?? "one"} this machine's "
                    + "doctrine tool carries, which never rolls a lock back: a reconcile waits for a doctrine tool that carries it."));
            }
        }

        if (OpenRefusal(await world.QuestsAsync(ct).ConfigureAwait(false), row.Repository) is { } open) refusals.Add(open);

        plan = plan with { Facts = line.Facts, Tools = tools, Doctrine = doctrine, Refusals = refusals };
        if (refusals.Count > 0) return plan;

        var (title, body) = SetupBrief.Compose(new SetupBriefInput(
            SetupCase.Reconcile, day, row.Repository, line.Facts!, tools.DaorisVersion is { Length: > 0 } version ? version : "(no version was found)",
            Description: null, Entries: [], Neighbours: [], Standing: config.StandingFor(row.Repository), Doctrine: doctrine));
        return plan with { Title = title, Body = body };
    }

    /// <summary>The refusal of a repository this machine's registry does not hold.</summary>
    public static SetupRefusal NotOnRegistry(string repository) =>
        new(SetupRefusals.NotOnRegistry,
            $"`{repository}` is not on this machine's registry. Add it on Repositories → *Add a repository*, or with "
            + "`daoris repository import <folder> --workspace <name>`, then press again.");

    /// <summary>
    /// The refusals the configuration and the registry's row alone say, in the press's order: no checkout here, no door that
    /// hands an unadopted repository's session its connector, and its sessions not in trees of their own. Driving is not one: a set-up
    /// where the repository is not driven is admitted alone (<see cref="SetupPlan.Alone"/>). Read by the press and by a
    /// place's plan over the configuration it would leave, so the two refuse alike.
    /// </summary>
    /// <param name="apart">Whether the workflow the repository follows sets its sessions apart, each in a tree of its own.</param>
    public static IReadOnlyList<SetupRefusal> ConfigurationRefusals(DriverConfig config, bool unadopted, RepoView row, bool apart)
    {
        if (row.Root is null)
        {
            return
            [
                new(SetupRefusals.NoCheckout,
                    $"`{row.Repository}` has no checkout here: its row is a teammate's registration, and its own "
                    + "machine's driver can be asked to set it up."),
            ];
        }

        var refusals = new List<SetupRefusal>();
        // Its code stays `not-driven`, a stored shape the machine log's `setup.skipped` keeps.
        if (!row.Adopted && !unadopted)
        {
            refusals.Add(new(SetupRefusals.NotDriven,
                $"`{row.Repository}` is not driven here: it has not adopted, so only a session on the protocol door is "
                + $"handed the connector it takes the quest with, and this machine's sessions start on `{ActorChoices.BuildAgent(config)}`, "
                + "which rides the pipe. Choose an ACP agent for Build in Agents → Roles, or with `daoris role set build \"<agent>\"`."));
        }

        // A tree per session is the repository's workflow's: its `isolate` step.
        if (!apart)
        {
            refusals.Add(new(SetupRefusals.NoOwnTree,
                $"`{row.Repository}`'s sessions run in its checkout, not a tree of their own, and a set-up rewrites what "
                + "every later session there reads, so it lands on a branch of its own. Start the workflow it follows with "
                + "*Work apart* (its `isolate` step) in the workflow's editor, or choose a set-up preset whose workflow holds one."));
        }

        return refusals;
    }

    /// <summary>A set-up already open or taken for <paramref name="repository"/>, refused as the press refuses it; null where none is.</summary>
    public static SetupRefusal? OpenRefusal(IEnumerable<QuestView> quests, string repository) =>
        SetupQuests.OpenFor(quests, repository) is { } open
            ? new(SetupRefusals.Open, $"a set-up is already {open.Status.ToLowerInvariant()} for `{repository}`: `#{open.Id}`, {open.Title}.")
            : null;

    /// <summary>
    /// Press: the rule into the repository's scope, then one ask to it, its quest admitted alone where the repository is not driven
    /// (<see cref="PublishAsync"/>). A refusal from the service takes back the rules this press added, and keeps any the person already
    /// had. A plan that is not pressable is never applied.
    /// </summary>
    /// <exception cref="DriverException">Daoris's rules file could not be read, so no rule is written over it.</exception>
    public static async Task<SetupOutcome> ApplyAsync(SetupPlan plan, ISetupWorld world, string home, CancellationToken ct = default)
    {
        if (!plan.Pressable || plan.Workspace is null)
        {
            return new(false, plan.Refusals.Count > 0 ? plan.Refusals[0].Sentence : "nothing to ask.", null, null, []);
        }

        // The rule first, so the session the next look starts is handed it: a session cannot ask for one.
        var added = new List<string>();
        PermissionChange.Change(home, file =>
        {
            var held = file.Repositories
                .Where(pair => string.Equals(pair.Key.Trim(), plan.Repository, StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value.Allow)
                .ToHashSet(StringComparer.Ordinal);
            added.AddRange(SetupPressRules.RulesOf(plan).Where(rule => !held.Contains(rule)));
            return added.Aggregate(file, (rules, rule) => PermissionRules.Add(rules, RuleScope.Repository, plan.Repository, RuleList.Allow, rule));
        });

        var asked = await PublishAsync(plan, world, home, ct).ConfigureAwait(false);
        if (asked.Published) return asked with { Added = added };

        PermissionChange.Change(home, after => added.Aggregate(after, (rules, rule) => PermissionRules.Remove(rules, RuleScope.Repository, plan.Repository, rule)));
        return asked;
    }

    /// <summary>
    /// The ask alone, as a workspace plan's tick publishes it: its rule is the workspace's, added once by the plan's
    /// press, so nothing is added here. Where the repository is not driven, the quest it published is admitted alone in
    /// <paramref name="home"/>'s <c>driver.json</c>, after the publication that names it. A plan that is not pressable is
    /// never asked.
    /// </summary>
    public static async Task<SetupOutcome> PublishAsync(SetupPlan plan, ISetupWorld world, string home, CancellationToken ct = default)
    {
        if (!plan.Pressable || plan.Workspace is null)
        {
            return new(false, plan.Refusals.Count > 0 ? plan.Refusals[0].Sentence : "nothing to ask.", null, null, []);
        }

        var answer = await world.PublishAsync(plan.Workspace, plan.Sentence!, plan.Repository, ct).ConfigureAwait(false);
        var quest = answer.Ok ? answer.QuestId : null;
        var message = answer.Ok && plan.Alone && Admit(home, plan.Repository, quest) is { } unadmitted ? $"{answer.Message} {unadmitted}" : answer.Message;
        return new(answer.Ok, message, answer.AskId, quest, []);
    }

    /// <summary>
    /// The quest admitted alone in the driver's file, or why it is not: then it waits, as the rest of its repository's work does, and the
    /// sentence says so.
    /// </summary>
    private static string? Admit(string home, string repository, string? quest)
    {
        var waits = $"waits as the rest of `{repository}`'s work does until it is driven here.";
        if (quest is not { Length: > 0 }) return $"The service named no quest for it, so none is admitted alone: it {waits}";

        try
        {
            var path = DriverConfig.ResolvePath(home);
            DriverConfig.Load(path).WithAdmitted(quest, repository).Save(path);
            return null;
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return $"Its admission could not be written ({error.Message.TrimEnd('.', ' ')}), so `#{quest}` {waits}";
        }
    }
}
