using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>role</c> proposal (FOUNDATION1y-i, the foundation design §2.7; O2-11): a working role of the role catalogue added or
/// changed (<c>set</c>), removed (<c>remove</c>) or given back its seed (<c>default</c>), or every seeded role at once (<c>default-all</c>,
/// DEFAULTCONFIG1b's <i>Restore all role defaults</i>, naming no one role), by the role's id, its file's own <c>press</c>
/// carrying the record's fields, the choices and the revision Ask Daoris read. It is judged by the role door's judge, the one
/// <c>SET_ROLE</c> and <c>daoris driver role</c> answer, and applied only by the person's press, through that door, in both modes: no
/// <c>settings_*</c> grant reaches a role (D205 point 9). It extends what MODELROLE1h's <c>actor_propose</c> would be: a <c>set</c> of a
/// seeded role naming its choices sets that actor's order.
/// </summary>
/// <remarks>A <c>set</c> leaving a field out keeps the role's own, so a proposal of a seeded role's choices names its choices alone.</remarks>
internal sealed class HelpRoleProposals : IHelpProposalKind
{
    public string Kind => "role";

    public string Tool => "role_propose";

    public IReadOnlyList<string> Doors { get; } = ["set", "remove", "default", "default-all"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file) =>
        proposal with { RolePress = file.TryGetProperty("press", out var press) && press.ValueKind == JsonValueKind.Object ? press.Clone() : null };

