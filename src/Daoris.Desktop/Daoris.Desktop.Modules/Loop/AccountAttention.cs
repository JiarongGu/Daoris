using System.Text.Json.Nodes;

namespace Daoris.Desktop;

/// <summary>
/// <i>What needs you</i>'s accounts (UX6d, D150 §6.2–§6.3; TOOL4m's row), judged in .NET (PUSH1d4, D182 point 6): a start waiting for
/// accounts, and a signed-out account of Daoris's that no waiting start names, with the states of the Agents place the rows read
/// (<c>accountState</c> on the page), each over the roster's doors as <c>HARNESSES</c> answers them, each agent's accounts as
/// <c>ACCOUNTS</c> answers them and the look's waits as the tick hands them. Every account is in its agent's one set (POOL1a2, D197;
/// the account-pool design §2.4): each holds work, none runs only somewhere, and none is outside a start's list.
/// </summary>
/// <remarks>
/// <para>🔴 <b>No row starts a process to find out</b> (§6.3, D150 point 5): the waits are the look's own, an account's state the
/// roster's last reading with its time, its cool-off and its scope's list the accounts' files. What no read answered is said so,
/// <i>unknown, never read</i>, and never guessed signed in or out.</para>
///
/// <para><b>What a row carries</b>: the ids of the accounts it names, each with its state, when it was read, its cool-off's end and the
/// name the look read beside it; and what the start holds. Its <c>outside</c> and <c>readFirst</c> are null, kept for the page's
/// shape until POOL1c: no account is outside the set. An account's
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
        string Name, string? Product, IReadOnlyList<JsonObject> Doors, IReadOnlyList<JsonObject> Accounts, bool Present, string OwnLogin,
        string? OwnRead);

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
            // The agent's one set, whatever the start's workspace (POOL1a2): every start reads it. A cool-off of the tool's own sign-in
            // holds a conversation begun there, which the set never serves (D197 point 3), so it names no account of the set.
            var scope = use is not null && held.Cooling is not { Account: null } ? SetScope(use) : null;
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
            // No account is outside the set (POOL1a2), so none is offered to let in or read first.
            whose["outside"] = null;
            whose["readFirst"] = null;
            if (held.NoAccount) whose["noAccount"] = true;

            var row = new JsonObject
            {
                ["kind"] = "account-wait",
                ["id"] = held.Key,
                ["since"] = HeldSince(held, use, facts),
                ["where"] = inOne ? repositories[0] : held.Workspace ?? "",
                ["wholeWorkspace"] = !inOne && !string.IsNullOrEmpty(held.Workspace),
                ["holding"] = new JsonObject { ["quests"] = Strings(held.Quests), ["asks"] = Strings(held.Asks) },
                ["account"] = whose,
            };
            // The starts it holds are their workspace's where they share one, for the window's scope (NEEDSSCOPE1); held across
            // workspaces, or where none is known, it is the machine's account's wait, and every window shows it.
            if (!string.IsNullOrEmpty(held.Workspace)) row["workspace"] = held.Workspace;
            rows.Add(row);
        }

        // A signed-out account of Daoris's, which no waiting start names; never the tool's own sign-in, which holds no work (D175 point
        // 1, OWNSPACE1b-A). No workspace, deliberately (NEEDSSCOPE1): an account of the set serves every workspace (POOL1a2).
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
    /// one account list per agent, by its home.
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

            static bool Definite(JsonObject door) => Truthy(door, "ownLogin") is { } login && login != "unknown";
            return new Tool(
                name,
                inOrder.Select(door => Truthy(door, "product")).FirstOrDefault(product => product is not null),
                inOrder,
                accounts,
                inOrder.Any(door => Flag(door, "present")),
                inOrder.Where(Definite).Select(door => Text(door, "ownLogin")).FirstOrDefault() ?? "unknown",
                Text(inOrder.FirstOrDefault(Definite) ?? inOrder.FirstOrDefault(door => Truthy(door, "ownRead") is not null), "ownRead"));
        })];
    }

    /// <summary>An agent's accounts as <c>ACCOUNTS</c> answers them, or null where the answer holds none.</summary>
    private static JsonObject? AgentOf(JsonObject? answer, string agent) =>
        Objects(answer?["agents"]).FirstOrDefault(each => Text(each, "agent") == agent);

    /// <summary>The agent's one set as <c>ACCOUNTS</c> answers it (POOL1a2): its one scope, or an empty one where none was answered.</summary>
    private static JsonObject SetScope(JsonObject agent) =>
        Objects(agent["scopes"]).FirstOrDefault(scope => Truthy(scope, "workspace") is null)
        ?? new JsonObject { ["workspace"] = null, ["default"] = null, ["list"] = new JsonArray(), ["use"] = new JsonObject() };

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

    /// <summary>Each named account's state on this agent, by its directory.</summary>
    private static Dictionary<string, State> AccountStates(Tool tool, JsonObject? use)
    {
        var states = new Dictionary<string, State>(StringComparer.Ordinal);
        foreach (var account in tool.Accounts)
        {
            var name = Text(account, "name") ?? "";
            // Every account of Daoris's is in the set, so each holds work (POOL1a2).
            states.TryAdd(name, StateOf(account, Cooling(use, name), holdsWork: true));
        }

        return states;
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
