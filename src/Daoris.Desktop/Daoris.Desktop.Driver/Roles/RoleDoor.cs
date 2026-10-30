namespace Daoris.Driver;

/// <summary>A change of the catalogue its judge refused at a door: the judge's closed code beside its sentence; nothing was written.</summary>
public sealed class RoleRefusedException(RoleRefusal refusal) : DriverException(refusal.Said)
{
    /// <summary>One of <see cref="RoleCatalog.Codes"/>.</summary>
    public string Code { get; } = refusal.Code;
}

/// <summary>One role as the screen, the terminal and <c>ROLES</c> say it: its state, its record's fields and its staffing as stored.</summary>
/// <param name="State"><c>seed</c>, <c>set</c>, <c>removed</c> or <c>unreadable</c>.</param>
/// <param name="Choices">Its stored staffing; null where none is stored (a seeded role <i>as before</i>, a removed one).</param>
public sealed record RoleRow(
    string Id, string State, bool Seeded, string? NameEn, string? NameZh, IReadOnlyList<string> Takes, string? ScopeKind, string? ScopeId,
    long Revision, string? Error, IReadOnlyList<ActorChoice>? Choices);

/// <summary>
/// The person's one door into the role catalogue (FOUNDATION1y-i, the foundation design §2.7): Settings' <c>SET_ROLE</c>,
/// <c>daoris driver role</c> and Ask Daoris's <c>role_propose</c>, applied by the person's press, each call <see cref="Apply"/>, so each
/// answers the judge's closed codes alike. A role's record and its staffing are set together: the staffing first, a row no record reads
/// until the record lands, then the catalogue's one write, then the staffing's check, which writes the projection the narrowing moved.
/// </summary>
/// <remarks>
/// Removing a role touches no session: a running one goes on with what it opened on, and a continuation reads the role that takes its work
/// now. A removed role's work holds its starts, said (<see cref="RoleCatalog.NoRole"/>), until the person gives it a role; the store keeps a
/// seeded role's staffing, so <i>Back to the default</i> starts it as before.
/// </remarks>
public static class RoleDoor
{
    /// <summary>Every role of <paramref name="catalogue"/> in its order, each with its staffing as <paramref name="config"/> holds it.</summary>
    public static IReadOnlyList<RoleRow> Rows(RoleCatalog catalogue, DriverConfig config) =>
    [
        .. catalogue.Slots.Select(slot => new RoleRow(
            slot.Id,
            slot.State switch { RoleState.Seed => "seed", RoleState.Set => "set", RoleState.Removed => "removed", _ => "unreadable" },
            ActorChoices.Ids.Contains(slot.Id, StringComparer.Ordinal),
            slot.Role?.NameEn, slot.Role?.NameZh, slot.Role?.Takes ?? [], slot.Role?.Scope.Kind ?? slot.Scope?.Kind, slot.Role?.Scope.Id ?? slot.Scope?.Id,
            slot.Revision, slot.Error,
            slot.State == RoleState.Removed ? null : config.Actors.TryGetValue(slot.Id, out var choices) ? choices : null)),
    ];

    /// <summary>
    /// <paramref name="change"/> made by the person: judged by the catalogue's judge and, for a set, its staffing by the role's work; then
    /// written. A new added role naming no <paramref name="choices"/> is off, which a work that may not be off refuses; null keeps the
    /// staffing a role has. <paramref name="read"/> is the revision the change was made against, a proposal's, refused where it moved.
    /// </summary>
    /// <returns>The id's new revision.</returns>
    /// <exception cref="RoleRefusedException">The judge's refusal; nothing written.</exception>
    /// <exception cref="DriverException">The role moved since <paramref name="read"/>, or a write refused; said.</exception>
    public static long Apply(RoleStaffing staffing, RoleChange change, IReadOnlyList<ActorChoice>? choices = null, long? read = null)
    {
        var catalogue = RoleCatalog.Read(staffing.Location);
        if (read is { } seen && catalogue.RevisionOf(change.Id) != seen)
        {
            throw new DriverException(
                $"the role `{change.Id}` changed since it was read (revision {seen}, now {catalogue.RevisionOf(change.Id)}), so nothing was written; read it again.");
        }

        if (Judge(catalogue, change, choices) is { } refused) throw new RoleRefusedException(refused);
        var seeded = ActorChoices.Ids.Contains(change.Id, StringComparer.Ordinal);
        if (change is RoleChange.SetRole { Role: var role })
        {
            var staffed = Staffed(catalogue, role.Id, choices);
            if (staffed is not null && seeded) staffing.Set(role.Id, staffed);
            else if (staffed is not null) staffing.SetAdded(role.Id, staffed);
        }
        else if (choices is not null) throw new ArgumentException("only a role set carries its choices", nameof(choices));

        var revision = RoleCatalog.Write(staffing.Location, change, staffing.Confirmation);
        if (!seeded && change is not RoleChange.SetRole) staffing.SetAdded(change.Id, null);
        staffing.Check(StaffingCheckAt.Write);
        return revision;
    }

