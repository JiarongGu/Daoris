using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a build enforces of a policy (STAFFPOLICY1b, the staff policy design §11 point 6): nothing is offered before it is enforced, so the
/// doors refuse a field this list does not hold, and <c>ROLES</c> answers the list for the screen. A field joins it in the row that makes
/// the start, the line or the course read it.
/// </summary>
public static class StaffCapabilities
{
    /// <summary>The fields the start's walk enforces: the rule, a choice's weight, share, daily budget of starts and works.</summary>
    public static IReadOnlyList<string> Fields { get; } = ["rule", "weight", "share", "budget.starts", "takes"];

    /// <summary>The first field of <paramref name="policy"/> this build does not enforce yet, refused with the fields it does; null where it sets none.</summary>
    public static RoleRefusal? Unenforced(string role, StaffPolicy policy)
    {
        var field = policy.Choices.Any(choice => choice.Budget?.Tokens is not null) ? "budget.tokens"
            : policy.Moves.Count > 0 ? "moves"
            : policy.Most is not null ? "most"
            : null;
        return field is null
            ? null
            : new RoleRefusal(StaffPolicy.Code,
                $"`{role}`'s policy sets `{field}`, which this build does not enforce yet, so nothing was saved: it enforces {string.Join(", ", Fields.Select(each => $"`{each}`"))}.");
    }

    /// <summary><see cref="StaffPolicy.Preset"/> as the doors apply it: the preset's moves and bound are left out while no build enforces them.</summary>
    public static StaffPolicy Preset(string preset, IReadOnlyList<ActorChoice> choices, StaffPolicy? draft)
    {
        var filled = StaffPolicy.Preset(preset, choices, draft);
        return filled with { Moves = new Dictionary<string, StaffMove>(), Most = null };
    }
}

/// <summary>
/// A press's policy for one role (STAFFPOLICY1b): a record, a preset's name that fills one for the role's choices, or a clear; with the
/// policy revision the person read, which a save against a moved one refuses.
/// </summary>
/// <param name="Policy">The record the press carries.</param>
/// <param name="Preset">A preset's name, filled for the role's choices at the door.</param>
/// <param name="Clear">The role goes back to today's order: its row deleted, its revisions kept.</param>
/// <param name="Read">The policy revision read; null saves over whatever is stored.</param>
public sealed record RolePolicyPress(StaffPolicy? Policy = null, string? Preset = null, bool Clear = false, long? Read = null)
{
    /// <summary>
    /// The press's <c>policy</c> (a preset's name, a record, or null to clear) and <c>policyRevision</c>; null where it carries neither.
    /// </summary>
    /// <exception cref="RoleRefusedException">The record does not read; said.</exception>
    public static RolePolicyPress? FromPayload(JsonElement press)
    {
        long? read = press.TryGetProperty("policyRevision", out var revision) && revision.TryGetInt64(out var number) ? number : null;
        if (!press.TryGetProperty("policy", out var policy)) return null;
        switch (policy.ValueKind)
        {
            case JsonValueKind.Null: return new RolePolicyPress(Clear: true, Read: read);
            case JsonValueKind.String: return new RolePolicyPress(Preset: policy.GetString(), Read: read);
            case JsonValueKind.Object:
                var (parsed, refused) = StaffPolicy.Parse(policy.GetRawText());
                return parsed is null ? throw new RoleRefusedException(refused!) : new RolePolicyPress(parsed, Read: read);
            default:
                throw new RoleRefusedException(new RoleRefusal(StaffPolicy.Code, "a role's policy is a preset's name, a record, or null to go back to the order."));
        }
    }

    /// <summary>The record this press saves for <paramref name="choices"/>: its own, or its preset filled over <paramref name="existing"/>; null for a clear.</summary>
    public (StaffPolicy? Policy, RoleRefusal? Refused) Resolved(IReadOnlyList<ActorChoice> choices, StaffPolicy? existing)
    {
        if (Clear) return (null, null);
        if (Policy is not null) return (Policy, null);
        if (Preset is { } name && StaffPresets.Names.Contains(name, StringComparer.Ordinal)) return (StaffCapabilities.Preset(name, choices, existing), null);
        return (null, new RoleRefusal(StaffPolicy.Code, $"`{Preset}` is not a preset — one of {string.Join(", ", StaffPresets.Names)}."));
    }
}