    /// <summary>
    /// ASKSCOPE1c: a role scoped to a workspace or a repository names it, the scope the press gives and the one the role stands in now
    /// (REVIEW2 S3); <c>default-all</c> reaches every workspace.
    /// </summary>
    public HelpNames Named(HelpProposal proposal, HelpMachineFacts facts)
    {
        if (proposal.Door == "default-all") return new([null], [null]) { Every = true };
        var scopes = new List<(string? Kind, string? Id)>();
        // CONTEXTDRIVE1d: the supervisor's rules name the place they are for.
        if (proposal.Target?.Trim() == SupervisorRuleDoor.Id && proposal.RolePress is { } rules && Text(rules, "place")?.Split(':', 2) is [var kind, var id])
        {
            scopes.Add((kind, id));
        }

        if (proposal.RolePress is { } press && press.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.Object)
        {
            scopes.Add((Text(scope, "kind"), Text(scope, "id")));
        }

        if (facts.Home is { } home && proposal.Target?.Trim() is { Length: > 0 } target)
        {
            try
            {
                if (RoleCatalog.ReadHome(home).Slots.FirstOrDefault(slot => slot.Id == target)?.Role?.Scope is { } now) scopes.Add((now.Kind, now.Id));
            }
            catch (DriverException)
            {
                // Plan refuses a catalogue that does not read.
            }
        }

        return new(
            [.. scopes.Select(each => each.Kind == RoleScope.Workspace ? each.Id : null)],
            [.. scopes.Select(each => each.Kind == RoleScope.Repository ? each.Id : null)]);
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        static HelpPlan Refused(string why) => new(why, "", "", null);
        if (!Doors.Contains(proposal.Door, StringComparer.Ordinal)) return Refused($"`{proposal.Door}` is not a role's change — `set`, `remove`, `default` or `default-all`.");
        var all = proposal.Door == "default-all";
        if ((all ? RoleChange.DefaultAll().Id : proposal.Target?.Trim()) is not { Length: > 0 } id) return Refused("a role's change names the role, as the role catalogue lists it.");
        if (facts.Home is not { } home) return Refused("this machine's role catalogue was not read, so nothing is proposed.");
        if (id == SupervisorRuleDoor.Id) return RulesPlan(proposal, home);

        RoleCatalog catalogue;
        try
        {
            catalogue = RoleCatalog.ReadHome(home);
        }
        catch (DriverException error)
        {
            return Refused(error.Message);
        }

        var press = proposal.RolePress;
        long? read = !all && press is { } given && given.TryGetProperty("revision", out var revision) && revision.TryGetInt64(out var number) ? number : null;
        if (read is { } seen && catalogue.RevisionOf(id) != seen)
        {
            return Refused($"the role `{id}` changed since it was read (revision {seen}, now {catalogue.RevisionOf(id)}); read the catalogue again.");
        }

        var current = catalogue.Slots.FirstOrDefault(slot => slot.Id == id)?.Role;
        IReadOnlyList<ActorChoice>? choices = null;
        RolePolicyPress? policy = null;
        RoleChange change;
        switch (proposal.Door)
        {
            case "remove": change = RoleChange.Remove(id); break;
            case "default": change = RoleChange.Default(id); break;
            case "default-all": change = RoleChange.DefaultAll(); break;
            default:
                var name = press is { } named && named.TryGetProperty("name", out var names) ? NameOf(names) : null;
                var takes = press is { } work && work.TryGetProperty("takes", out var works) && works.ValueKind == JsonValueKind.Array
                    ? [.. works.EnumerateArray().Select(each => each.GetString()).OfType<string>()]
                    : current?.Takes ?? [];
                var scope = press is { } placed && placed.TryGetProperty("scope", out var place) && place.ValueKind == JsonValueKind.Object
                    ? new RoleScope(Text(place, "kind") ?? RoleScope.Machine, Text(place, "id"))
                    : current?.Scope ?? RoleScope.Everywhere;
                try
                {
                    choices = ActorChoices.Payload(press);
                    policy = press is { } carried ? RolePolicyPress.FromPayload(carried) : null;
                }
                catch (Exception error) when (error is DriverException or RoleRefusedException)
                {
                    return Refused(error.Message);
                }

                change = RoleChange.Set(new RoleRecord(id, name ?? current?.Name ?? AuthoredText.Of(""), RoleRecord.Always, takes, scope));
                break;
        }

        if (RoleDoor.Judge(catalogue, change, choices) is { } refused) return Refused(refused.Said);
        if (policy is not null && change is RoleChange.SetRole { Role: var set })
        {
            var staffing = new RoleStaffing(home);
            var effective = choices ?? (config.Actors.TryGetValue(id, out var own) ? own : null);
            var existing = staffing.Policy(id);
            var (resolved, notTaken) = StaffPolicyDoor.Judge(set, effective, policy, existing, staffing.InUse());
            if (notTaken is not null) return Refused(notTaken.Said);
            var reached = new SupervisorRuleStore(home).Reached();
            if (reached && RuledPolicy.On(set, policy, resolved).Refused is { } ruled) return Refused(ruled.Said);
            // On a reached home a preset is carried as its name, so the door writes its moves as rules beside the policy.
            policy = reached && policy.Preset is not null ? policy : policy with { Policy = resolved, Preset = null, Clear = resolved is null };
            var before = (existing.Applied, config.Actors.TryGetValue(id, out var held) ? held : []);
            return new HelpPlan(null, Describe(change, choices) + PolicyDescribe(set.Name.Texts.FirstOrDefault() ?? set.Id, before, existing, resolved, effective), Terminal(change, choices) + PolicyTerminal(id, resolved, effective), null)
            {
                Role = new HelpRoleChange(change, choices, read, policy),
            };
        }

        return new HelpPlan(null, Describe(change, choices) + Unsaid(change, choices, catalogue, new RoleStaffing(home), config), Terminal(change, choices), null)
        {
            Role = new HelpRoleChange(change, choices, read),
        };
    }

    /// <summary>
    /// CONTEXTDRIVE1d (the context counters design §7.6): a <c>set</c> of the supervisor carries a place's rules and the revision read, judged by
    /// the rules' judge against the store now; the card says the place's rules before and after, and the terminal's twin.
    /// </summary>
    private static HelpPlan RulesPlan(HelpProposal proposal, string home)
    {
        static HelpPlan Refused(string why) => new(why, "", "", null);
        if (proposal.Door != "set") return Refused($"the supervisor's rules are a `set` of `{SupervisorRuleDoor.Id}`: its place, its rules written whole and the revision read.");
        if (proposal.RolePress is not { } given) return Refused("the supervisor's rules name their place, the rules and the revision read.");
        SupervisorRulesPress press;
        try
        {
            press = SupervisorRuleDoor.Read(given);
        }
        catch (Exception error) when (error is RuleRefusedException or RoleRefusedException)
        {
            return Refused(error.Message);
        }

        if (SupervisorRuleDoor.Judge(home, press) is { } refused) return Refused($"{refused.Message} ({refused.Code})");
        var before = new SupervisorRuleStore(home).Of(press.Place).Rules;
        static string Rules(IReadOnlyList<SupervisorRule> rules) => rules.Count == 0 ? "none here" : string.Join("; ", rules.Select(SupervisorRuleDoor.Said));
        var describe = $"Supervisor, {press.Place}: {Rules(before)} → {Rules(press.Rules)}. Lines here read it at their next go-on or start.";
        return new HelpPlan(null, describe, RulesTerminal(press), null) { Role = new HelpRoleChange(RoleChange.Rules(press), null, null) };
    }

