using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The `agents rules` and `agents proposals` verbs: the rules a harness reads and the proposals made for them.
/// </summary>
internal static class AgentRulesVerb
{
    internal static string RulesHome(AgentsWorld world, HomeEntry entry) =>
        world.Instance.Home ?? throw new AgentWords($"{DaorisHome.Sentence} (wanted: {entry.New})");

    /// <summary><c>rules [list] | allow|ask|deny|remove &lt;rule&gt; | default &lt;id&gt; on|off</c>, the words after <c>rules</c>.</summary>
    internal static AgentPress? Rules(IReadOnlyList<string> argv, AgentsWorld world, out int exit)
    {
        exit = 0;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var verb = argv.Count > 0 ? argv[0] : "list";
        var home = RulesHome(world, HomeEntry.Permissions);
        var file = PermissionRules.Load(home);

        var workspace = AgentsCommand.FlagValue(argv, "--workspace");
        var repository = AgentsCommand.FlagValue(argv, "--repository");
        if (workspace is not null && repository is not null)
        {
            throw new AgentWords("a rule reaches one scope — name a workspace with `--workspace` or a repository with `--repository`, not both.");
        }

        var scope = workspace is not null ? RuleScope.Workspace : repository is not null ? RuleScope.Repository : RuleScope.Machine;
        var name = workspace ?? repository;
        var where = scope == RuleScope.Machine
            ? "every session on this machine"
            : $"{(scope == RuleScope.Workspace ? "workspace" : "repository")} `{name}`";

        string[] Needed(string what)
        {
            var bare = AgentsCommand.Operands(argv, new HashSet<string>(StringComparer.Ordinal) { "--workspace", "--repository" }).Skip(1).ToArray();
            return bare.Length > 0 ? bare : throw new AgentWords($"`daoris agent rules {verb}` needs a {what}.");
        }

        // Judged here before anything is sent, in the rules' own words, as the instance would judge it.
        static void Judged(Action judge)
        {
            try
            {
                judge();
            }
            catch (DriverException refused)
            {
                throw new AgentWords(refused.Message);
            }
        }

        JsonObject Payload(string action)
        {
            var payload = new JsonObject { ["action"] = action, ["scope"] = scope.ToString().ToLowerInvariant() };
            if (name is not null) payload["name"] = name;
            return payload;
        }

        AgentPress Press(JsonObject payload, Func<PermissionFile, PermissionFile> edit, Func<int> said) =>
            new($"rules {verb}", AgentRoutes.Rule, false, payload, _ =>
            {
                PermissionRules.Save(home, edit(PermissionRules.Load(home)));
                return Task.FromResult<object?>(null);
            }, _ => null, _ => said());

        switch (verb)
        {
            case "list":
                exit = RulesList(home, file, world);
                return null;

            case "allow" or "ask" or "deny":
            {
                var rule = Needed("<rule>")[0];
                var list = verb switch { "allow" => RuleList.Allow, "ask" => RuleList.Ask, _ => RuleList.Deny };
                Judged(() => PermissionRules.Add(PermissionFile.Empty, scope, name, list, rule));
                var payload = Payload("add");
                payload["list"] = verb;
                payload["rule"] = rule;
                return Press(payload, held => PermissionRules.Add(held, scope, name, list, rule), () =>
                {
                    Say($"{AgentsCommand.Prefix}: `{rule}` is now {(verb == "allow" ? "allowed" : verb == "ask" ? "asked for" : "denied")} for {where}.");
                    Say("  Sessions started from now on are handed it; one already running keeps what it began with.");
                    if (verb == "ask") Say("  An ask is a refusal for anything Daoris starts — nobody is there to answer it.");
                    return 0;
                });
            }

            case "remove":
            {
                var rule = Needed("<rule>")[0];
                Judged(() => PermissionRules.Remove(PermissionFile.Empty, scope, name, rule));
                var payload = Payload("remove");
                payload["rule"] = rule;
                return Press(payload, held => PermissionRules.Remove(held, scope, name, rule), () =>
                {
                    Say($"{AgentsCommand.Prefix}: `{rule}` is no longer one of the rules for {where}.");
                    return 0;
                });
            }

            case "default":
            {
                var operands = Needed("<id>");
                var id = operands[0];
                var state = operands.Length > 1 ? operands[1] : null;
                if (state is not ("on" or "off"))
                {
                    throw new AgentWords($"say whether the default `{id}` is on or off: `daoris agent rules default {id} on|off`.");
                }

                var on = state == "on";
                Judged(() => PermissionRules.SwitchDefault(PermissionFile.Empty, id, on));
                return new AgentPress($"rules {verb}", AgentRoutes.Rule, false, new JsonObject { ["action"] = "default", ["id"] = id, ["on"] = on }, _ =>
                {
                    PermissionRules.Save(home, PermissionRules.SwitchDefault(PermissionRules.Load(home), id, on));
                    return Task.FromResult<object?>(null);
                }, _ => null, _ =>
                {
                    Say($"{AgentsCommand.Prefix}: the default `{id}` is {state} on this machine.");
                    return 0;
                });
            }

            default:
                throw new AgentWords($"unknown rules verb '{verb}' — one of: list, allow, ask, deny, remove, default, proposals, accept, decline");
        }
    }

