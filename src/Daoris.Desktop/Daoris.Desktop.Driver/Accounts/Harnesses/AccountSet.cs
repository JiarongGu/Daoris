using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// An agent's accounts signed in for Daoris as one set (POOL1a, D197; <c>docs/2026-10-21-account-pool-design.md</c> §2, §6): every
/// account serves every workspace, in one order with one entry of settings. <c>harnesses.json</c> is read and written raw
/// (<see cref="HarnessSettings.Load"/>); <see cref="Of"/> answers the set to every reader, and <see cref="Fold"/>, which
/// <see cref="HarnessSettings.Change"/> runs under the file's lock before every edit, writes it so, with a receipt of what changed.
/// </summary>
/// <remarks>
/// <para>Both are one rule (design §6.1): per agent, the order is the union of every scope (the machine's, then each workspace's by
/// name, each as its first as its starts read it and then its list, then every other account by id), nothing signed in dropped; the
/// settings are the machine's entry where it sets a field, else the one value every workspace entry a start read agrees on, else the
/// default; a machine scope that named one account and no list is used <c>order</c>, so its starts keep beginning there; the
/// defaults and the workspace sections go. A folded file folds to itself.</para>
/// <para>The person's own sign-in is never one of the accounts (design §3): an account is a folder under the home's account homes.</para>
/// </remarks>
public static class AccountSet
{
    /// <summary>How many said receipts an agent keeps beyond its unsaid ones (design §6.3).</summary>
    public const int SaidKept = 5;

    /// <summary>
    /// The set as every reader takes it (design §2.4): <paramref name="settings"/> folded in memory, no receipt appended, so a
    /// start, a look or a door reads one scope, the machine's, whatever the raw file still holds.
    /// </summary>
    /// <param name="accounts">Each agent's accounts on this machine, sorted (<see cref="HarnessSettings.Profiles"/>).</param>
    public static HarnessSettings Of(HarnessSettings settings, IReadOnlyDictionary<string, IReadOnlyList<string>> accounts) =>
        Fold(settings, accounts, DateTimeOffset.UnixEpoch, () => "", receipts: false).Settings;

    /// <summary>
    /// <paramref name="settings"/> folded to one set per agent (design §6.1), each fold that had anything to say appended to
    /// <see cref="HarnessSettings.Pooled"/> as a receipt; and, per agent folded, its counts for the machine log's line.
    /// </summary>
    /// <param name="accounts">Each agent's accounts on this machine, sorted.</param>
    /// <param name="now">When the receipts were written.</param>
    /// <param name="newId">A fresh receipt id, unique among the agent's (design §6.3).</param>
    public static (HarnessSettings Settings, IReadOnlyList<FoldCount> Folded) Fold(
        HarnessSettings settings, IReadOnlyDictionary<string, IReadOnlyList<string>> accounts, DateTimeOffset now, Func<string> newId) =>
        Fold(settings, accounts, now, newId, receipts: true);