    /// <summary>The terminal's twin of a place's rules: the place cleared and each count's ladder set, one command per set of roles.</summary>
    private static string RulesTerminal(SupervisorRulesPress press)
    {
        var groups = press.Rules.GroupBy(rule => rule.Roles is { } roles ? string.Join(",", roles) : "").ToList();
        if (groups.Count == 0) return $"daoris role rules --place {press.Place} --clear all";
        return string.Join(" && ", groups.Select((group, index) =>
            $"daoris role rules --place {press.Place}" + (group.Key.Length > 0 ? $" --roles {group.Key}" : "") + (index == 0 ? " --clear all" : "")
            + string.Concat(group.GroupBy(rule => rule.Count).Select(count => count.All(rule => rule.Do == SupervisorRules.Off)
                ? $" --off {count.Key}"
                : $" --set {count.Key}=" + string.Join(",", count.Select(rule => $"{rule.At}:{rule.Do}" + (rule.UpTo is { } upTo ? $":{upTo}" : "")))))));
    }

    /// <summary>
    /// What a change does to a standing policy it does not name (REVIEW2 S2): a default, a remove or a set emptying the choices deletes it, and a
    /// set naming other choices re-keys it. Said on the card, so the revision the person presses holds it.
    /// </summary>
    private static string Unsaid(RoleChange change, IReadOnlyList<ActorChoice>? choices, RoleCatalog catalogue, RoleStaffing staffing, DriverConfig config)
    {
        IEnumerable<string> roles = change switch
        {
            RoleChange.DefaultAllRoles => ActorChoices.Ids,
            RoleChange.SetRole => choices is null ? [] : [change.Id],
            _ => [change.Id],
        };
        var said = new StringBuilder();
        foreach (var role in roles)
        {
            var existing = staffing.Policy(role);
            if (existing.State == StaffPolicyRead.Default) continue;
            var held = config.Actors.TryGetValue(role, out var own) ? own : [];
            var name = catalogue.Slots.FirstOrDefault(slot => slot.Id == role)?.Role?.Name.Texts.FirstOrDefault() ?? role;
            var rekeyed = change is RoleChange.SetRole && choices is { Count: > 0 } && existing is { State: StaffPolicyRead.Set, Policy: not null } ? existing.Policy.Rekeyed(choices) : null;
            if (change is RoleChange.SetRole && rekeyed is null && choices is { Count: > 0 }) continue;
            said.Append(PolicyDescribe(name, (existing.Applied, held), existing, rekeyed, choices));
        }

        return said.ToString();
    }

    /// <summary>
    /// The card's before and after in the fold's words (STAFFPOLICY1g): <i>Build: First that can run → Spread by weight (3:1); on a limit: next
    /// by weight. This can spend differently.</i> A policy set for other choices ran in order, and the card says so.
    /// </summary>
    private static string PolicyDescribe(
        string name, (StaffPolicy? Policy, IReadOnlyList<ActorChoice> Choices) before, StaffPolicyRead existing, StaffPolicy? policy, IReadOnlyList<ActorChoice>? choices)
    {
        var was = existing.State == StaffPolicyRead.Stale
            ? "First that can run (its policy was set for other choices, so it ran in order)"
            : StaffPolicyFlags.Fold(before.Policy, before.Choices);
        return policy is null
            ? $" Policy, {name}: {was} → First that can run (its policy is cleared)."
            : $" Policy, {name}: {was} → {StaffPolicyFlags.Fold(policy, choices ?? [])}. This can spend differently.";
    }

