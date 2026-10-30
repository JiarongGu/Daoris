using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>role</c> proposal (FOUNDATION1y-i, the foundation design §2.7; O2-11): a working role of the role catalogue added or
/// changed (<c>set</c>), removed (<c>remove</c>) or given back its seed (<c>default</c>), by the role's id, its file's own <c>press</c>
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

    public IReadOnlyList<string> Doors { get; } = ["set", "remove", "default"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file) =>
        proposal with { RolePress = file.TryGetProperty("press", out var press) && press.ValueKind == JsonValueKind.Object ? press.Clone() : null };

    /// <summary>ASKSCOPE1c: a role scoped to a workspace or a repository names it.</summary>
    public HelpNames Named(HelpProposal proposal, HelpMachineFacts facts)
    {
        var (kind, id) = proposal.RolePress is { } press && press.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.Object
            ? (Text(scope, "kind"), Text(scope, "id"))
            : (null, null);
        return new(kind == RoleScope.Workspace ? [id] : [null], kind == RoleScope.Repository ? [id] : [null]);
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        static HelpPlan Refused(string why) => new(why, "", "", null);
        if (!Doors.Contains(proposal.Door, StringComparer.Ordinal)) return Refused($"`{proposal.Door}` is not a role's change — `set`, `remove` or `default`.");
        if (proposal.Target?.Trim() is not { Length: > 0 } id) return Refused("a role's change names the role, as the role catalogue lists it.");
        if (facts.Home is not { } home) return Refused("this machine's role catalogue was not read, so nothing is proposed.");

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
        long? read = press is { } given && given.TryGetProperty("revision", out var revision) && revision.TryGetInt64(out var number) ? number : null;
        if (read is { } seen && catalogue.RevisionOf(id) != seen)
        {
            return Refused($"the role `{id}` changed since it was read (revision {seen}, now {catalogue.RevisionOf(id)}); read the catalogue again.");
        }

        var current = catalogue.Slots.FirstOrDefault(slot => slot.Id == id)?.Role;
        IReadOnlyList<ActorChoice>? choices = null;
        RoleChange change;
        switch (proposal.Door)
        {
            case "remove": change = RoleChange.Remove(id); break;
            case "default": change = RoleChange.Default(id); break;
            default:
                var name = press is { } named && named.TryGetProperty("name", out var names) && names.ValueKind == JsonValueKind.Object ? names : default;
                var takes = press is { } work && work.TryGetProperty("takes", out var works) && works.ValueKind == JsonValueKind.Array
                    ? [.. works.EnumerateArray().Select(each => each.GetString()).OfType<string>()]
                    : current?.Takes ?? [];
                var scope = press is { } placed && placed.TryGetProperty("scope", out var place) && place.ValueKind == JsonValueKind.Object
                    ? new RoleScope(Text(place, "kind") ?? RoleScope.Machine, Text(place, "id"))
                    : current?.Scope ?? RoleScope.Everywhere;
                var en = (name.ValueKind == JsonValueKind.Object ? Text(name, "en") : null) ?? current?.NameEn ?? "";
                var zh = (name.ValueKind == JsonValueKind.Object ? Text(name, "zh") : null) ?? current?.NameZh ?? en;
                try
                {
                    choices = ActorChoices.Payload(press);
                }
                catch (DriverException error)
                {
                    return Refused(error.Message);
                }

                change = RoleChange.Set(new RoleRecord(id, en, zh, RoleRecord.Always, takes, scope));
                break;
        }

        if (RoleDoor.Judge(catalogue, change, choices) is { } refused) return Refused(refused.Said);
        return new HelpPlan(null, Describe(change, choices), Terminal(change, choices), null) { Role = new HelpRoleChange(change, choices, read) };
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var role = applying.Plan.Role!;
        var staffing = RoleStaffing.Of(DaorisHome.Entry(applying.Home, HomeEntry.DriverConfig))
            ?? throw new DriverException("this home's actors are the file's own (not this home's store), so its roles are not set here.");
        RoleDoor.Apply(staffing, role.Change, role.Choices, role.Read);
        return Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} (`{applying.Plan.Terminal}`)", null));
    }

    private static string Describe(RoleChange change, IReadOnlyList<ActorChoice>? choices) => change switch
    {
        RoleChange.SetRole { Role: var role } =>
            $"Set the role `{role.Id}` ({role.NameEn} · {role.NameZh}): it takes {string.Join(", ", role.Takes)} {Place(role.Scope)}"
            + (choices is null ? ", its choices kept." : choices.Count == 0 ? ", off." : $", on {Choices(choices)}.")
            + " Running work goes on; new work takes it.",
        RoleChange.RemoveRole => $"Remove the role `{change.Id}`: a work no role then takes waits until one is given it; running work goes on.",
        _ => $"Give `{change.Id}` back its seed: the role as Daoris first had it, its choices kept.",
    };

    private static string Terminal(RoleChange change, IReadOnlyList<ActorChoice>? choices) => change switch
    {
        RoleChange.SetRole { Role: var role } =>
            $"daoris driver role set {role.Id} --name \"{role.NameEn}\" --zh \"{role.NameZh}\" --takes {string.Join(",", role.Takes)} "
            + $"--scope {(role.Scope.Kind == RoleScope.Machine ? RoleScope.Machine : $"{role.Scope.Kind}:{role.Scope.Id}")}"
            + string.Concat((choices ?? []).Select(choice => $" \"{choice.Agent} {choice.Model ?? "-"} {choice.Effort ?? "-"}\"")),
        RoleChange.RemoveRole => $"daoris driver role remove {change.Id}",
        _ => $"daoris driver role default {change.Id}",
    };

    private static string Place(RoleScope scope) => scope.Kind == RoleScope.Machine ? "on this machine" : $"in {scope.Kind} `{scope.Id}`";

    private static string Choices(IReadOnlyList<ActorChoice> choices) =>
        string.Join(", then ", choices.Select(choice => $"{choice.Agent} · {choice.Model ?? "account default"} · {choice.Effort ?? "account default"}"));

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text.Trim() : null;
}

/// <summary>A role change a proposal makes (FOUNDATION1y-i): applied as <c>SET_ROLE</c> applies it, through the role door.</summary>
public sealed record HelpRoleChange(RoleChange Change, IReadOnlyList<ActorChoice>? Choices, long? Read);
