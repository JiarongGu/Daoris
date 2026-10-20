using System.Text.Json.Nodes;

namespace Daoris.Desktop;

/// <summary>
/// <i>What needs you</i>'s accounts (UX6d, D150 §6.2–§6.3; TOOL4m's row), judged in .NET (PUSH1d4, D182 point 6): a start waiting for
/// accounts, and a signed-out account a list or a default holds that no waiting start names, with the states of the Agents place the
/// rows read (<c>accountState</c>, <c>heldBy</c>, <c>runsFor</c>, <c>joinChoices</c> on the page), each over the roster's doors as
/// <c>HARNESSES</c> answers them, each agent's accounts as <c>ACCOUNTS</c> answers them and the look's waits as the tick hands them.
/// </summary>
/// <remarks>
/// <para>🔴 <b>No row starts a process to find out</b> (§6.3, D150 point 5): the waits are the look's own, an account's state the
/// roster's last reading with its time, its cool-off and its scope's list the accounts' files. What no read answered is said so,
/// <i>unknown, never read</i>, and never guessed signed in or out.</para>
///
/// <para><b>What a row carries</b>: the ids of the accounts it names, each with its state, when it was read, its cool-off's end and the
/// name the look read beside it; what the start holds; and the one account outside its list it may let in or read first. An account's
/// name, the row's title and its line of why are the page's words, named from the roster it holds (D182's PUSH1d4 note).</para>
///
/// <para><b>A signed-out account's row has no wait</b> (UXFIX3): only when it was read is known, so it carries its reading and no
/// <c>since</c>. A start whose work no record in hand names waits since its limit, or since nothing known: never the clock's now.</para>
/// </remarks>
internal static class AccountAttention
{
    /// <summary>One start the look held on its accounts: what it holds, whose accounts, and why.</summary>
    private sealed record Held(
        string Key, string Agent, Cooled? Cooling, IReadOnlyList<(string Id, string? Name)> SignedOut, List<string> Quests,
        IReadOnlyList<string> Asks, string? Workspace, bool NoAccount);

    /// <summary>The account whose cool-off ends first, as the look named it; a null account is the tool's own sign-in.</summary>
    private sealed record Cooled(string? Account, string? Name, string Until, bool Stated);

    /// <summary>One agent as the page grouped its doors (<c>byTool</c>): the account-owning door first, one account list.</summary>
    private sealed record Tool(
        string Name, string? Product, IReadOnlyList<JsonObject> Doors, IReadOnlyList<JsonObject> Accounts, string? MachineDefault,
        bool Present, string OwnLogin, string? OwnRead, IReadOnlyList<(string Workspace, string Profile)> WorkspaceDefaults);

    /// <summary>An account's state as last known (D150 §5.3), when it was read, its cool-off, and whether it holds work.</summary>
    private sealed record State(string Name, string? Read, JsonObject? Cooling, bool HoldsWork)
    {
        /// <summary>Whether it waits on the person as a sign-out does (ACCTUX1): signed out, or a key its provider refused.</summary>
        public bool WaitsOnPerson => Name is "out" or "refused";
    }