    /// <summary>
    /// Whether <paramref name="change"/> with <paramref name="choices"/> may be made to <paramref name="catalogue"/>: the catalogue's judge,
    /// then a set's staffing by the role's work. Every door asks it before anything is sent or shown, and <see cref="Apply"/> again.
    /// </summary>
    public static RoleRefusal? Judge(RoleCatalog catalogue, RoleChange change, IReadOnlyList<ActorChoice>? choices)
    {
        if (catalogue.Judge(change) is { } refused) return refused;
        return change is RoleChange.SetRole { Role: var role } && Staffed(catalogue, role.Id, choices) is { } staffed ? RoleCatalog.Judge(role, staffed) : null;
    }

    /// <summary>The staffing a set writes: the choices named, else none for a new added role (off), else what the role has.</summary>
    private static IReadOnlyList<ActorChoice>? Staffed(RoleCatalog catalogue, string id, IReadOnlyList<ActorChoice>? choices) =>
        choices ?? (ActorChoices.Ids.Contains(id, StringComparer.Ordinal) || catalogue.Slots.Any(slot => slot.Id == id && slot.Role is not null) ? null : []);

    /// <summary>
    /// A press's change as <c>SET_ROLE</c>, the verb and a proposal carry it: <c>change</c> (<c>set</c>, <c>remove</c> or <c>default</c>),
    /// <c>id</c>, for a set its <c>role</c> (<c>name.en</c>, <c>name.zh</c>, <c>takes</c>, <c>scope.kind</c>, <c>scope.id</c>) and its
    /// <c>choices</c> as <c>SET_ACTOR</c> takes them, and the <c>revision</c> it was made against, if any.
    /// </summary>
    /// <exception cref="DriverException">The press does not read as a change; said.</exception>
    public static (RoleChange Change, IReadOnlyList<ActorChoice>? Choices, long? Read) Read(System.Text.Json.JsonElement? payload)
    {
        if (payload is not { ValueKind: System.Text.Json.JsonValueKind.Object } press) throw new DriverException("the press named no role change.");
        var id = Text(press, "id") ?? throw new DriverException("the press named no role.");
        long? read = press.TryGetProperty("revision", out var revision) && revision.TryGetInt64(out var number) ? number : null;
        switch (Text(press, "change"))
        {
            case "remove": return (RoleChange.Remove(id), null, read);
            case "default": return (RoleChange.Default(id), null, read);
            case "set":
                if (!press.TryGetProperty("role", out var role) || role.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    throw new DriverException($"the press sets `{id}` and carries no role.");
                }

                var name = role.TryGetProperty("name", out var names) && names.ValueKind == System.Text.Json.JsonValueKind.Object ? names : default;
                var takes = role.TryGetProperty("takes", out var works) && works.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? works.EnumerateArray().Where(work => work.ValueKind == System.Text.Json.JsonValueKind.String).Select(work => work.GetString()!).ToList()
                    : [];
                var scope = role.TryGetProperty("scope", out var place) && place.ValueKind == System.Text.Json.JsonValueKind.Object
                    ? new RoleScope(Text(place, "kind") ?? RoleScope.Machine, Text(place, "id"))
                    : RoleScope.Everywhere;
                var record = new RoleRecord(
                    id, name.ValueKind == System.Text.Json.JsonValueKind.Object ? Text(name, "en") ?? "" : "",
                    name.ValueKind == System.Text.Json.JsonValueKind.Object ? Text(name, "zh") ?? "" : "", RoleRecord.Always, takes, scope);
                return (RoleChange.Set(record), ActorChoices.Payload(press), read);
            default:
                throw new DriverException("a role's change is `set`, `remove` or `default`.");
        }
    }

    private static string? Text(System.Text.Json.JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text.Trim()
            : null;
}