/// <summary>One role's change as a door reads it (STAFFPOLICY1b): the change, the staffing it names, the role revision and the policy press.</summary>
public sealed record RolePress(RoleChange Change, IReadOnlyList<ActorChoice>? Choices = null, long? Read = null, RolePolicyPress? Policy = null);

/// <summary>The next new start of a role as its policy would take it (§6.4), a probe: the choice, why, and that said. Nothing is reserved and no account is asked.</summary>
public sealed record RoleNext(string Choice, string By, string Said, string Agent, string? Model, string? Effort);

/// <summary>
/// One role's policy as <c>ROLES</c> and <c>daoris driver role &lt;id&gt;</c> say it (STAFFPOLICY1b): its state (<c>default</c>, <c>set</c> or
/// <c>stale</c>), revision, why it is stale, the record (also while stale, for the editor), and its next new start.
/// </summary>
public sealed record RolePolicyRow(string Role, string PolicyState, long Revision, string? Why, System.Text.Json.Nodes.JsonNode? StaffPolicy, RoleNext? Next)
{
    /// <summary>The rows for each role of <paramref name="catalogue"/> that takes work, read from <paramref name="ledger"/>'s store and probing nothing.</summary>
    public static IReadOnlyList<RolePolicyRow> Of(StaffLedger ledger, RoleCatalog catalogue, DriverConfig config) =>
    [
        .. catalogue.Slots.Where(slot => slot.Role is { Takes.Count: > 0 }).Select(slot =>
        {
            var read = ledger.Policy(slot.Id);
            return new RolePolicyRow(
                slot.Id, read.State, read.Revision, read.Why, read.Policy is { } policy ? System.Text.Json.Nodes.JsonNode.Parse(policy.Write()) : null, NextOf(ledger, slot.Role!, config));
        }),
    ];

    private static RoleNext? NextOf(StaffLedger ledger, RoleRecord role, DriverConfig config)
    {
        if (role.Takes.Contains(RoleWork.Brainstorm, StringComparer.Ordinal)) return null;
        try
        {
            var take = ledger.Preview(role.Id, role.Takes[0], ActorChoices.Effective(role.Id, config));
            return take.Arrangement.Walk.FirstOrDefault() is { } top
                ? new RoleNext(top.Entry.From, top.By, top.Said, top.Entry.Agent, top.Entry.Model, top.Entry.Effort)
                : null;
        }
        catch (Exception error) when (error is ActorUnreadableException or DriverException or Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            return null;
        }
    }
}

/// <summary>The judge of a policy at a door (STAFFPOLICY1b): the role door's, beside the catalogue's, so every door answers a refusal alike.</summary>
public static class StaffPolicyDoor
{
    /// <summary>
    /// Whether <paramref name="press"/> may be saved for <paramref name="role"/> staffed by <paramref name="staffed"/>: the home keeps
    /// policies, the policy resolves and this build enforces it, <see cref="StaffPolicy.Judge"/> takes it, and its revision has not moved
    /// from <paramref name="existing"/>'s. The record to save, or the refusal.
    /// </summary>
    /// <param name="kept">Whether this home's staffing is in the store, where a policy is kept.</param>
    public static (StaffPolicy? Policy, RoleRefusal? Refused) Judge(
        RoleRecord role, IReadOnlyList<ActorChoice>? staffed, RolePolicyPress press, StaffPolicyRead existing, bool kept)
    {
        if (!kept) return (null, Refused("Policies are kept in this machine's store, which this home does not use yet."));
        if (press.Read is { } read && read != existing.Revision)
        {
            return (null, Refused($"`{role.Id}`'s policy changed since you read it (revision {read}, now {existing.Revision}), so nothing was saved: read it again and save your draft over it."));
        }

        if (press.Clear) return (null, null);
        if (staffed is not { Count: > 0 }) return (null, Refused($"`{role.Id}` has no choices of its own, so it keeps no policy: give it its choices first."));
        var (policy, refused) = press.Resolved(staffed, existing.Policy);
        if (refused is not null || policy is null) return (null, refused);
        var judged = StaffCapabilities.Unenforced(role.Id, policy) ?? StaffPolicy.Judge(role, staffed, policy);
        return judged is null ? (policy, null) : (null, judged);
    }

    private static RoleRefusal Refused(string said) => new(StaffPolicy.Code, said);
}