    /// <summary>The agents a fold reads: every one the file names, then every one with accounts, by name.</summary>
    public static IReadOnlyList<string> AgentsOf(HarnessSettings settings, IReadOnlyDictionary<string, IReadOnlyList<string>> accounts) =>
        [.. settings.Defaults.Keys
            .Concat(settings.Workspaces.Values.SelectMany(circle => circle.Keys))
            .Concat(settings.Rotation.Keys)
            .Concat(settings.WorkspaceRotation.Values.SelectMany(circle => circle.Keys))
            .Concat(settings.WorkspaceUses.Values.SelectMany(circle => circle.Keys))
            .Concat(accounts.Where(each => each.Value.Count > 0).Select(each => each.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Whether <paramref name="agent"/>'s wiring has anything to fold (design §6.1 rule 1): a default, any workspace entry, no stored
    /// order while it has accounts, or a stored order that leaves out an account or names one that is gone.
    /// </summary>
    public static bool Unfolded(HarnessSettings settings, string agent, IReadOnlyList<string> accounts)
    {
        if (settings.Defaults.ContainsKey(agent)) return true;
        if (settings.Workspaces.Values.Any(circle => circle.ContainsKey(agent))) return true;
        if (settings.WorkspaceRotation.Values.Any(circle => circle.ContainsKey(agent))) return true;
        if (settings.WorkspaceUses.Values.Any(circle => circle.ContainsKey(agent))) return true;
        if (!settings.Rotation.TryGetValue(agent, out var order)) return accounts.Count > 0;
        return order.Count != accounts.Count
            || order.Any(name => !accounts.Contains(name, StringComparer.Ordinal))
            || accounts.Any(name => !order.Contains(name, StringComparer.Ordinal));
    }

    /// <summary>
    /// Each agent's order made whole after an edit (design §2.2, §5.1): the order as edited, then the accounts it did not name in the
    /// order before the edit, then any other by id, a name that is no account left out. An order the edit cleared is the ids' order.
    /// So a new account goes last whatever its id, and the order is written whole at every change of the set.
    /// </summary>
    public static HarnessSettings Whole(
        HarnessSettings before, HarnessSettings edited, IReadOnlyDictionary<string, IReadOnlyList<string>> accounts)
    {
        var orders = new Dictionary<string, IReadOnlyList<string>>(edited.Rotation, StringComparer.OrdinalIgnoreCase);
        foreach (var (agent, mine) in accounts)
        {
            if (mine.Count == 0) continue;
            var named = edited.Rotation.GetValueOrDefault(agent) ?? [];
            IReadOnlyList<string> previous = named.Count == 0 ? [] : before.Rotation.GetValueOrDefault(agent) ?? [];
            List<string> order = [];
            foreach (var name in named.Concat(previous).Concat(mine))
            {
                if (mine.Contains(name, StringComparer.Ordinal) && !order.Contains(name, StringComparer.Ordinal)) order.Add(name);
            }

            orders[agent] = order;
        }

        return edited with { Rotation = orders };
    }

    private static (HarnessSettings Settings, IReadOnlyList<FoldCount> Folded) Fold(
        HarnessSettings settings, IReadOnlyDictionary<string, IReadOnlyList<string>> accounts, DateTimeOffset now, Func<string> newId,
        bool receipts)
    {
        var folded = settings;
        var counts = new List<FoldCount>();
        foreach (var agent in AgentsOf(settings, accounts))
        {
            var mine = accounts.GetValueOrDefault(agent) ?? [];
            if (!Unfolded(settings, agent, mine)) continue;
            var (next, receipt, count) = FoldAgent(folded, agent, mine);
            folded = next;
            counts.Add(count);
            if (receipts && receipt is not null)
            {
                var kept = folded.Pooled.GetValueOrDefault(agent) ?? [];
                var id = newId();
                while (kept.Any(each => each.Id == id)) id = newId();
                folded = folded.WithPooled(agent, [.. kept, receipt with { Id = id, At = now }]);
            }
        }

        return (folded, counts);
    }

    /// <summary>One agent folded (design §6.1 rules 2 to 5): the settings without its scopes, its receipt where anything is said.</summary>
    private static (HarnessSettings Settings, PoolReceipt? Receipt, FoldCount Count) FoldAgent(
        HarnessSettings settings, string agent, IReadOnlyList<string> accounts)
    {
        bool Here(string? name) => name is not null && accounts.Contains(name, StringComparer.Ordinal);

        // The scopes as the raw file names them: the machine's, then each workspace's by name, each with its first as ScopeOf reads it.
        var scopes = new List<FoldedScope>();
        var machineDefault = Named(settings.Defaults.GetValueOrDefault(agent));
        IReadOnlyList<string> machineList = settings.Rotation.GetValueOrDefault(agent) ?? [];
        var storedOrder = settings.Rotation.ContainsKey(agent);
        if (machineDefault is not null || machineList.Count > 0) scopes.Add(Scope(null, machineDefault, machineList));
        foreach (var workspace in settings.Workspaces.Keys.Concat(settings.WorkspaceRotation.Keys)
                     .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            var own = settings.Workspaces.TryGetValue(workspace, out var defaults) ? Named(defaults.GetValueOrDefault(agent)) : null;
            IReadOnlyList<string> list = settings.WorkspaceRotation.TryGetValue(workspace, out var orders)
                ? orders.GetValueOrDefault(agent) ?? [] : [];
            if (own is not null || list.Count > 0) scopes.Add(Scope(workspace, own, list));
        }

        // Rule 2: the order, the union, nothing signed in dropped; a name that is no account here left out and said.
        var order = new List<string>();
        var gone = new List<string>();
        void Place(string? name)
        {
            if (name is null) return;
            if (!Here(name))
            {
                if (!gone.Contains(name, StringComparer.Ordinal)) gone.Add(name);
                return;
            }

            if (!order.Contains(name, StringComparer.Ordinal)) order.Add(name);
        }

        foreach (var scope in scopes)
        {
            Place(scope.Begins);
            foreach (var name in scope.List) Place(name);
            // A default its own list does not hold was never where its starts began: placed as every other account is, unless gone.
            if (scope.Default is { } named && !Here(named)) Place(named);
        }

        var unlisted = accounts.Where(name => !order.Contains(name, StringComparer.Ordinal)).ToList();
        order.AddRange(unlisted);

        // Which firsts moved: each scope's first against the order's; a machine scope that named nobody by its walk's first.
        var moved = new List<MovedFirst>();
        foreach (var scope in scopes)
        {
            if (Here(scope.Begins) && scope.Begins != order[0]) moved.Add(new MovedFirst(scope.Workspace, scope.Begins!, order.IndexOf(scope.Begins!) + 1));
        }

        if (scopes.All(scope => scope.Workspace is not null) && scopes.Count > 0 && accounts.Count > 0 && accounts[0] != order[0])
        {
            moved.Add(new MovedFirst(null, accounts[0], order.IndexOf(accounts[0]) + 1));
        }

        // Rule 5: each account a stored order left out, where the file held one, said as the fold places it.
        List<PlacedAccount> joined = storedOrder
            ? [.. accounts.Where(name => !machineList.Contains(name, StringComparer.Ordinal))
                .Select(name => new PlacedAccount(name, order.IndexOf(name) + 1))]
            : [];

        // Rule 3: the settings, field by field, over each entry as RotationUse.Read normalizes it.
        var notKept = new List<NotKeptSetting>();
        var machineEntry = settings.Uses.GetValueOrDefault(agent)?.Folded() ?? UseEntry.Empty;
        var taking = new List<(string Workspace, UseEntry Entry)>();
        foreach (var (workspace, uses) in settings.WorkspaceUses.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            if (uses.GetValueOrDefault(agent) is not { } own) continue;
            var folded = own.Folded();
            var read = settings.WorkspaceRotation.TryGetValue(workspace, out var orders) && orders.GetValueOrDefault(agent) is { Count: > 0 };
            if (read) taking.Add((workspace, folded));
            else
            {
                // A dormant workspace entry: no start read it, so every field it set is said, none taken.
                foreach (var (name, value) in Shown(folded)) notKept.Add(new NotKeptSetting(workspace, name, value));
            }
        }

        var entry = machineEntry;
        foreach (var field in RotationUse.Fields)
        {
            if (machineEntry[field] is not null) continue;
            var values = taking.Where(each => each.Entry[field] is not null).Select(each => Normal(field, each.Entry[field]!.Value)).ToList();
            if (values.Count > 0 && values.All(value => value == values[0]))
            {
                entry = entry.With(field, taking.First(each => each.Entry[field] is not null).Entry[field]);
            }
        }

        foreach (var (workspace, taken) in taking)
        {
            foreach (var (name, value) in Shown(taken))
            {
                var set = entry[name];
                if (set is null || !RotationUse.Fields.Contains(name, StringComparer.Ordinal) || Normal(name, set.Value) != Normal(name, value))
                {
                    notKept.Add(new NotKeptSetting(workspace, name, value));
                }
            }
        }

        // A machine scope that named one account and no list is used One by one, in order, so its starts keep beginning there.
        string? inOrder = null;
        if (Here(machineDefault) && machineList.Count == 0)
        {
            inOrder = machineDefault;
            if (machineEntry["use"] is { } use && RotationUse.ModeOf(use) != "order") notKept.Add(new NotKeptSetting(null, "use", use));
            entry = entry.With("use", UseEntry.Element(writer => writer.WriteStringValue("order")));
        }

        // A keep naming no account of the set, or left alone in a set of one, is dropped and said, whichever entry set it.
        if (entry["keep"] is { } keep && (RotationUse.KeepOf(keep) is not { } kept || !Here(kept) || order.Count < 2))
        {
            var from = machineEntry["keep"] is not null ? null : taking.First(each => each.Entry["keep"] is not null).Workspace;
            if (!notKept.Any(each => each.Workspace == from && each.Setting == "keep")) notKept.Add(new NotKeptSetting(from, "keep", keep));
            entry = entry.With("keep", null);
        }

        // Rule 4: what goes. Rule 5: the order whole and the settings, in the machine's slots an older build reads.
        var next = settings.WithoutScopes(agent) with
        {
            Rotation = Placed(settings.Rotation, agent, order),
            Uses = Placed(settings.Uses, agent, entry),
        };

        var narrowed = scopes.Where(scope => !accounts.All(name => scope.Accounts.Contains(name, StringComparer.Ordinal))).ToList();
        var said = narrowed.Count > 0 || moved.Count > 0 || joined.Count > 0 || notKept.Count > 0 || gone.Count > 0 || inOrder is not null;
        var receipt = said
            ? new PoolReceipt("", DateTimeOffset.UnixEpoch, scopes, order, moved, joined, inOrder, notKept, gone, Said: false)
            : null;
        return (next, receipt, new FoldCount(agent, order.Count, scopes.Count, notKept.Count));
    }

    private static FoldedScope Scope(string? workspace, string? own, IReadOnlyList<string> list) =>
        new(workspace, own, list, list.Count == 0 ? own : own is not null && list.Contains(own, StringComparer.Ordinal) ? own : list[0]);

    private static string? Named(string? raw) => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    /// <summary>Each field an entry sets that a fold may say: the settings this build knows, then any it has no name for; retired ones never.</summary>
    private static IEnumerable<(string Name, JsonElement Value)> Shown(UseEntry entry) =>
        entry.Fields.Where(field => !RotationUse.Retired.Contains(field.Key, StringComparer.Ordinal))
            .Select(field => (field.Key, field.Value));

    /// <summary>A setting's value as a start reads it, for comparing two entries (design §6.1 rule 3): a value this build knows normalized.</summary>
    private static string Normal(string field, JsonElement raw) => field switch
    {
        "use" when RotationUse.ModeOf(raw) is { } mode => mode,
        "keep" when RotationUse.KeepOf(raw) is { } keep => keep,
        "switch" when RotationUse.SwitchOf(raw) is { } switched => switched,
        "curve" when UseCurve.Read(raw) is { } curve => JsonFile.Serialize(curve.WriteTo),
        _ => raw.GetRawText(),
    };

    private static Dictionary<string, T> Placed<T>(IReadOnlyDictionary<string, T> map, string agent, T value)
        where T : class
    {
        var next = new Dictionary<string, T>(map, StringComparer.OrdinalIgnoreCase);
        var empty = value is IReadOnlyCollection<string> { Count: 0 } || value is UseEntry { IsEmpty: true };
        if (empty) next.Remove(agent);
        else next[agent] = value;
        return next;
    }
}

/// <summary>One scope a fold read, as the raw file named it (design §6.3).</summary>
/// <param name="Workspace">The workspace whose scope it was, or null for the machine's.</param>
/// <param name="Default">Its default, or none.</param>
/// <param name="List">Its list, empty for none.</param>
/// <param name="Begins">Its first as its starts read it: its default where its list holds it, else its list's first; with no list its default.</param>
public sealed record FoldedScope(string? Workspace, string? Default, IReadOnlyList<string> List, string? Begins)
{
    /// <summary>The accounts its starts could run on: its list, or its default alone.</summary>
    public IReadOnlyList<string> Accounts => List.Count > 0 ? List : Default is { } only ? [only] : [];
}

/// <summary>A scope's first that is no longer the order's, and its place in the order now, from one.</summary>
public sealed record MovedFirst(string? Workspace, string Account, int Place);

/// <summary>An account a stored order left out, and the place the fold gave it, from one.</summary>
public sealed record PlacedAccount(string Account, int Place);

/// <summary>A setting a fold did not keep: whose it was (a workspace, or null for the machine's entry), its name and its value as written.</summary>
public sealed record NotKeptSetting(string? Workspace, string Setting, JsonElement Value);

/// <summary>A fold's counts for the machine log's <c>accounts.pooled</c> line (design §6.2): never a name.</summary>
public sealed record FoldCount(string Agent, int Accounts, int Scopes, int NotKept);

/// <summary>
/// What one fold changed for an agent, kept in <c>harnesses.json</c>'s <c>pooled</c> until the person presses <i>Got it</i>
/// (design §6.3): appended, never replaced unsaid, marked said by its id.
/// </summary>
/// <param name="Id">A random id, written once and unique among the agent's receipts.</param>
/// <param name="At">When it was written.</param>
/// <param name="Scopes">Each scope folded, as it was.</param>
/// <param name="Order">The order written.</param>
/// <param name="Moved">Each first moved, with its new place.</param>
/// <param name="Joined">Each account the stored order left out, with its place.</param>
/// <param name="InOrder">The account the machine scope named alone, where the fold set <c>use: order</c> for it.</param>
/// <param name="NotKept">Each setting not kept.</param>
/// <param name="Gone">Each name that was no account here, left out.</param>
/// <param name="Said">Whether the person has pressed <i>Got it</i> on it.</param>
public sealed record PoolReceipt(
    string Id, DateTimeOffset At, IReadOnlyList<FoldedScope> Scopes, IReadOnlyList<string> Order, IReadOnlyList<MovedFirst> Moved,
    IReadOnlyList<PlacedAccount> Joined, string? InOrder, IReadOnlyList<NotKeptSetting> NotKept, IReadOnlyList<string> Gone, bool Said);
