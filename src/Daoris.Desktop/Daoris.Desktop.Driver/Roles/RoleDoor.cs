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
    string Id, string State, bool Seeded, AuthoredText? Name, IReadOnlyList<string> Takes, string? ScopeKind, string? ScopeId,
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
/// seeded role's staffing, and <i>Use the default</i> writes its seed (<see cref="ActorChoices.Seed"/>).
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
            slot.Role?.Name, slot.Role?.Takes ?? [], slot.Role?.Scope.Kind ?? slot.Scope?.Kind, slot.Role?.Scope.Id ?? slot.Scope?.Id,
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
    public static long Apply(RoleStaffing staffing, RoleChange change, IReadOnlyList<ActorChoice>? choices = null, long? read = null, RolePolicyPress? policy = null, string door = Doors.Window) =>
        Publish(staffing, [new RolePress(change, choices, read, policy)], door)[0];

    /// <summary>The doors a press comes through, as a place's rules revision keeps it.</summary>
    public static class Doors
    {
        public const string Window = "window";
        public const string Terminal = "terminal";
        public const string Proposal = "proposal";
    }

    /// <summary>
    /// Several changes made as one (AGENTSPLACE1g2, the Agents-place design §4): each judged in order against the roles the ones before it
    /// leave, its staffing by its work, and its revision against the one it was made against, <b>before anything is written</b>; the first
    /// refusal refuses the batch and writes nothing. Then the seeded roles' staffing in one write, the added roles' in one store
    /// transaction, the catalogue's rows in one transaction, and the staffing's check once.
    /// </summary>
    /// <returns>Each id's new revision, in the batch's order.</returns>
    /// <exception cref="RoleRefusedException">The first judge's refusal; nothing written.</exception>
    /// <exception cref="DriverException">The batch is empty or names a role twice, a role moved since it was read, or a write refused; said.</exception>
    public static IReadOnlyList<long> ApplyAll(RoleStaffing staffing, IReadOnlyList<(RoleChange Change, IReadOnlyList<ActorChoice>? Choices, long? Read)> batch) =>
        Publish(staffing, [.. batch.Select(each => new RolePress(each.Change, each.Choices, each.Read))], Doors.Window);

    /// <summary><see cref="ApplyAll(RoleStaffing, IReadOnlyList{ValueTuple{RoleChange, IReadOnlyList{ActorChoice}, Nullable{long}}})"/> for presses that may carry a policy or a place's rules.</summary>
    public static IReadOnlyList<long> ApplyAll(RoleStaffing staffing, IReadOnlyList<RolePress> batch, string door = Doors.Window) => Publish(staffing, batch, door);

    /// <summary>
    /// The one publication (STAFFPOLICY1b, the staff policy design §11 point 2): every change judged in order, its staffing by its work and
    /// its policy by the role's choices, at the revisions read, <b>before anything is written</b>; then the roles' staffing, the catalogue's
    /// rows and the policies in one store transaction, so none of the three lands without the others. A seeded role's staffing is also
    /// projected onto <c>driver.json</c> first, as any write of it is; the staffing's check runs once after.
    /// </summary>
    private static IReadOnlyList<long> Publish(RoleStaffing staffing, IReadOnlyList<RolePress> batch, string door)
    {
        if (batch.Count == 0) throw new DriverException("the press named no role change.");
        static string Key(RoleChange change) => change is RoleChange.SetRules { Press: var rules } ? $"{change.Id} at {rules.Place}" : change.Id;
        if (batch.GroupBy(each => Key(each.Change), StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            throw new DriverException($"the press changes the role `{twice.Key}` more than once, so nothing was written.");
        }

        var catalogue = RoleCatalog.Read(staffing.Location);
        var judged = catalogue;
        var held = staffing.Read();
        var seeded = new List<(string Actor, IReadOnlyList<ActorChoice>? Choices)>();
        var rows = new List<(string Role, IReadOnlyList<ActorChoice>? Choices)>();
        var saves = new List<(string Role, StaffPolicy? Policy, IReadOnlyList<ActorChoice>? Choices, long Read)>();
        var cleared = new List<string>();
        var ruled = new List<SupervisorRulesPress>();
        foreach (var press in batch)
        {
            var (change, choices, read, policy) = (press.Change, press.Choices, press.Read, press.Policy);
            // CONTEXTDRIVE1d: the supervisor's rules, judged by their own judge inside the one transaction below.
            if (change is RoleChange.SetRules { Press: var rules })
            {
                if (choices is not null || policy is not null) throw new ArgumentException("a place's rules carry no staffing and no policy", nameof(batch));
                ruled.Add(rules);
                continue;
            }

            if (read is { } seen && catalogue.RevisionOf(change.Id) != seen)
            {
                throw new DriverException(
                    $"the role `{change.Id}` changed since it was read (revision {seen}, now {catalogue.RevisionOf(change.Id)}), so nothing was written; read it again.");
            }

            if (Judge(judged, change, choices) is { } refused) throw new RoleRefusedException(refused);
            judged = judged.With(change);
            var isSeeded = ActorChoices.Ids.Contains(change.Id, StringComparer.Ordinal);
            if (change is RoleChange.SetRole { Role: var role })
            {
                var staffed = Staffed(catalogue, role.Id, choices);
                if (staffed is not null && isSeeded) seeded.Add((role.Id, staffed));
                else if (staffed is not null) rows.Add((role.Id, staffed));
                if (policy is not null)
                {
                    var effective = staffed ?? (held is not null && held.Actors.TryGetValue(role.Id, out var own) ? own : null);
                    var existing = staffing.Policy(role.Id);
                    var (resolved, refusedPolicy) = StaffPolicyDoor.Judge(role, effective, policy, existing, held is not null);
                    if (refusedPolicy is not null) throw new RoleRefusedException(refusedPolicy);
                    saves.Add((role.Id, resolved, effective, policy.Read ?? existing.Revision));
                }
            }
            else if (choices is not null) throw new ArgumentException("only a role set carries its choices", nameof(batch));
            else
            {
                if (policy is not null) throw new RoleRefusedException(new RoleRefusal(StaffPolicy.Code, $"a policy goes with a role's set: `{change.Id}` is removed or given its seed."));
                if (!isSeeded && change is not RoleChange.DefaultAllRoles) rows.Add((change.Id, null));
                // A seeded role given its seed is given its seed's staffing too (ONEWAY1a, ONEWAY1b): a written order, or its caller's rule.
                if (change is RoleChange.DefaultRole && isSeeded) seeded.Add((change.Id, ActorChoices.Seed(change.Id)));
                if (change is RoleChange.DefaultAllRoles) seeded.AddRange(ActorChoices.Ids.Select(id => (id, ActorChoices.Seed(id))));
                cleared.AddRange(change is RoleChange.DefaultAllRoles ? ActorChoices.Ids : [change.Id]);
            }
        }

        if (rows.Count > 0 && held is null) throw RoleStaffing.NotInStore();
        IReadOnlyList<long> revisions = [];
        var changes = batch.Select(each => each.Change).Where(change => change is not RoleChange.SetRules).ToList();
        var world = ruled.Count > 0 ? SupervisorRuleDoor.World(staffing.Home) : null;
        void Also(DesktopWrite write)
        {
            var written = new Queue<long>(RoleCatalog.WriteIn(write, changes));
            if (changes.Count > 0) write.Moved("roles");
            var rulesWritten = new Queue<long>(ruled.Select(rules => SupervisorRuleStore.WriteIn(write, rules.Place, rules.Read, rules.Rules, door, world!)));
            revisions = [.. batch.Select(each => each.Change is RoleChange.SetRules ? rulesWritten.Dequeue() : written.Dequeue())];
            if (!StaffPolicies.Has(write))
            {
                if (saves.Count > 0) throw new DriverException("this home's store is not yet at the version that keeps policies, so nothing was saved; try again after the next start.");
                return;
            }

            var at = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            var moved = false;
            foreach (var role in cleared) moved |= StaffPolicies.Delete(write, role, at);
            foreach (var (role, policy, choices, read) in saves)
            {
                if (policy is null) StaffPolicies.Delete(write, role, at);
                else StaffPolicies.Save(write, role, policy, choices!, read, at);
                moved = true;
            }

            if (moved) write.Moved(StaffPolicies.Family);
        }

        // The seeded roles' staffing is the file's projection too, so it goes through Set; the rest is one transaction beside it.
        if (seeded.Count > 0) staffing.Set(seeded, write => { RoleStaffing.AddedRows(write, rows); Also(write); });
        else staffing.Publish(rows, Also);
        staffing.Check(StaffingCheckAt.Write);
        return revisions;
    }

    /// <summary>
    /// Whether <paramref name="change"/> with <paramref name="choices"/> may be made to <paramref name="catalogue"/>: the catalogue's judge,
    /// then a set's staffing by the role's work. Every door asks it before anything is sent or shown, and <see cref="Apply"/> again.
    /// </summary>
    public static RoleRefusal? Judge(RoleCatalog catalogue, RoleChange change, IReadOnlyList<ActorChoice>? choices)
    {
        // A place's rules are the rules' judge's, against the store (SupervisorRuleDoor.Judge), never the catalogue's.
        if (change is RoleChange.SetRules) return null;
        if (catalogue.Judge(change) is { } refused) return refused;
        return change is RoleChange.SetRole { Role: var role } && Staffed(catalogue, role.Id, choices) is { } staffed ? RoleCatalog.Judge(role, staffed) : null;
    }

    /// <summary>The staffing a set writes: the choices named, else none for a new added role (off), else what the role has.</summary>
    private static IReadOnlyList<ActorChoice>? Staffed(RoleCatalog catalogue, string id, IReadOnlyList<ActorChoice>? choices) =>
        choices ?? (ActorChoices.Ids.Contains(id, StringComparer.Ordinal) || catalogue.Slots.Any(slot => slot.Id == id && slot.Role is not null) ? null : []);

    /// <summary>
    /// A press's change as <c>SET_ROLE</c>, the verb and a proposal carry it: <c>change</c> (<c>set</c>, <c>remove</c>, <c>default</c> or <c>default-all</c>, which names no id),
    /// <c>id</c>, for a set its <c>role</c> (<c>name.en</c>, <c>name.zh</c>, <c>takes</c>, <c>scope.kind</c>, <c>scope.id</c>) and its
    /// <c>choices</c> (absent or null leaves them, an empty list is off), and the <c>revision</c> it was made against, if any.
    /// </summary>
    /// <exception cref="DriverException">The press does not read as a change; said.</exception>
    public static (RoleChange Change, IReadOnlyList<ActorChoice>? Choices, long? Read) Read(System.Text.Json.JsonElement? payload)
    {
        var press = ReadPress(payload);
        return (press.Change, press.Choices, press.Read);
    }

    /// <summary><see cref="Read"/> with the policy a set carries (<c>policy</c>, a preset's name, a record or null; <c>policyRevision</c>).</summary>
    /// <exception cref="DriverException">The press does not read as a change; said.</exception>
    /// <exception cref="RoleRefusedException">The policy record does not read; said.</exception>
    public static RolePress ReadPress(System.Text.Json.JsonElement? payload)
    {
        if (payload is not { ValueKind: System.Text.Json.JsonValueKind.Object } press) throw new DriverException("the press named no role change.");
        if (SupervisorRuleDoor.Names(press)) return new RolePress(RoleChange.Rules(SupervisorRuleDoor.Read(press)));
        if (Text(press, "change") == "default-all") return new RolePress(RoleChange.DefaultAll());
        var id = Text(press, "id") ?? throw new DriverException("the press named no role.");
        long? read = press.TryGetProperty("revision", out var revision) && revision.TryGetInt64(out var number) ? number : null;
        switch (Text(press, "change"))
        {
            case "remove": return new RolePress(RoleChange.Remove(id), null, read);
            case "default": return new RolePress(RoleChange.Default(id), null, read);
            case "set":
                if (!press.TryGetProperty("role", out var role) || role.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    throw new DriverException($"the press sets `{id}` and carries no role.");
                }

                var name = role.TryGetProperty("name", out var names) ? AuthoredText.Read(names) : null;
                var takes = role.TryGetProperty("takes", out var works) && works.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? works.EnumerateArray().Where(work => work.ValueKind == System.Text.Json.JsonValueKind.String).Select(work => work.GetString()!).ToList()
                    : [];
                var scope = role.TryGetProperty("scope", out var place) && place.ValueKind == System.Text.Json.JsonValueKind.Object
                    ? new RoleScope(Text(place, "kind") ?? RoleScope.Machine, Text(place, "id"))
                    : RoleScope.Everywhere;
                var record = new RoleRecord(id, name ?? AuthoredText.Of(""), RoleRecord.Always, takes, scope);
                return new RolePress(RoleChange.Set(record), ActorChoices.Payload(press), read, RolePolicyPress.FromPayload(press));
            default:
                throw new DriverException("a role's change is `set`, `remove`, `default` or `default-all`.");
        }
    }

    /// <summary><see cref="Read"/> for <c>SET_ROLES</c>: its <c>changes</c>, each a press as <c>SET_ROLE</c> takes it, in order.</summary>
    /// <exception cref="DriverException">The press carries no list of changes, or one of them does not read as a change; said.</exception>
    public static IReadOnlyList<(RoleChange Change, IReadOnlyList<ActorChoice>? Choices, long? Read)> ReadAll(System.Text.Json.JsonElement? payload) =>
        [.. ReadPresses(payload).Select(press => (press.Change, press.Choices, press.Read))];

    /// <summary><see cref="ReadAll"/> with each press's policy.</summary>
    public static IReadOnlyList<RolePress> ReadPresses(System.Text.Json.JsonElement? payload)
    {
        if (payload is not { ValueKind: System.Text.Json.JsonValueKind.Object } press
            || !press.TryGetProperty("changes", out var changes) || changes.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            throw new DriverException("the press named no role changes.");
        }

        return [.. changes.EnumerateArray().Select(each => ReadPress(each))];
    }

    private static string? Text(System.Text.Json.JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text.Trim()
            : null;
}
