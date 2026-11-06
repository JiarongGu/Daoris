using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris role</c> (FOUNDATION1y-i, the foundation design §2.7): the terminal's door into the role catalogue, the window
/// twin of Agents → <i>Roles</i> over <c>SET_ROLE</c>. <c>role</c> says every role, <c>role &lt;id&gt;</c> one;
/// <c>role set</c> adds or changes a working role (its names, work, scope and choices), <c>role remove</c> takes one out and
/// <c>role default</c> gives a seeded one back its seed. Each change is judged by the role door's judge before anything is sent or
/// written, so a refusal is said in the judge's sentence.
/// </summary>
public sealed class RoleVerb : ChoiceVerb
{
    public override string Word => "role";

    /// <summary>The words that follow <c>role</c> to change the catalogue; the CLI's usage names them (held by <c>RoleVerbUsageTests</c>).</summary>
    internal static readonly string[] Changes = ["set", "remove", "default", "policy", "rules"];

    private const string Grammar =
        "`driver role set <id> [--name \"…\"] [--name-in <tag>=\"…\"]… [--takes <work,…>] [--scope machine|workspace:<id>|repository:<id>] "
        + "[\"<agent> [<model>|-] [<effort>|-]\" … | off]`, an appointed role's `[--acts <act,…>] [--holder terminal|rules|none] [--window <1-60>]`, "
        + "`driver role remove <id>`, `driver role default <id>` or `driver role default --all`";

    /// <summary><c>role suggested [--use]</c> (MODELROLE1g): the choices Daoris suggests for the seeded roles, said, or with <c>--use</c> set through the role door; null for the rest.</summary>
    public override Task<int>? RunAsync(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world, CancellationToken ct) =>
        args.Count > 1 && args[1] == "suggested" ? Task.FromResult(Suggested(args, world)) : null;

    private static int Suggested(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var said = world.Instance.Out;
        if (args.Skip(2).Any(word => word != "--use"))
        {
            world.Instance.Error.WriteLine($"{DriverChoicesCommand.Prefix}: `driver role suggested` takes `--use` alone.");
            return 2;
        }

        var use = args.Contains("--use");
        var table = ActorSuggestion.For(null);
        said.WriteLine($"daoris: the choices Daoris suggests (the reference of {ActorSuggestion.Dated}); a suggestion is never applied by itself");
        foreach (var each in table) said.WriteLine($"  {each.Role,-12}{Choices(each.Choices)}");
        if (!use)
        {
            said.WriteLine("  This can spend more than the order it replaces. Use them for new work on this machine: `daoris role suggested --use`.");
            said.WriteLine("  The window leaves out an agent this machine has not set up; here the choices are set as listed.");
            return 0;
        }

        try
        {
            var staffing = RoleStaffing.Of(world.ConfigPath) ?? throw new ChoiceWords(Unkept(world));
            var catalogue = RoleCatalog.Read(staffing.Location);
            var set = new List<string>();
            foreach (var each in table)
            {
                var slot = catalogue.Slots.FirstOrDefault(found => found.Id == each.Role);
                if (slot?.Role is null || slot.State is RoleState.Removed or RoleState.Unreadable)
                {
                    said.WriteLine($"  {each.Role} is left out: its role is not in the catalogue as a record to set.");
                    continue;
                }

                RoleDoor.Apply(staffing, RoleChange.Set(slot.Role), each.Choices);
                set.Add(each.Role);
            }

            said.WriteLine($"daoris: set {string.Join(", ", set)}. New work takes them; work already running keeps its choices.");
            return 0;
        }
        catch (Exception refused) when (refused is ChoiceWords or DriverException)
        {
            world.Instance.Error.WriteLine($"{DriverChoicesCommand.Prefix}: {refused.Message}");
            return 2;
        }
    }

    private const string PolicyGrammar =
        "`driver role policy <id> [--preset first|even|weighted] [--rule order|weight] [--weight <n>=<0-10>]… [--share <n>=<1-99>]… "
        + "[--budget <n>=<starts>/day]… [--takes <n>=<work,…>]… [--clear]` "
        + "(<n> is a choice's number in the role's staffing)";