    /// <summary>The account rows: each start the look held on its accounts, then each signed-out account no such start names.</summary>
    public static IEnumerable<JsonObject> Rows(AttentionFacts facts)
    {
        var tools = ByTool(facts.Doors);
        Tool? ToolOf(string agent) => tools.FirstOrDefault(tool => tool.Name == agent);
        var named = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<JsonObject>();

        foreach (var held in HeldStarts(facts))
        {
            var tool = ToolOf(held.Agent);
            var use = AgentOf(facts.Use, held.Agent);
            var scope = use is not null && held.Workspace is not null ? WorkspaceScope(use, held.Workspace) ?? MachineScope(use) : null;
            var accounts = ServingIds(scope, held).Select(id => NamedAccount(id, tool, use, scope, held)).ToList();
            foreach (var account in accounts) named.Add($"{held.Agent}/{Text(account, "id") ?? ""}");

            var repositories = held.Quests
                .Select(id => Text(facts.Quests.FirstOrDefault(quest => Text(quest, "id") == id), "to")
                    ?? Text(facts.Considered.FirstOrDefault(each => Text(each, "quest") == id), "repository"))
                .Where(repository => !string.IsNullOrEmpty(repository))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var inOne = held.Asks.Count == 0 && repositories.Count == 1;

            var whose = RowFacts(held.Agent, tool);
            whose["named"] = new JsonArray([.. accounts]);
            var (outside, readFirst) = OutsideOf(tool, use, scope, held, accounts);
            whose["outside"] = outside;
            whose["readFirst"] = readFirst;
            if (held.NoAccount) whose["noAccount"] = true;

            rows.Add(new JsonObject
            {
                ["kind"] = "account-wait",
                ["id"] = held.Key,
                ["since"] = HeldSince(held, use, facts),
                ["where"] = inOne ? repositories[0] : held.Workspace ?? "",
                ["wholeWorkspace"] = !inOne && !string.IsNullOrEmpty(held.Workspace),
                ["holding"] = new JsonObject { ["quests"] = Strings(held.Quests), ["asks"] = Strings(held.Asks) },
                ["account"] = whose,
            });
        }

        // A signed-out account a list or a default holds, which no waiting start names; never the tool's own sign-in, which holds no
        // work (D175 point 1, OWNSPACE1b-A).
        foreach (var tool in tools.Where(tool => tool.Present))
        {
            var use = AgentOf(facts.Use, tool.Name);
            var states = AccountStates(tool, use);
            foreach (var account in tool.Accounts)
            {
                var id = Text(account, "name") ?? "";
                var state = states[id];
                if (!state.WaitsOnPerson || !state.HoldsWork || named.Contains($"{tool.Name}/{id}")) continue;
                var whose = RowFacts(tool.Name, tool);
                whose["named"] = new JsonArray(new JsonObject
                {
                    ["id"] = id, ["name"] = null, ["state"] = state.Name == "refused" ? "refused" : "out", ["read"] = state.Read, ["until"] = null,
                });
                whose["outside"] = null;
                rows.Add(new JsonObject
                {
                    ["kind"] = "signed-out",
                    ["id"] = $"signed-out:{tool.Name}/{id}",
                    // When it was read, which its row says as *read …*; since when it holds work nobody knows (UXFIX3).
                    ["since"] = null,
                    ["read"] = state.Read,
                    ["where"] = tool.Product ?? tool.Name,
                    // Where it runs, each a workspace or null for this machine, which the page says.
                    ["runs"] = new JsonArray([.. RunsFor(tool, use, id).Select(workspace => (JsonNode?)workspace)]),
                    ["account"] = whose,
                });
            }
        }

        return rows;
    }

    /// <summary>
    /// The starts held on their accounts: each of the look's waits, then each quest a consideration says waits that no wait holds (an
    /// older shell's look carried no waits). Quests held alike, in one workspace, are one start's wait. A wait with no <c>until</c> is no
    /// cool-off (UX6d1, UX6d2): it names no account, and its key is its own.
    /// </summary>
    private static List<Held> HeldStarts(AttentionFacts facts)
    {
        var held = facts.Waits.Select(wait =>
        {
            var agent = Text(wait, "agent") ?? "";
            var until = Truthy(wait, "until");
            var noAccount = until is null && Flag(wait, "noAccount");
            return new Held(
                until is not null ? $"wait:{agent}/{Text(wait, "account") ?? ""}" : noAccount ? $"wait:{agent}:no-account" : $"wait:{agent}/:signed-out",
                agent,
                until is null ? null : new Cooled(Text(wait, "account"), Text(wait, "name"), until, Flag(wait, "stated")),
                [.. StringsOf(wait["signedOut"]).Select(id => (id, (string?)null))],
                [.. StringsOf(wait["quests"])],
                [.. StringsOf(wait["asks"])],
                Text(wait, "workspace"),
                noAccount);
        }).ToList();

        var inAWait = held.SelectMany(wait => wait.Quests).ToHashSet(StringComparer.Ordinal);
        var grouped = new List<Held>();
        foreach (var consideration in facts.Considered)
        {
            var quest = Text(consideration, "quest") ?? "";
            if (Text(consideration, "verdict") != "Blocked" || inAWait.Contains(quest)) continue;
            var waitsFor = consideration["waitsFor"] as JsonObject;
            var signedOut = consideration["signedOut"] as JsonObject;
            var noAccount = waitsFor is null && Flag(signedOut, "noAccount");
            var accounts = StringsOf(signedOut?["accounts"]);
            if (waitsFor is null && accounts.Count == 0 && !noAccount) continue;

            var agent = Text(waitsFor, "agent") ?? Text(signedOut, "agent") ?? "";
            var record = facts.Quests.FirstOrDefault(each => Text(each, "id") == quest);
            var registered = facts.Registry.FirstOrDefault(row => Text(row, "repository") == Text(consideration, "repository"));
            var workspace = Text(record, "workspace") ?? (registered is null ? null : Text(registered, "workspace") ?? "default");
            var names = signedOut?["names"] as JsonArray;
            var passed = accounts.Select((id, at) => (id, Trimmed(names is not null && at < names.Count ? names[at] : null))).ToList();
            var key = string.Join("/",
                "quest", agent, waitsFor is null ? "" : $"{Text(waitsFor, "account") ?? ""}@{Text(waitsFor, "until")}",
                string.Join(",", passed.Select(each => each.id).Order(StringComparer.Ordinal)), workspace ?? "", noAccount ? "no-account" : "");

            if (grouped.FirstOrDefault(each => each.Key == key) is { } seen)
            {
                seen.Quests.Add(quest);
                continue;
            }

            grouped.Add(new Held(
                key, agent,
                waitsFor is null ? null : new Cooled(Text(waitsFor, "account"), Trimmed(waitsFor["name"]), Text(waitsFor, "until") ?? "", Flag(waitsFor, "stated")),
                passed, [quest], [], workspace, noAccount));
        }

        return [.. held, .. grouped];
    }