    private static string PolicyTerminal(string id, StaffPolicy? policy, IReadOnlyList<ActorChoice>? choices) =>
        $" && daoris driver role policy {id} " + (policy is null ? "--clear" : StaffPolicyFlags.Terminal(policy, choices ?? []));

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var role = applying.Plan.Role!;
        var staffing = RoleStaffing.Of(DaorisHome.Entry(applying.Home, HomeEntry.DriverConfig))
            ?? throw new DriverException("this home's actors are the file's own (not this home's store), so its roles are not set here.");
        RoleDoor.Apply(staffing, role.Change, role.Choices, role.Read, role.Policy, RoleDoor.Doors.Proposal);
        return Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} (`{applying.Plan.Terminal}`)", null));
    }

    private static string Describe(RoleChange change, IReadOnlyList<ActorChoice>? choices) => change switch
    {
        RoleChange.SetRole { Role: var role } =>
            $"Set the role `{role.Id}` ({string.Join(" · ", role.Name.Texts)}): it takes {string.Join(", ", role.Takes)} {Place(role.Scope)}"
            + (choices is null ? ", its choices kept." : choices.Count == 0 ? ", off." : $", on {Choices(choices)}.")
            + " Running work goes on; new work takes it.",
        RoleChange.RemoveRole => $"Remove the role `{change.Id}`: a work no role then takes waits until one is given it; running work goes on.",
        RoleChange.DefaultAllRoles => "Give every seeded role back its seed: the roles as Daoris first had them, their choices kept; added roles are kept.",
        _ => $"Give `{change.Id}` back its seed: the role as Daoris first had it, its choices kept.",
    };

    private static string Terminal(RoleChange change, IReadOnlyList<ActorChoice>? choices) => change switch
    {
        RoleChange.SetRole { Role: var role } =>
            $"daoris driver role set {role.Id} {NameFlags(role.Name)} --takes {string.Join(",", role.Takes)} "
            + $"--scope {(role.Scope.Kind == RoleScope.Machine ? RoleScope.Machine : $"{role.Scope.Kind}:{role.Scope.Id}")}"
            + string.Concat((choices ?? []).Select(choice => $" \"{choice.Agent} {choice.Model ?? "-"} {choice.Effort ?? "-"}\"")),
        RoleChange.RemoveRole => $"daoris driver role remove {change.Id}",
        RoleChange.DefaultAllRoles => "daoris driver role default --all",
        _ => $"daoris driver role default {change.Id}",
    };

    /// <summary>The name a press carries: authored text, where a proposal written before (<c>{ "en": null, "zh": "…" }</c>) leaves a language out as null.</summary>
    private static AuthoredText? NameOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? AuthoredText.FromTags(element.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.String).Select(each => new KeyValuePair<string, string>(each.Name, each.Value.GetString()!)))
            : AuthoredText.Read(element);

    /// <summary>A name as the terminal takes it: <c>--name</c> the text as written (a map's <c>en</c>), <c>--name-in</c> for each other language.</summary>
    private static string NameFlags(AuthoredText name)
    {
        if (name.Tags.Count == 0) return $"--name \"{name.AsWritten}\"";
        var main = name.Find("en");
        return string.Join(" ", (main is null ? [] : new[] { $"--name \"{main}\"" })
            .Concat(name.Tags.Where(tag => !tag.Equals("en", StringComparison.OrdinalIgnoreCase)).Select(tag => $"--name-in {tag}=\"{name.Find(tag)}\"")));
    }

    private static string Place(RoleScope scope) => scope.Kind == RoleScope.Machine ? "on this machine" : $"in {scope.Kind} `{scope.Id}`";

    private static string Choices(IReadOnlyList<ActorChoice> choices) =>
        string.Join(", then ", choices.Select(choice => $"{choice.Agent} · {choice.Model ?? "account default"} · {choice.Effort ?? "account default"}"));

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text.Trim() : null;
}

/// <summary>A role change a proposal makes (FOUNDATION1y-i): applied as <c>SET_ROLE</c> applies it, through the role door.</summary>
public sealed record HelpRoleChange(RoleChange Change, IReadOnlyList<ActorChoice>? Choices, long? Read, RolePolicyPress? Policy = null);