    /// <summary>
    /// <c>role policy &lt;id&gt; …</c> (STAFFPOLICY1b, the staff policy design §6.4): the role's policy set through the role door's
    /// <c>SET_ROLE</c>, the draft being the stored policy (or today's order) with the flags laid over a preset; judged before anything is sent.
    /// </summary>
    private ChoicePress PolicyPlan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        if (args.Count < 3) throw new ChoiceWords($"{PolicyGrammar}.");
        var staffing = RoleStaffing.Of(world.ConfigPath) ?? throw new ChoiceWords(Unkept(world));
        var catalogue = RoleCatalog.Read(staffing.Location);
        var id = args[2];
        var role = catalogue.Slots.FirstOrDefault(slot => slot.Id == id && slot.Role is not null)?.Role ?? throw new ChoiceWords($"no role `{id}` is in the catalogue.");
        var existing = staffing.Policy(id);
        var choices = before.Actors.TryGetValue(id, out var staffed) ? staffed : [];
        if (choices.Count == 0) throw new ChoiceWords($"`{id}` has no choices of its own, so it keeps no policy: give it its choices first.");

        var press = StaffPolicyFlags.Read(args.Skip(3).ToList(), choices, existing, PolicyGrammar);
        var (policy, refused) = StaffPolicyDoor.Judge(role, choices, press, existing, kept: true);
        if (refused is not null) throw new ChoiceWords($"`driver role policy`: {refused.Said}");
        var payload = new JsonObject
        {
            ["change"] = "set",
            ["id"] = id,
            ["role"] = new JsonObject
            {
                ["name"] = JsonNode.Parse(role.Name.Json()),
                ["takes"] = new JsonArray([.. role.Takes.Select(work => (JsonNode)JsonValue.Create(work))]),
                ["scope"] = new JsonObject { ["kind"] = role.Scope.Kind, ["id"] = role.Scope.Id },
            },
            ["policy"] = policy is null ? null : press.Preset is { } preset ? preset : JsonNode.Parse(policy.Write()),
            ["policyRevision"] = existing.Revision,
        };
        return new(Word, ChoiceRoutes.Role, payload, _ =>
        {
            RoleDoor.Apply(staffing, RoleChange.Set(role), null, null, press with { Read = existing.Revision });
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            world.Instance.Out.WriteLine(policy is null
                ? $"daoris: role `{id}` goes back to its choices in order, as before."
                : $"daoris: role `{id}` policy set: {StaffPolicyFlags.Said(policy, choices)}.");
            world.Instance.Out.WriteLine("  New work takes it; work already running keeps its choice.");
            return 0;
        });
    }

    private const string RulesGrammar =
        "`role rules --place machine|workspace:<id>|repository:<id> [--roles <id,…>] [--set <count>=<at>:<do>[:<minutes>]]… "
        + "[--off <count>]… [--clear <count>|all]` (counts: answers-alone, failures, failures-on-choice, tries, quiet, limits, moves; "
        + "`role supervisor [--place <place>]` says them)";

    /// <summary>
    /// <c>role rules --place … --set|--off|--clear …</c> (CONTEXTDRIVE1d, the context counters design §7.6): the place's rules as stored, each
    /// flag laid over them in order for the press's roles, judged by the rules' judge before anything is sent, then written whole through the
    /// role door's <c>SET_ROLE</c> at the revision read.
    /// </summary>
    private ChoicePress RulesPlan(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var staffing = RoleStaffing.Of(world.ConfigPath) ?? throw new ChoiceWords(Unkept(world));
        string? place = null;
        IReadOnlyList<string>? roles = null;
        var steps = new List<(string Flag, string Value)>();
        for (var at = 2; at < args.Count; at++)
        {
            string Value() => at + 1 < args.Count ? args[++at] : throw new ChoiceWords($"`{args[at]}` needs its value: {RulesGrammar}.");
            switch (args[at])
            {
                case "--place": place = Value(); break;
                case "--roles": roles = [.. Value().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]; break;
                case "--set" or "--off" or "--clear": steps.Add((args[at], Value())); break;
                default: throw new ChoiceWords($"`{args[at]}` is not a flag of `role rules`: {RulesGrammar}.");
            }
        }

        if (place is null || steps.Count == 0) throw new ChoiceWords($"{RulesGrammar}.");
        var home = staffing.Ledger.Home;
        var stored = new SupervisorRuleStore(home).Of(place);
        var rules = stored.Rules.ToList();
        bool Same(SupervisorRule rule, string count) =>
            rule.Count == count && (rule.Roles is null ? roles is null : roles is not null && rule.Roles.Order().SequenceEqual(roles.Order()));
        foreach (var (flag, value) in steps)
        {
            if (flag == "--clear" && value == "all") { rules.Clear(); continue; }
            var count = flag == "--set" ? value.Split('=', 2)[0] : value;
            rules.RemoveAll(rule => Same(rule, count));
            if (flag == "--off") rules.Add(new SupervisorRule("", count, SupervisorRules.Windows.GetValueOrDefault(count, ""), 1, SupervisorRules.Off, null, place, roles));
            if (flag != "--set") continue;
            // One rule per count for the press's roles: every executor acts on one, so a ladder is never written.
            var step = value.Split('=', 2) is [_, var spec] ? spec.Trim() : throw new ChoiceWords($"`--set {value}` names no `<at>:<do>`: {RulesGrammar}.");
            var parts = step.Split(':');
            if (step.Contains(',', StringComparison.Ordinal) || parts.Length is < 2 or > 3
                || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var threshold))
            {
                throw new ChoiceWords($"`{step}` is not one `<at>:<do>[:<minutes>]`: {RulesGrammar}.");
            }

            int? upTo = parts.Length == 3 && int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var minutes)
                ? minutes
                : parts.Length == 3 ? throw new ChoiceWords($"`{parts[2]}` is not whole minutes: {RulesGrammar}.") : null;
            rules.Add(new SupervisorRule("", count, SupervisorRules.Windows.GetValueOrDefault(count, ""), threshold, parts[1], upTo, place, roles));
        }

        var named = Named(rules);
        var press = new SupervisorRulesPress(place, stored.Revision, named);
        if (SupervisorRuleDoor.Judge(home, press) is { } refused) throw new ChoiceWords($"`role rules`: {refused.Message} ({refused.Code})");
        var payload = new JsonObject
        {
            ["id"] = SupervisorRuleDoor.Id, ["place"] = place, ["rules"] = SupervisorRuleDoor.Wire(named), ["rulesRevision"] = stored.Revision,
        };
        return new(Word, ChoiceRoutes.Role, payload, _ =>
        {
            RoleDoor.Apply(staffing, RoleChange.Rules(press), door: RoleDoor.Doors.Terminal);
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var said = world.Instance.Out;
            said.WriteLine($"daoris: the supervisor's rules for {place} set: {(named.Count == 0 ? "none here, so this place's lines read the wider places' rules" : string.Join("; ", named.Select(SupervisorRuleDoor.Said)))}.");
            said.WriteLine("  Every line here reads them at its next go-on or start; a line waiting now waits until you go on.");
            return 0;
        });
    }

    /// <summary>Each rule given an id unique in its place's set: its count and its place in the list.</summary>
    private static IReadOnlyList<SupervisorRule> Named(IReadOnlyList<SupervisorRule> rules)
    {
        var used = rules.Select(rule => rule.Id).Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);
        var named = new List<SupervisorRule>();
        foreach (var rule in rules)
        {
            if (rule.Id.Length > 0) { named.Add(rule); continue; }
            var n = 1;
            while (!used.Add($"{rule.Count}-{n}")) n++;
            named.Add(rule with { Id = $"{rule.Count}-{n}" });
        }

        return named;
    }

    /// <summary>
    /// <c>role supervisor [--place &lt;place&gt;]</c> (§7.6): the place's rules and each count's effective rules there, where each comes from;
    /// with no place, every place that holds rules.
    /// </summary>
    private static int SupervisorSaid(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var said = world.Instance.Out;
        var place = args.Count > 3 && args[2] == "--place" ? args[3] : args.Count > 2 ? throw new ChoiceWords($"`role supervisor` takes `--place <place>` alone: {RulesGrammar}.") : null;
        var staffing = RoleStaffing.Of(world.ConfigPath) ?? throw new ChoiceWords(Unkept(world));
        var sets = SupervisorRuleBook.Read(staffing.Ledger.Home).Sets;
        foreach (var set in sets.Where(set => place is null || set.Place == place))
        {
            said.WriteLine($"supervisor  {set.Place} · rules revision {set.Revision}");
            foreach (var rule in set.Rules) said.WriteLine($"  {rule.Count,-20}at {rule.At,-3}{rule.Do,-10}{(rule.Roles is { } roles ? string.Join(",", roles) : "every role"),-14}here");
        }

        if (place is not null && place != SupervisorRules.Machine)
        {
            var chain = place.Split(':', 2) is [var kind, var id] ? kind switch
            {
                "repository" => SupervisorRules.Chain(id, null),
                "workspace" => SupervisorRules.Chain(null, id),
                _ => [SupervisorRules.Machine],
            } : [SupervisorRules.Machine];
            foreach (var (count, rules) in SupervisorRules.Effective(sets, chain, null))
            {
                foreach (var rule in rules.Where(rule => rule.Place != place))
                {
                    said.WriteLine($"  {count,-20}at {rule.At,-3}{rule.Do,-10}{"every role",-14}{rule.Place}");
                }
            }
        }

        said.WriteLine($"  Change them: {RulesGrammar}.");
        return 0;
    }

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        if (args.Count > 1 && args[1] == "policy") return PolicyPlan(args, before, world);
        if (args.Count > 1 && args[1] == "rules") return RulesPlan(args, world);
        if (args.Count < 3 || !Changes.Contains(args[1]) || args[1] == "policy") throw new ChoiceWords($"{Grammar}.");
        var staffing = RoleStaffing.Of(world.ConfigPath) ?? throw new ChoiceWords(Unkept(world));
        var catalogue = RoleCatalog.Read(staffing.Location);
        var all = args is [_, "default", "--all"];
        var id = all ? RoleChange.DefaultAll().Id : args[2];
        var (change, choices) = args[1] switch
        {
            "remove" => (RoleChange.Remove(id), (IReadOnlyList<ActorChoice>?)null),
            "default" when all => (RoleChange.DefaultAll(), null),
            "default" => (RoleChange.Default(id), null),
            _ => Set(id, [.. args.Skip(3)], catalogue),
        };
        if (args[1] != "set" && args.Count > 3) throw new ChoiceWords($"`driver role {args[1]}` takes the role's id alone.");
        if (RoleDoor.Judge(catalogue, change, choices) is { } refused) throw new ChoiceWords($"`driver role`: {refused.Said}");

        var payload = all ? new JsonObject { ["change"] = "default-all" } : new JsonObject { ["change"] = args[1], ["id"] = id };
        if (change is RoleChange.SetRole { Role: var role })
        {
            payload["role"] = new JsonObject
            {
                ["name"] = JsonNode.Parse(role.Name.Json()),
                ["takes"] = new JsonArray([.. role.Takes.Select(work => (JsonNode)JsonValue.Create(work))]),
                ["scope"] = new JsonObject { ["kind"] = role.Scope.Kind, ["id"] = role.Scope.Id },
            };
            if (role.Hold is { } hold)
            {
                payload["role"]!["filled"] = RoleRecord.Appointed;
                payload["role"]!["kind"] = hold.Kind;
                payload["role"]!["authority"] = new JsonArray([.. hold.Acts.Select(act => (JsonNode)JsonValue.Create(act))]);
                payload["role"]!["holder"] = hold.Holder;
                payload["role"]!["window"] = hold.Window;
            }

            payload["choices"] = choices is null ? null : new JsonArray([.. choices.Select(choice => (JsonNode)new JsonObject
            {
                ["agent"] = choice.Agent, ["model"] = choice.Model, ["effort"] = choice.Effort,
            })]);
        }

        return new(Word, ChoiceRoutes.Role, payload, _ =>
        {
            RoleDoor.Apply(staffing, change, choices);
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var said = world.Instance.Out;
            said.WriteLine(change switch
            {
                RoleChange.SetRole { Role: var set } => $"daoris: role `{set.Id}` set: {Shape(set)}"
                    + (choices is null ? "." : $"; {(choices.Count == 0 ? "off" : Choices(choices))}."),
                RoleChange.RemoveRole => $"daoris: role `{id}` removed; a work no role takes holds its starts until it is given one. A running session goes on.",
                RoleChange.DefaultAllRoles => "daoris: every seeded role is the seed's again; added roles are kept.",
                _ => $"daoris: role `{id}` is the seed's again.",
            });
            said.WriteLine("  The driver reads the catalogue at each start, so nothing restarts.");
            return 0;
        });
    }

    /// <summary><c>role</c> or <c>role &lt;id&gt;</c>: each role, its state, work, scope and choices; a work with no role said as holding.</summary>
    public override int? Said(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world)
    {
        var said = world.Instance.Out;
        if (args.Count > 1 && args[1] is "set" or "remove" or "default") throw new ChoiceWords($"{Grammar}.");
        if (args.Count > 1 && args[1] == "policy") throw new ChoiceWords($"{PolicyGrammar}.");
        if (args.Count > 1 && args[1] == "rules") throw new ChoiceWords($"{RulesGrammar}.");
        if (args.Count > 1 && args[1] == SupervisorRuleDoor.Id) return SupervisorSaid(args, world);
        var staffing = RoleStaffing.Of(world.ConfigPath);
        var catalogue = staffing is null ? RoleCatalog.Of([]) : RoleCatalog.Read(staffing.Location);
        var rows = RoleDoor.Rows(catalogue, choices).Where(row => args.Count < 2 || row.Id == args[1]).ToList();
        if (args.Count > 1 && rows.Count == 0) throw new ChoiceWords($"no role `{args[1]}` is in the catalogue.");

        var policies = staffing is null ? [] : RolePolicyRow.Of(staffing.Ledger, catalogue, choices);
        said.WriteLine("daoris: the roles that take this machine's work");
        foreach (var row in rows)
        {
            var shape = row.State switch
            {
                "removed" => "removed",
                "unreadable" => $"unreadable: {row.Error}",
                _ => $"{row.Name?.In("en")} · {(row.Hold is { } hold ? Held(hold, row.ScopeKind, row.ScopeId) : Shape(row.Takes, row.ScopeKind, row.ScopeId))}"
                    + (row.Choices is { } staffed ? $" · {(staffed.Count == 0 ? "off" : Choices(staffed))}" : row.Seeded ? " · its staffing's order" : "")
                    + (row.State == "seed" ? " (the seed)" : $" (revision {row.Revision})"),
            };
            said.WriteLine($"  {row.Id,-12}{shape}");
            if (policies.FirstOrDefault(each => each.Role == row.Id) is { } policy && (policy.PolicyState != StaffPolicyRead.Default || args.Count > 1))
            {
                said.WriteLine($"  {"",-12}{PolicySaid(policy, row.Choices)}");
            }
        }

        foreach (var work in RoleWork.Codes.Where(work => catalogue.RoleFor(work, new RolePlace()).Held is { } held && held == RoleCatalog.NoRole(work)))
        {
            said.WriteLine($"  `{work}` has no role on the machine, so its starts hold until one is given it.");
        }

        if (staffing is null) said.WriteLine($"  {Unkept(world)}");
        said.WriteLine($"  Change one: {Grammar}.");
        said.WriteLine($"  Set how a role's choices share new starts: {PolicyGrammar}.");
        said.WriteLine($"  Set the supervisor's rules for a place: {RulesGrammar}.");
        return 0;
    }

    /// <summary>A role's policy line (§6.4): its state and the rule said, why it is stale, and the next new start; the roles with no policy say they run in order.</summary>
    private static string PolicySaid(RolePolicyRow row, IReadOnlyList<ActorChoice>? choices)
    {
        var rule = row.PolicyState switch
        {
            StaffPolicyRead.Default => "policy: none, its choices in order",
            StaffPolicyRead.Stale => $"policy (revision {row.Revision}) is stale: {row.Why}",
            _ => $"policy (revision {row.Revision}): "
                + (row.StaffPolicy is { } node && StaffPolicy.Parse(node.ToJsonString()).Policy is { } policy ? StaffPolicyFlags.Said(policy, choices ?? []) : "set"),
        };
        return rule + (row.Next is { } next ? $" · next new start: choice {next.Choice}, {next.Said}" : "");
    }

    private static (RoleChange, IReadOnlyList<ActorChoice>?) Set(string id, IReadOnlyList<string> rest, RoleCatalog catalogue)
    {
        var current = catalogue.Slots.FirstOrDefault(slot => slot.Id == id)?.Role;
        var name = current?.Name;
        var takes = current?.Takes ?? [];
        var scope = current?.Scope ?? RoleScope.Everywhere;
        var (acts, holder, window, appointed) = (current?.Hold?.Acts, current?.Hold?.Holder, current?.Hold?.Window ?? RoleHold.DefaultWindow, current?.Hold is not null);
        var entries = new List<ActorChoice>();
        var off = false;
        for (var at = 0; at < rest.Count; at++)
        {
            string Value() => at + 1 < rest.Count ? rest[++at] : throw new ChoiceWords($"`{rest[at]}` needs its value: {Grammar}.");
            switch (rest[at])
            {
                case "--name": name = Renamed(name, Value()); break;
                case "--name-in": name = NameIn(name, Value()); break;
                case "--zh": name = NameIn(name, $"zh={Value()}"); break;
                case "--takes": takes = [.. Value().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]; break;
                case "--scope": scope = Scope(Value()); break;
                case "--acts": (acts, appointed) = ([.. Value().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)], true); break;
                case "--holder": (holder, appointed) = (Value() is var held && held == "none" ? null : held, true); break;
                case "--window":
                    var minutes = Value();
                    (window, appointed) = (int.TryParse(minutes, out var set) ? set : throw new ChoiceWords($"`--window {minutes}` is not whole minutes: {Grammar}."), true);
                    break;
                case var flag when flag.StartsWith("--", StringComparison.Ordinal): throw new ChoiceWords($"`{flag}` is not a flag of `driver role set`: {Grammar}.");
                // ONEWAY1a: a role whose work may be off (Intake, Ask Daoris) is set off here, as the retired `driver intake|helper off` did.
                case "off": off = true; break;
                default: entries.Add(Entry(rest[at])); break;
            }
        }

        if (off && entries.Count > 0) throw new ChoiceWords($"`off` is a role's whole staffing, never beside its choices: {Grammar}.");
        if (name is null) throw new ChoiceWords($"a new role names itself (`--name`): {Grammar}.");
        IReadOnlyList<ActorChoice>? choices = off ? [] : entries.Count > 0 ? entries : null;
        // An appointed role keeps its acts in place of work, its kind its own or the one its acts name; the judge refuses a record holding both.
        if (appointed)
        {
            var kind = current?.Hold?.Kind ?? AppointedRoles.KindOf(acts ?? [])
                ?? throw new ChoiceWords($"a new appointed role names the supervisor's acts or the steward's (`--acts`): {Grammar}.");
            return (RoleChange.Set(new RoleRecord(id, name, RoleRecord.Appointed, takes, scope, new RoleHold(kind, acts ?? [], holder, window))), choices);
        }

        if (takes.Count == 0) throw new ChoiceWords($"a new role names the work it takes (`--takes`): {Grammar}.");
        return (RoleChange.Set(new RoleRecord(id, name, RoleRecord.Always, takes, scope)), choices);
    }

    /// <summary><c>--name</c>: the one text as written, which replaces a map's <c>en</c> (its first entry where it has none) and keeps its other languages.</summary>
    private static AuthoredText Renamed(AuthoredText? current, string text)
    {
        if (current is null || current.Tags.Count == 0) return AuthoredText.Of(text);
        var at = current.Tags.FirstOrDefault(tag => tag.Equals("en", StringComparison.OrdinalIgnoreCase)) ?? current.Tags[0];
        return current.With(at, text) ?? AuthoredText.Of(text);
    }

    /// <summary><c>--name-in &lt;tag&gt;="…"</c>: the name in one language, added to the others; a name given as one text so far is kept as <c>en</c>.</summary>
    private static AuthoredText NameIn(AuthoredText? current, string given)
    {
        var split = given.IndexOf('=');
        if (split < 1 || split == given.Length - 1) throw new ChoiceWords($"`--name-in` takes a language tag and its text, `<tag>=\"…\"`: {Grammar}.");
        var tag = given[..split];
        var text = given[(split + 1)..];
        return (current is null ? AuthoredText.FromTags([new(tag, text)]) : current.With(tag, text))
            ?? throw new ChoiceWords($"`{tag}` is not a language tag, or its name is blank: {Grammar}.");
    }

    private static RoleScope Scope(string word)
    {
        if (word == RoleScope.Machine) return RoleScope.Everywhere;
        var parts = word.Split(':', 2);
        return parts.Length == 2 && parts[0] is RoleScope.Workspace or RoleScope.Repository && parts[1].Length > 0
            ? new RoleScope(parts[0], parts[1])
            : throw new ChoiceWords($"`{word}` is not a scope: `machine`, `workspace:<id>` or `repository:<id>`.");
    }

    private static ActorChoice Entry(string quoted)
    {
        var words = quoted.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length is 0 or > 3) throw new ChoiceWords($"`{quoted}` is not a choice: an agent, then its model or `-`, then its effort or `-`, in one quoted word.");
        string? Value(int at) => words.Length > at && words[at] != "-" ? words[at] : null;
        return new ActorChoice(words[0], Value(1), Value(2));
    }

    private static string Shape(RoleRecord role) =>
        $"{role.Name.In("en")} ·{(role.Hold is { } hold ? " " + Held(hold, role.Scope.Kind, role.Scope.Id) : Shape(role.Takes, role.Scope.Kind, role.Scope.Id))}";

    private static string Shape(IReadOnlyList<string> takes, string? kind, string? scope) =>
        $"takes {string.Join(", ", takes)} " + Where(kind, scope);

    /// <summary>An appointed role's line: the acts it holds, where, who holds it, and the supervisor's window.</summary>
    private static string Held(RoleHold hold, string? kind, string? scope) =>
        $"holds {string.Join(", ", hold.Acts)} {Where(kind, scope)} · "
        + (hold.Holder switch { RoleHolders.Terminal => "held by a terminal", RoleHolders.Rules => "held by your rules", _ => "held by no one" })
        + (hold.Kind == AppointedRoles.Supervisor ? $" · answers held {hold.Window} minutes for your overrule" : "");

    private static string Where(string? kind, string? scope) => kind is null or RoleScope.Machine ? "on the machine" : $"in {kind} `{scope}`";

    private static string Choices(IReadOnlyList<ActorChoice> choices) =>
        string.Join(", ", choices.Select((choice, i) => $"{i + 1} {choice.Agent} · {choice.Model ?? "account default"} · {choice.Effort ?? "account default"}"));

    private static string Unkept(DriverChoicesWorld world) =>
        $"{world.ConfigPath}'s staffing is the file's own here (not this home's store), so its roles are not set here.";
}