    /// <summary>
    /// The accounts that would serve a held start: its scope's list, else its default, less the account kept for conversations, which a
    /// driven start drops (D130 point 6) unless it would leave none; then whatever the look named that the list no longer holds.
    /// </summary>
    private static List<string?> ServingIds(JsonObject? scope, Held held)
    {
        var ids = new List<string?>();
        void Add(string? id)
        {
            if (!ids.Contains(id)) ids.Add(id);
        }

        if (scope is not null)
        {
            var list = StringsOf(scope["list"]);
            var listed = list.Count > 0 ? list : Truthy(scope, "default") is { } chosen ? [chosen] : [];
            var keep = Text(scope["use"] as JsonObject, "keep");
            var kept = listed.Where(id => id != keep).ToList();
            foreach (var id in kept.Count > 0 ? kept : listed) Add(id);
        }

        if (held.Cooling is { } cooled) Add(cooled.Account);
        foreach (var (id, _) in held.SignedOut) Add(id);
        return ids;
    }

    /// <summary>
    /// One account as last known (§5.3): the roster's reading with when, the accounts' files' cool-off. Where the roster has no reading,
    /// the look's word stands: the start's own refusal said it signed out, or its wait said it cools.
    /// </summary>
    private static JsonObject NamedAccount(string? id, Tool? tool, JsonObject? use, JsonObject? scope, Held held)
    {
        var ticked = held.Cooling is { } cooled && cooled.Account == id
            ? new JsonObject { ["until"] = cooled.Until, ["stated"] = cooled.Stated }
            : null;
        var passed = id is not null && held.SignedOut.Any(one => one.Id == id);
        // The driver's own judgement of what holds each account, asked of the walk without probing (TOOL6e).
        var holds = Text(Objects((scope?["next"] as JsonObject)?["others"]).FirstOrDefault(other => Text(other, "account") == id), "hold");
        var tickedName = id is null ? null
            : held.Cooling?.Account == id ? held.Cooling.Name
            : held.SignedOut.FirstOrDefault(one => one.Id == id).Name;

        if (id is null)
        {
            var own = tool is null ? null : OwnState(tool, use);
            var ownCooling = (use?["own"] as JsonObject)?["cooling"] as JsonObject ?? ticked;
            var state = own?.Name == "out" ? "out" : ownCooling is not null ? "cooling" : own?.Name == "in" ? "in" : "unknown";
            return Named(null, null, state, own?.Read, Text(ownCooling, "until"));
        }

        var account = tool?.Accounts.FirstOrDefault(each => Text(each, "name") == id);
        var cooling = Cooling(use, id) ?? ticked;
        var known = account is not null ? StateOf(account, cooling, holdsWork: true).Name : cooling is not null ? "cooling" : "unknown";
        var signedOut = known == "unknown" && (passed || holds is "signedOut" or "refused");
        // A key the walk says its provider refused is refused, whatever the tool last said of it (ACCTUX1, AGT3b).
        var refused = known == "keyed" && holds == "refused";
        return Named(id, tickedName, refused ? "refused" : signedOut ? "out" : known, Text(account, "read"), Text(cooling, "until"));
    }