    /// <summary>Every default with whether it is on, then each scope's rules, as the file stands.</summary>
    internal static int RulesList(string home, PermissionFile file, AgentsWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        Say($"What an agent Daoris starts may do on this machine — {PermissionRules.PathOf(home)}");
        Say("  Claude Code's own permission rules. Sessions on other agents are handed none of these.");
        if (file.Problem is not null) Say($"  ⚠ {file.Problem}");
        Say("");
        Say("Defaults");
        foreach (var shipped in PermissionRules.Defaults)
        {
            var on = PermissionRules.IsOn(file, shipped);
            var what = shipped.Hook is { } hook ? $"a hook on {hook}"
                : shipped.Rules.Count == 0 ? "a sentence to auto mode's classifier"
                : string.Join(", ", shipped.Rules);
            Say($"  {shipped.Id,-10} {(on ? "on " : "off")}  {shipped.List.ToString().ToLowerInvariant()}  {what}");
        }

        void Show(string title, RuleLists held)
        {
            if (held.IsEmpty) return;
            Say("");
            Say(title);
            foreach (var rule in held.Allow) Say($"  allow  {rule}");
            foreach (var rule in held.Ask) Say($"  ask    {rule}");
            foreach (var rule in held.Deny) Say($"  deny   {rule}");
        }

        Show("This machine", file.Machine);
        foreach (var (circle, held) in file.Workspaces) Show($"workspace `{circle}`", held);
        foreach (var (repository, held) in file.Repositories) Show($"repository `{repository}`", held);
        return 0;
    }

    /// <summary><c>rules proposals | accept &lt;id&gt; | decline &lt;id&gt; [--note "…"]</c>, the words after <c>rules</c>.</summary>
    internal static AgentPress? Proposals(IReadOnlyList<string> argv, AgentsWorld world, out int exit)
    {
        exit = 0;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var verb = argv[0];
        var home = RulesHome(world, HomeEntry.RuleProposals);
        if (verb == "proposals")
        {
            exit = ProposalsList(home, world);
            return null;
        }

        var note = AgentsCommand.FlagValue(argv, "--note");
        string? id = null;
        for (var at = 1; at < argv.Count && id is null; at++)
        {
            if (!argv[at].StartsWith("--", StringComparison.Ordinal) && argv[at - 1] != "--note") id = argv[at];
        }

        if (id is null) throw new AgentWords($"`daoris agent rules {verb}` needs the proposal's id — `daoris agent rules proposals` lists them.");

        var accept = verb == "accept";
        RuleProposal proposal;
        try
        {
            // None by that id, or one already history, is said before anything is sent, as the TypeScript said it.
            proposal = RuleProposals.Open(home, id);
        }
        catch (DriverException refused)
        {
            throw new AgentWords(refused.Message);
        }

        var payload = new JsonObject { ["id"] = proposal.Id, ["accept"] = accept };
        if (note is not null) payload["note"] = note;
        return new AgentPress($"rules {verb}", AgentRoutes.Proposal, false, payload, _ =>
        {
            RuleProposals.Answer(home, id, accept, note, world.Now());
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            Say(accept
                ? $"{AgentsCommand.Prefix}: accepted #{proposal.Id} — {RuleProposals.Describe(proposal.Change)} is now in the rules."
                : $"{AgentsCommand.Prefix}: declined #{proposal.Id} — the rules are as they were.");
            if (accept) Say("  Sessions started from now on are handed it; one already running keeps what it began with.");
            return 0;
        });
    }

    /// <summary>Every proposal, newest first, with its state, its author and its reason, and how to answer those that wait.</summary>
    internal static int ProposalsList(string home, AgentsWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var proposals = RuleProposals.Load(home);
        Say($"Agents' proposals to change these rules — {DaorisHome.Entry(home, HomeEntry.RuleProposals)}");
        Say("  A narrowing applies at the driver's next tick; a widening waits for your yes.");
        if (proposals.Count == 0)
        {
            Say("");
            Say("No agent has proposed a change.");
            return 0;
        }

        foreach (var proposal in proposals)
        {
            var state = proposal.State.ToString().ToLowerInvariant();
            Say("");
            Say($"#{proposal.Id}  {state,-9} {RuleProposals.Describe(proposal.Change)}");
            Say($"  from {RuleProposals.Author(proposal)}, {Moments.Write(proposal.Proposed)}");
            Say($"  why: {proposal.Why}");
            if (proposal.SettledBy is { } by) Say($"  {state} by {by}{(proposal.Note is { } note ? $": {note}" : "")}");
        }

        var open = proposals.Count(proposal => proposal.State is ProposalState.Proposed or ProposalState.Waiting);
        if (open > 0)
        {
            Say("");
            Say($"{open} {(open == 1 ? "waits" : "wait")} for an answer: `daoris agent rules accept <id>` or "
                + "`daoris agent rules decline <id> [--note \"…\"]`.");
        }

        return 0;
    }

    // ——— the wiring's verbs: trust and settings
}
