using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris role set|remove|default|policy|rules</c> as the card shows it (<c>SET_ROLE</c>): a working role set, removed or given back its
/// seed, every seeded role at once, a role's policy, a supervisor set, or a place's supervisor rules written whole. Judged by the role
/// door's judges against the catalogue, staffing and rules this machine holds now, as the route judges them, so a refusal is theirs, back to
/// the terminal with no card. A supervisor's record and a place's rules ride the view before and after, for the page to word.
/// </summary>
public sealed class RoleChangeView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_ROLE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        if (machine.Facts.Home is not { } home) return PressViewed.Refused("this machine's role catalogue was not read, so nothing is asked.");
        try
        {
            var press = RoleDoor.ReadPress(payload);
            return press.Change is RoleChange.SetRules { Press: var rules } ? Rules(home, rules) : Role(home, machine.Config, press);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }
    }

    private static PressViewed Rules(string home, SupervisorRulesPress rules)
    {
        if (SupervisorRuleDoor.Judge(home, rules) is { } refused) return PressViewed.Refused($"{refused.Message} ({refused.Code})");
        var before = new SupervisorRuleStore(home).Of(rules.Place).Rules;
        return PressViewed.Shown(new PressView("role-rules", Facts(("place", rules.Place)), SupervisorRulesFlags.Terminal(rules))
        {
            Rules = PressRules.Of(rules.Place, before, rules.Rules),
        });
    }

    private static PressViewed Role(string home, DriverConfig config, RolePress press)
    {
        var (change, choices, read, policy) = (press.Change, press.Choices, press.Read, press.Policy);
        var catalogue = RoleCatalog.ReadHome(home);
        if (read is { } seen && catalogue.RevisionOf(change.Id) != seen) return PressViewed.Refused(RoleCatalog.Moved(change.Id, seen, catalogue.RevisionOf(change.Id)).Message);
        if (RoleDoor.Judge(catalogue, change, choices) is { } refused) return PressViewed.Refused(refused.Said);

        var staffing = new RoleStaffing(home);
        var current = catalogue.Slots.FirstOrDefault(slot => slot.Id == change.Id)?.Role;
        if (change is RoleChange.SetRole { Role: { Hold: { } hold } appointed })
        {
            if (hold.Kind == AppointedRoles.Supervisor && !SupervisorConversion.Converted(home)) return PressViewed.Refused(SupervisorConversion.NotConvertedSaid);
            if (policy is not null && AppointedStaffing.JudgePolicy(appointed, policy.Policy) is { } unordered) return PressViewed.Refused(unordered.Said);
        }

        if (policy is not null && change is RoleChange.SetRole { Role: var set })
        {
            var effective = choices ?? (config.Actors.TryGetValue(set.Id, out var own) ? own : null);
            var (resolved, notTaken) = StaffPolicyDoor.Judge(set, effective, policy, staffing.Policy(set.Id), staffing.InUse());
            if (notTaken is not null) return PressViewed.Refused(notTaken.Said);
            if (choices is null && set.Hold is null)
            {
                return PressViewed.Shown(new PressView(resolved is null ? "role-policy-clear" : "role-policy-set", Facts(("role", set.Id)),
                    $"daoris role policy {set.Id} " + (resolved is null ? "--clear" : StaffPolicyFlags.Terminal(resolved, effective ?? []))));
            }
        }

        var also = new List<PressSaid>();
        var view = change switch
        {
            RoleChange.SetRole { Role: { Hold: { Kind: AppointedRoles.Supervisor } } supervisor } => new PressView(
                "role-supervisor-set", Facts(("role", supervisor.Id), ("name", Names(supervisor.Name))), SetCommand(supervisor, choices))
            {
                Supervisor = new PressSupervisor(supervisor.Id, supervisor.Name.Texts, new PressScope(supervisor.Scope.Kind, supervisor.Scope.Id),
                    PressHold.Of(supervisor.Hold!), current?.Hold is { Kind: AppointedRoles.Supervisor } before ? PressHold.Of(before) : null),
            },
            RoleChange.SetRole { Role: var role } => new PressView(
                role.Hold is null ? "role-set" : "role-appointed-set",
                Facts(("role", role.Id), ("name", Names(role.Name)), ("takes", string.Join(", ", role.Takes)), ("scope", Scope(role.Scope))),
                SetCommand(role, choices)),
            RoleChange.RemoveRole => new PressView("role-remove", Facts(("role", change.Id)), $"daoris role remove {change.Id}"),
            RoleChange.DefaultAllRoles => new PressView("role-default-all", Facts(), "daoris role default --all"),
            _ => new PressView("role-default", Facts(("role", change.Id)), $"daoris role default {change.Id}"),
        };
        if (change is RoleChange.SetRole && choices is { Count: 0 }) also.Add(new PressSaid("role-choices-off"));
        else if (change is RoleChange.SetRole && choices is { Count: > 0 }) also.Add(new PressSaid("role-choices", ("choices", Choices(choices))));
        if (policy is null) also.AddRange(PolicyMoved(change, choices, staffing));
        if (change is RoleChange.SetRole { Role: var reader })
        {
            var order = choices ?? (staffing.Read()?.Actors.TryGetValue(reader.Id, out var staffed) == true ? staffed : null);
            also.AddRange(SecondCheckOnSaid.Of(reader, order, policy?.Policy ?? staffing.Policy(reader.Id).Policy));
        }

        return PressViewed.Shown(also.Count > 0 ? view with { Also = also } : view);
    }

    /// <summary>
    /// What a change does to a standing policy it does not name: a default, a remove or a set emptying the choices deletes it, and a set
    /// naming other choices re-keys it.
    /// </summary>
    private static IEnumerable<PressSaid> PolicyMoved(RoleChange change, IReadOnlyList<ActorChoice>? choices, RoleStaffing staffing)
    {
        IEnumerable<string> roles = change switch
        {
            RoleChange.DefaultAllRoles => ActorChoices.Ids,
            RoleChange.SetRole => choices is null ? [] : [change.Id],
            _ => [change.Id],
        };
        foreach (var role in roles)
        {
            var existing = staffing.Policy(role);
            if (existing.State == StaffPolicyRead.Default) continue;
            if (change is not RoleChange.SetRole || choices is not { Count: > 0 }) yield return new PressSaid("role-policy-goes", ("role", role));
            else if (existing is { State: StaffPolicyRead.Set, Policy: { } standing } && standing.Rekeyed(choices) is not null) yield return new PressSaid("role-policy-rekeyed", ("role", role));
        }
    }

    private static string SetCommand(RoleRecord role, IReadOnlyList<ActorChoice>? choices)
    {
        var said = new StringBuilder($"daoris role set {role.Id} {NameFlags(role.Name)}");
        if (role.Hold is { } hold)
        {
            said.Append($" --scope {Scope(role.Scope)}")
                .Append(hold.Kind == AppointedRoles.Steward ? "" : $" --decides {(hold.Generic ? "every" : string.Join(",", hold.Decides))}")
                .Append($" --acts {(hold.Acts.Count == 0 ? "\"\"" : string.Join(",", hold.Acts))} --holder {hold.Holder ?? (hold.Kind == AppointedRoles.Steward ? "none" : RoleHolders.Rules)} --window {hold.Window}");
        }
        else said.Append($" --takes {string.Join(",", role.Takes)} --scope {Scope(role.Scope)}");

        foreach (var choice in choices ?? []) said.Append($" \"{choice.Agent} {choice.Model ?? "-"} {choice.Effort ?? "-"}\"");
        if (choices is { Count: 0 }) said.Append(" off");
        return said.ToString();
    }

    /// <summary>A name as the terminal takes it: <c>--name</c> the text as written (a map's <c>en</c>), <c>--name-in</c> for each other language.</summary>
    private static string NameFlags(AuthoredText name)
    {
        if (name.Tags.Count == 0) return $"--name {ShellWord.Of(name.AsWritten, ShellWord.Name)}";
        var main = name.Find("en");
        return string.Join(" ", (main is null ? [] : new[] { $"--name {ShellWord.Of(main, ShellWord.Name)}" })
            .Concat(name.Tags.Where(tag => !tag.Equals("en", StringComparison.OrdinalIgnoreCase))
                .Select(tag => $"--name-in {tag}={ShellWord.Of(name.Find(tag) ?? "", ShellWord.Name)}")));
    }

    private static string Names(AuthoredText name) => string.Join(" · ", name.Texts);

    private static string Scope(Daoris.Driver.RoleScope scope) => scope.Kind == Daoris.Driver.RoleScope.Machine ? Daoris.Driver.RoleScope.Machine : $"{scope.Kind}:{scope.Id}";

    private static string Choices(IReadOnlyList<ActorChoice> choices) =>
        string.Join(", ", choices.Select(choice => $"{choice.Agent} · {choice.Model ?? "-"} · {choice.Effort ?? "-"}"));

    private static Dictionary<string, string> Facts(params (string Name, string Value)[] facts) =>
        facts.ToDictionary(fact => fact.Name, fact => fact.Value, StringComparer.Ordinal);
}