    private static JsonObject Named(string? id, string? name, string state, string? read, string? until) =>
        new() { ["id"] = id, ["name"] = name, ["state"] = state, ["read"] = read, ["until"] = until };

    /// <summary>
    /// The account outside the list the start reads that the person may let run the work (D130 §3.3): one the join would take, known
    /// ready (read signed in, or a key); else one never read, offered to read first (UXFIX3). None where the wait spans workspaces.
    /// </summary>
    private static (JsonObject? Outside, JsonObject? ReadFirst) OutsideOf(Tool? tool, JsonObject? use, JsonObject? scope, Held held, IReadOnlyList<JsonObject> named)
    {
        if (tool is null || use is null || scope is null || held.Workspace is null) return (null, null);
        var target = Text(scope, "workspace");
        var states = AccountStates(tool, use);
        var candidates = tool.Accounts.Where(account =>
        {
            var name = Text(account, "name") ?? "";
            if (named.Any(one => Text(one, "id") == name)) return false;
            if (!states.TryGetValue(name, out var state) || state.WaitsOnPerson || state.Name == "cooling") return false;
            return JoinChoices(tool, use, name).Contains(target);
        }).ToList();

        var ready = candidates.FirstOrDefault(account => states[Text(account, "name") ?? ""].Name is "in" or "keyed");
        if (ready is not null) return (new JsonObject { ["id"] = Text(ready, "name"), ["list"] = target, ["workspace"] = held.Workspace }, null);
        var unread = candidates.FirstOrDefault(account => states[Text(account, "name") ?? ""] is { Name: "unknown", Read: null or "" });
        return (null, unread is null ? null : new JsonObject { ["id"] = Text(unread, "name") });
    }

    /// <summary>
    /// Since when it has waited: since what it holds was asked for (the oldest quest filed, the oldest ask asked), or since the limit
    /// that held it where that came later; the limit's where nothing it holds is in hand, and null where neither is known.
    /// </summary>
    private static string? HeldSince(Held held, JsonObject? use, AttentionFacts facts)
    {
        var asked = held.Quests.Select(id => Text(facts.Quests.FirstOrDefault(quest => Text(quest, "id") == id), "filed"))
            .Concat(held.Asks.Select(id => Text(facts.Asks.FirstOrDefault(ask => Text(ask, "id") == id), "asked")))
            .Where(at => !string.IsNullOrEmpty(at))
            .Order(StringComparer.Ordinal)
            .ToList();
        var cooled = held.Cooling is { } cooling
            ? Text(cooling.Account is null ? (use?["own"] as JsonObject)?["cooling"] as JsonObject : Cooling(use, cooling.Account), "seen")
            : null;
        var from = asked.FirstOrDefault();
        if (from is null) return string.IsNullOrEmpty(cooled) ? null : cooled;
        return !string.IsNullOrEmpty(cooled) && string.CompareOrdinal(cooled, from) > 0 ? cooled : from;
    }

    /// <summary>Whose a row is: the agent, its product, the door its acts run on, and whether Daoris runs its sign-in there.</summary>
    private static JsonObject RowFacts(string agent, Tool? tool)
    {
        var door = tool?.Doors.FirstOrDefault();
        return new JsonObject
        {
            ["agent"] = agent,
            ["product"] = tool?.Product ?? agent,
            ["harness"] = Text(door, "harness"),
            // Installed, with a door, and that door not saying it runs no sign-in.
            ["signsIn"] = tool is { Present: true } && door is not null
                && !(door["signsIn"] is JsonValue says && says.TryGetValue<bool>(out var signs) && !signs),
        };
    }

    // ---- the Agents place's states, as the page read them (agents.ts, settings/accounts/accounts.ts, agents/tools.ts) ----

