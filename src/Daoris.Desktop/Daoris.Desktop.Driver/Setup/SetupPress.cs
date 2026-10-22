namespace Daoris.Driver;

/// <summary>
/// The set-up press (LAYOUT7; D117 §6.1–§6.3 as D124 §2.1 and §2.4 amend them): what the repository's line and this
/// machine say, each refusal with its door, one ask to one repository through the ask door with its receiver named,
/// as the person's (no intake session runs), and the rule its session needs to run the doctrine tool.
/// </summary>
/// <remarks>
/// <para><b>Never Daoris writing into the repository</b> (D32): a set-up is the repository's own session's work, on its
/// branch, landed by the workspace's rule. The press only asks, and adds a rule in Daoris's own file.</para>
///
/// <para><b>The rule is exact verbs</b> (D124 §2.4), never a runner, since a session on the protocol door cannot ask for
/// another (D52): no <c>upstream</c>, which writes into the canon inside the install, and no management verb, since a
/// set-up registers nothing. <c>sync --force</c> is in: on a set-up's branch, in a tree grown from the line, what it
/// replaces is committed on the line and can be taken back. As <c>Bash(…)</c>; <c>PowerShell(…)</c> waits on the
/// canary that says a driven session on Windows uses that tool.</para>
///
/// <para><b>"Already set up" reads the line, not <c>check</c></b>: Daoris runs no doctrine command into a repository
/// (D32), and a second <c>check</c> would twin the CLI's hardest table. What the line shows of it is read
/// (<see cref="LayoutFacts.Clean"/>); the rest is that repository's own gate.</para>
///
/// <para><b>The use case, against its port</b>: the world comes through <see cref="ISetupWorld"/>, and the rules it applies
/// are <see cref="SetupPressRules"/>', which its callers name.</para>
/// </remarks>
public static class SetupPress
{
    /// <summary>What a press on <paramref name="repository"/> would do today. Reads, and writes nothing.</summary>
    /// <param name="door">The door this machine's driven sessions ride: the configured agent's wire.</param>
    public static async Task<SetupPlan> PlanAsync(
        ISetupWorld world, DriverConfig config, SessionWire door, string repository, DateOnly day, CancellationToken ct = default)
    {
        var name = repository.Trim();
        var registry = await world.RegistryAsync(ct).ConfigureAwait(false);
        var row = registry.FirstOrDefault(repo => string.Equals(repo.Repository, name, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return new SetupPlan(name)
            {
                Refusals =
                [
                    new(SetupRefusals.NotOnRegistry,
                        $"`{name}` is not on this machine's registry. Add it on Repositories → *Add a repository*, or with "
                        + "`daoris import <folder> --workspace <name>`, then press again."),
                ],
            };
        }

        var plan = new SetupPlan(row.Repository)
        {
            Workspace = row.Workspace,
            Landing = LandingRules.Choose(config, row.Repository, row.Workspace),
            Adapter = config.Adapter,
        };
        if (row.Root is null)
        {
            return plan with
            {
                Refusals =
                [
                    new(SetupRefusals.NoCheckout,
                        $"`{row.Repository}` has no checkout here: its row is a teammate's registration, and its own "
                        + "machine's driver can be asked to set it up."),
                ],
            };
        }

        var refusals = new List<SetupRefusal>();
        // HELPSETUP1: driving and a tree per session are the repository's own, on its Setup → Driving since UX6f (D150 §4.2).
        if (!config.Drivable.Contains(row.Repository, StringComparer.OrdinalIgnoreCase))
        {
            refusals.Add(new(SetupRefusals.NotDriven,
                $"`{row.Repository}` is not driven here: it is not opted into driving on this machine, and only a driven "
                + "session carries a set-up. Drive it under Repositories → the repository's page → Setup → Driving, or with "
                + $"`daoris driver drive {row.Repository}`."));
        }
        else if (!row.Adopted && door != SessionWire.Acp)
        {
            refusals.Add(new(SetupRefusals.NotDriven,
                $"`{row.Repository}` is not driven here: it has not adopted, so only a session on the protocol door is "
                + $"handed the connector it takes the quest with, and this machine's sessions start on `{config.Adapter}`, "
                + "which rides the pipe. Choose an ACP agent with `daoris driver adapter <agent>`, in Settings → Driver."));
        }

        if (!config.OpensOwnTree(row.Repository))
        {
            refusals.Add(new(SetupRefusals.NoOwnTree,
                $"`{row.Repository}`'s sessions run in its checkout, not a tree of their own, and a set-up rewrites what "
                + "every later session there reads, so it lands on a branch of its own. Give it trees under Repositories → "
                + $"the repository's page → Setup → Driving, or with `daoris driver trees {row.Repository} on`."));
        }

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

        // ORIENT2c: the index quest names its generator from what the repository is built with, or there is none to ask.
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

        var open = (await world.QuestsAsync(ct).ConfigureAwait(false)).FirstOrDefault(quest =>
            string.Equals(quest.To, row.Repository, StringComparison.OrdinalIgnoreCase)
            && quest.Status is "Open" or "Taken"
            && SetupQuests.IsSetup(quest.Title));
        if (open is not null)
        {
            refusals.Add(new(SetupRefusals.Open,
                $"a set-up is already {open.Status.ToLowerInvariant()} for `{row.Repository}`: `#{open.Id}`, {open.Title}."));
        }

        plan = plan with { Facts = facts, Tools = tools, Case = kind, Index = form, Refusals = refusals };
        if (facts is null || kind is null or SetupCase.Done || (kind == SetupCase.Index && form is null)) return plan;

        var neighbours = registry
            .Where(repo => repo.Workspace == row.Workspace && !string.Equals(repo.Repository, row.Repository, StringComparison.OrdinalIgnoreCase))
            .Select(repo => repo.Repository)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var (title, body) = SetupBrief.Compose(new SetupBriefInput(
            kind.Value, day, row.Repository, facts, tools.DaorisVersion is { Length: > 0 } version ? version : "(no version was found)",
            description, await world.EntriesAsync(row.Repository, ct).ConfigureAwait(false), neighbours, form));
        return plan with { Title = title, Body = body };
    }

    /// <summary>
    /// Press: the rule into the repository's scope, then one ask to it. A refusal from the service takes back the rules
    /// this press added, and keeps any the person already had. A plan that is not pressable is never applied.
    /// </summary>
    /// <exception cref="DriverException">Daoris's rules file could not be read, so no rule is written over it.</exception>
    public static async Task<SetupOutcome> ApplyAsync(SetupPlan plan, ISetupWorld world, string home, CancellationToken ct = default)
    {
        if (!plan.Pressable || plan.Workspace is null)
        {
            return new(false, plan.Refusals.Count > 0 ? plan.Refusals[0].Sentence : "nothing to ask.", null, null, []);
        }

        // The rule first, so the session the next look starts is handed it: a session cannot ask for one (D52).
        var file = PermissionRules.Load(home);
        var held = file.Repositories
            .Where(pair => string.Equals(pair.Key.Trim(), plan.Repository, StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value.Allow)
            .ToHashSet(StringComparer.Ordinal);
        var added = SetupPressRules.RulesOf(plan).Where(rule => !held.Contains(rule)).ToList();
        PermissionRules.Save(home, added.Aggregate(file, (rules, rule) => PermissionRules.Add(rules, RuleScope.Repository, plan.Repository, RuleList.Allow, rule)));

        var asked = await PublishAsync(plan, world, ct).ConfigureAwait(false);
        if (asked.Published) return asked with { Added = added };

        var after = PermissionRules.Load(home);
        PermissionRules.Save(home, added.Aggregate(after, (rules, rule) => PermissionRules.Remove(rules, RuleScope.Repository, plan.Repository, rule)));
        return asked;
    }

    /// <summary>
    /// The ask alone, as a workspace plan's tick publishes it (WSSETUP6): its rule is the workspace's, added once by the plan's
    /// press (D124 §4.3), so nothing is added here. A plan that is not pressable is never asked.
    /// </summary>
    public static async Task<SetupOutcome> PublishAsync(SetupPlan plan, ISetupWorld world, CancellationToken ct = default)
    {
        if (!plan.Pressable || plan.Workspace is null)
        {
            return new(false, plan.Refusals.Count > 0 ? plan.Refusals[0].Sentence : "nothing to ask.", null, null, []);
        }

        var answer = await world.PublishAsync(plan.Workspace, plan.Sentence!, plan.Repository, ct).ConfigureAwait(false);
        return new(answer.Ok, answer.Message, answer.AskId, answer.Ok ? answer.QuestId : null, []);
    }
}