    /// <summary>
    /// The roster grouped into the agents a person has (<c>byTool</c>): by the account a door runs as, the account-owning door first;
    /// one account list per agent, by its home; the first door to name a workspace's account speaks for it.
    /// </summary>
    private static List<Tool> ByTool(IReadOnlyList<JsonObject> doors)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);
        foreach (var door in doors)
        {
            var name = Trimmed(door["accountOf"]) ?? Text(door, "harness") ?? "";
            if (!grouped.TryGetValue(name, out var list))
            {
                grouped[name] = list = [];
                order.Add(name);
            }

            list.Add(door);
        }

        return [.. order.Select(name =>
        {
            var own = grouped[name];
            List<JsonObject> inOrder = [.. own.Where(door => Text(door, "harness") == name), .. own.Where(door => Text(door, "harness") != name)];
            var accounts = new List<JsonObject>();
            foreach (var profile in inOrder.SelectMany(door => Objects(door["profiles"])))
            {
                if (accounts.Any(held => Text(held, "home") == Text(profile, "home"))) continue;
                accounts.Add(profile);
            }

            var workspaceDefaults = new List<(string, string)>();
            foreach (var circle in inOrder.SelectMany(door => Objects(door["workspaceDefaults"])))
            {
                var workspace = Text(circle, "workspace") ?? "";
                if (workspaceDefaults.Any(held => held.Item1 == workspace)) continue;
                workspaceDefaults.Add((workspace, Text(circle, "profile") ?? ""));
            }

            static bool Definite(JsonObject door) => Truthy(door, "ownLogin") is { } login && login != "unknown";
            return new Tool(
                name,
                inOrder.Select(door => Truthy(door, "product")).FirstOrDefault(product => product is not null),
                inOrder,
                accounts,
                inOrder.Select(door => Truthy(door, "machineDefault")).FirstOrDefault(chosen => chosen is not null),
                inOrder.Any(door => Flag(door, "present")),
                inOrder.Where(Definite).Select(door => Text(door, "ownLogin")).FirstOrDefault() ?? "unknown",
                Text(inOrder.FirstOrDefault(Definite) ?? inOrder.FirstOrDefault(door => Truthy(door, "ownRead") is not null), "ownRead"),
                workspaceDefaults);
        })];
    }

    /// <summary>An agent's accounts as <c>ACCOUNTS</c> answers them, or null where the answer holds none.</summary>
    private static JsonObject? AgentOf(JsonObject? answer, string agent) =>
        Objects(answer?["agents"]).FirstOrDefault(each => Text(each, "agent") == agent);

    /// <summary>The machine's scope, which every agent answers: its default where none was answered.</summary>
    private static JsonObject MachineScope(JsonObject agent) =>
        Objects(agent["scopes"]).FirstOrDefault(scope => Truthy(scope, "workspace") is null)
        ?? new JsonObject { ["workspace"] = null, ["default"] = null, ["list"] = new JsonArray(), ["use"] = new JsonObject() };

    /// <summary>A workspace's own scope, or null where it names neither a default nor a list of its own.</summary>
    private static JsonObject? WorkspaceScope(JsonObject agent, string workspace) =>
        Objects(agent["scopes"]).FirstOrDefault(scope => Text(scope, "workspace") == workspace);

    private static JsonObject? Cooling(JsonObject? use, string account) =>
        Objects(use?["accounts"]).FirstOrDefault(facts => Text(facts, "name") == account)?["cooling"] as JsonObject;

    /// <summary>
    /// One account's state from what the roster and the accounts' files last said (<c>accountState</c>): signed out first, or for a key
    /// refused; then a cool-off; then a key, which no sign-in names; then the agent's word.
    /// </summary>
    private static State StateOf(JsonObject account, JsonObject? cooling, bool holdsWork)
    {
        var login = Text(account, "login");
        var key = Truthy(account, "key") is not null;
        var name = login == "out" ? (key ? "refused" : "out") : cooling is not null ? "cooling" : key ? "keyed" : login == "in" ? "in" : "unknown";
        return new State(name, Text(account, "read"), cooling, holdsWork);
    }

    /// <summary>The tool's own sign-in's state: its word only at a person's press (TOOL6g), its cool-off where it cools; it holds no work.</summary>
    private static State OwnState(Tool tool, JsonObject? use)
    {
        var login = tool.OwnLogin is "in" or "out" ? tool.OwnLogin : "unknown";
        return StateOf(new JsonObject { ["login"] = login, ["read"] = tool.OwnRead }, (use?["own"] as JsonObject)?["cooling"] as JsonObject, holdsWork: false);
    }

    /// <summary>Whether a default or a list names the account: the machine's or a workspace's (D130 §3.1).</summary>
    private static bool HeldBy(Tool tool, JsonObject? use, string name) =>
        tool.MachineDefault == name
        || tool.WorkspaceDefaults.Any(circle => circle.Profile == name)
        || Objects(use?["scopes"]).Any(scope => Text(scope, "default") == name || StringsOf(scope["list"]).Contains(name));

    /// <summary>Each named account's state on this agent, by its directory.</summary>
    private static Dictionary<string, State> AccountStates(Tool tool, JsonObject? use)
    {
        var states = new Dictionary<string, State>(StringComparer.Ordinal);
        foreach (var account in tool.Accounts)
        {
            var name = Text(account, "name") ?? "";
            states.TryAdd(name, StateOf(account, Cooling(use, name), HeldBy(tool, use, name)));
        }

        return states;
    }

    /// <summary>The workspaces with a default or a list of their own, whose starts take neither this machine's default nor its list.</summary>
    private static HashSet<string> OwnScopes(Tool tool, JsonObject? use) =>
    [
        .. Objects(use?["scopes"]).Select(scope => Truthy(scope, "workspace")).OfType<string>(),
        .. tool.WorkspaceDefaults.Select(circle => circle.Workspace),
    ];

    /// <summary>
    /// Where an account runs (ACCT1's <c>runsFor</c>): each scope whose own list holds it or whose own default names it, this machine (a
    /// null workspace) first. The roster says it where it answers its places; else it is worked out from the same lists and defaults.
    /// </summary>
    private static List<string?> RunsFor(Tool tool, JsonObject? use, string name)
    {
        var account = tool.Accounts.FirstOrDefault(each => Text(each, "name") == name);
        IEnumerable<string?> places;
        if (account?["places"] is JsonArray answered)
        {
            places = answered.OfType<JsonObject>().Select(place => Text(place, "workspace"));
        }
        else
        {
            var found = new List<string?>();
            void Add(string? workspace)
            {
                if (!found.Contains(workspace)) found.Add(workspace);
            }

            var machine = use is null ? null : MachineScope(use);
            if (tool.MachineDefault == name || Text(machine, "default") == name || StringsOf(machine?["list"]).Contains(name)) Add(null);
            foreach (var circle in tool.WorkspaceDefaults.Where(circle => circle.Profile == name)) Add(circle.Workspace);
            foreach (var scope in Objects(use?["scopes"]))
            {
                if (Truthy(scope, "workspace") is { } workspace && (Text(scope, "default") == name || StringsOf(scope["list"]).Contains(name))) Add(workspace);
            }

            places = found;
        }

        return [.. places.OrderBy(place => place is not null).ThenBy(place => place, StringComparer.InvariantCulture)];
    }

    /// <summary>
    /// The lists an account may join (D152 §4.5, <c>joinChoices</c>), by workspace, null for this machine's: each workspace with its own
    /// list or default that does not hold it, then this machine's where it does not hold it.
    /// </summary>
    private static List<string?> JoinChoices(Tool tool, JsonObject use, string account)
    {
        var choices = new List<string?>();
        foreach (var workspace in OwnScopes(tool, use).Order(StringComparer.Ordinal))
        {
            var scope = WorkspaceScope(use, workspace);
            var list = StringsOf(scope?["list"]);
            var named = Text(scope, "default") ?? tool.WorkspaceDefaults.FirstOrDefault(circle => circle.Workspace == workspace).Profile;
            if (list.Contains(account) || (list.Count == 0 && named == account)) continue;
            choices.Add(workspace);
        }

        var machine = MachineScope(use);
        var machineList = StringsOf(machine["list"]);
        var chosen = Text(machine, "default") ?? tool.MachineDefault;
        if (!machineList.Contains(account) && !(machineList.Count == 0 && chosen == account)) choices.Add(null);
        return choices;
    }

    private static IEnumerable<JsonObject> Objects(JsonNode? node) => node is JsonArray items ? items.OfType<JsonObject>() : [];

    private static List<string> StringsOf(JsonNode? node) =>
        node is JsonArray items ? [.. items.Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text : null).OfType<string>()] : [];

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(value => (JsonNode?)value)]);

    private static string? Text(JsonObject? record, string field) =>
        record?[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? Truthy(JsonObject? record, string field) => Text(record, field) is { Length: > 0 } text ? text : null;

    private static string? Trimmed(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    private static bool Flag(JsonObject? record, string field) =>
        record?[field] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}
