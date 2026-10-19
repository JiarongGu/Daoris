using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What one start of Ask Daoris handed its agent (HELPWIRED1, D183 point 4, the Ask Daoris modes design §6): recorded at the start
/// from the spawn's own inputs, never worked out again from configuration, since a list worked out later could disagree with
/// what the agent was given (D183's rejected list). One per start: a conversation going on, or going on under another mode, adds
/// one, and the earlier are kept.
/// </summary>
/// <remarks>
/// <para><b>Fields by allow-list.</b> Each field is one the receipt names; nothing of the spawn's environment, a server's command,
/// arguments or environment, or an account's key is ever copied (design §7, the receipt leaking). Machine paths are kept (its
/// folder, the checkouts it may read, the rules), so it is this desktop's alone: answered by the bridge's <c>HELP_WIRED</c>, never
/// by a shared host or on a feed.</para>
/// </remarks>
public sealed record HelpReceipt
{
    /// <summary>When the start handed it.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary><see cref="HelpWired.New"/> for a conversation's first start, <see cref="HelpWired.GoingOn"/> for one going on.</summary>
    public string Start { get; init; } = HelpWired.New;

    /// <summary>The access mode it ran under, the effective one (<see cref="HelpModes"/>; design §1), never the mode chosen now.</summary>
    public string Mode { get; init; } = HelpModes.Restricted;

    /// <summary>Its agent: the harness Daoris names it by.</summary>
    public string Harness { get; init; } = "";

    /// <summary>The harness version observed for the start, or null where none was.</summary>
    public string? Version { get; init; }

    /// <summary>The account it ran as, by its name here; null for the agent's own sign-in.</summary>
    public string? Account { get; init; }

    /// <summary>Its door: <see cref="HelpWired.Pipe"/> (the native door) or <see cref="HelpWired.Acp"/> (the protocol door).</summary>
    public string Door { get; init; } = HelpWired.Pipe;

    /// <summary>The protocol door's mode it asked (D81, D183), its required mark taken off; null on the native door.</summary>
    public string? Posture { get; init; }

    /// <summary>The native door's permission mode Daoris named (full access's); null where the adapter's own was kept.</summary>
    public string? PermissionMode { get; init; }

    /// <summary>Its working folder: its room.</summary>
    public string Folder { get; init; } = "";

    /// <summary>
    /// Daoris's own tools its allow-list allows, as the rules spell them (<c>mcp__daoris-knowledge__…</c>); none in full access, whose
    /// harness mode asks nothing, nor where no rules were handed.
    /// </summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>The plugins' skills composed for it, each as <c>plugin:skill</c> with whether it was handed.</summary>
    public IReadOnlyList<HelpWiredSkill> Skills { get; init; } = [];

    /// <summary>Each server: handed, handed by full access without an approval, or withheld and why.</summary>
    public IReadOnlyList<HelpWiredServer> Servers { get; init; } = [];

    /// <summary>The permission rules handed, list for list as the settings file holds them; null where none was handed.</summary>
    public HelpWiredRules? Rules { get; init; }

    /// <summary>Whether the tree guard (PERM3) was handed with its rules: restricted's, never full access's (D183's HELPMODE1a1 note).</summary>
    public bool Guarded { get; init; }

    /// <summary>The checkouts reading across lets it read (D107), as its rules grant them; none where no rules were handed.</summary>
    public IReadOnlyList<HelpRead> Checkouts { get; init; } = [];

    /// <summary>In restricted mode, what it lacks, each with the door that grants it (D183 point 4); none in full access.</summary>
    public IReadOnlyList<HelpWiredLack> Lacks { get; init; } = [];
}

/// <summary>A plugin's skill composed for the start (HELPPLUG1b): <c>plugin:skill</c>, its <see cref="HelpWired"/> state and why.</summary>
public sealed record HelpWiredSkill(string Name, string State, string? Why = null);

/// <summary>
/// A server: its name, the plugin that declares it (null for Daoris's own connector), its <see cref="HelpWired"/> state, its tools
/// where known, and why where it was withheld.
/// </summary>
public sealed record HelpWiredServer(string Name, string? Plugin, string State, IReadOnlyList<string>? Tools = null, string? Why = null);

/// <summary>The rules a start handed, as the settings file holds them (PERM1).</summary>
public sealed record HelpWiredRules(IReadOnlyList<string> Allow, IReadOnlyList<string> Ask, IReadOnlyList<string> Deny);

/// <summary>
/// What restricted mode lacks: its kind (<c>server</c>, <c>shell</c>, <c>edits</c>), its name, the door that grants it, why, and the
/// plugin whose page holds the door where it is one.
/// </summary>
public sealed record HelpWiredLack(string Kind, string Name, string Door, string Why, string? Plugin = null);

/// <summary>
/// What a start of Ask Daoris composed and put on its spawn, as <c>ChatRunner</c> holds it at the moment before the process starts
/// (HELPWIRED1): what <see cref="HelpWired.Compose"/> makes a receipt of. Each "handed" is read from the spawn itself, whether its
/// command line or its protocol door's <c>_meta</c> names the file, so a door that takes no rules, no skills or no servers is said so.
/// </summary>
public sealed record HelpHanded(string Mode, bool GoesOn, string Harness, SessionWire Wire, string Folder)
{
    public string? Version { get; init; }

    public string? Account { get; init; }

    public string? Posture { get; init; }

    public string? PermissionMode { get; init; }

    /// <summary>The rules composed for the settings file; null where the harness takes none.</summary>
    public RuleLists? Rules { get; init; }

    /// <summary>Whether the spawn was handed the settings file.</summary>
    public bool RulesHanded { get; init; }

    /// <summary>Whether the settings file carried the tree guard.</summary>
    public bool Guarded { get; init; }

    /// <summary>The checkouts the place's rules let it read (D107).</summary>
    public IReadOnlyList<HelpRead> Reads { get; init; } = [];

    /// <summary>The plugin folders composed for its skills (<see cref="SpawnSkills"/>).</summary>
    public IReadOnlyList<string> SkillPlugins { get; init; } = [];

    /// <summary>Whether the spawn was handed them.</summary>
    public bool SkillsHanded { get; init; }

    /// <summary>The servers put on the spawn: the knowledge connector where one was found, and the plugins' servers handed.</summary>
    public IReadOnlyList<AcpMcpServer> Servers { get; init; } = [];

    /// <summary>Whether the spawn was handed them: the protocol door always is; the native door where it takes the servers file.</summary>
    public bool ServersHanded { get; init; }

    /// <summary>The note that said a browser server was withheld (D78), or null.</summary>
    public Noted? Withheld { get; init; }
}

/// <summary>
/// Ask Daoris's receipts (HELPWIRED1), kept per conversation in <c>&lt;home&gt;/sessions/&lt;id&gt;.wired.json</c> beside its
/// other files, oldest first, and answered newest first; a delete or a history clear takes the file with the rest
/// (<see cref="SessionHomeFiles"/>, under Ask Daoris's own name). A conversation started before this row has none: not recorded.
/// </summary>
/// <remarks>
/// <para><b>A file that does not read is nothing kept</b>, as Ask Daoris's other file is (<see cref="HelpConversations"/>): it reads
/// as not recorded, and the next start writes it again. A receipt is read back field by field, so what the file holds that a
/// receipt has no place for is never answered.</para>
/// </remarks>
public sealed class HelpWired(string home)
{
    public const string Suffix = ".wired.json";

    /// <summary>A conversation's first start.</summary>
    public const string New = "new";

    /// <summary>A start of a conversation going on (MSG1c, ASKHIST1).</summary>
    public const string GoingOn = "going-on";

    /// <summary>The native door.</summary>
    public const string Pipe = "pipe";

    /// <summary>The protocol door.</summary>
    public const string Acp = "acp";

    /// <summary>On the spawn: approved for Ask Daoris, or Daoris's own.</summary>
    public const string Handed = "handed";

    /// <summary>On the spawn by full access, with no restricted approval (D183 point 1).</summary>
    public const string FullAccess = "full-access";

    /// <summary>Not on the spawn; why says which.</summary>
    public const string Withheld = "withheld";

    /// <summary>
    /// Handed by a door whose loading of it was read from the adapter's sources and never run (D170's HELPPLUG1b note): a skill on
    /// the protocol door.
    /// </summary>
    public const string Unverified = "unverified";

    /// <summary>A server declared for Ask Daoris the person has not approved.</summary>
    public const string NotApproved = "not-approved";

    /// <summary>A server whose approval its plugin's update has changed since (HELPPLUG1c).</summary>
    public const string Changed = "changed";

    /// <summary>A server naming a generic execution tool, never handed in restricted mode (HELP4).</summary>
    public const string Refused = "refused";

    /// <summary>A server declared for sessions alone, not for Ask Daoris.</summary>
    public const string NotForHelp = "not-for-help";

    /// <summary>The harness has no door for it: the native door's adapter took no servers file, or no skills or rules.</summary>
    public const string NoDoor = "no-door";

    /// <summary>Daoris's knowledge connector was not found on this machine.</summary>
    public const string NotFound = "not-found";

    /// <summary>What restricted mode does not do: run commands, edit files (D89, D107).</summary>
    public const string RestrictedMode = "restricted";

    /// <summary>A server's browser could not be brought up for a reason the shell said in its own words.</summary>
    public const string BrowserFailed = "browser.failed";

    /// <summary>The door that grants it: the plugin's approval on its page (D170).</summary>
    public const string DoorPlugins = "plugins";

    /// <summary>The door that grants it: full access, the person's choice (D183 point 2), until HELPMODE1c's grants.</summary>
    public const string DoorMode = "mode";

    /// <summary>The door that grants it: the host, the desktop's browser or Daoris's own install.</summary>
    public const string DoorHost = "host";

    /// <summary>The door that grants it: another agent, whose harness takes what this one does not.</summary>
    public const string DoorHarness = "harness";

    // One writer at a time in this process: two starts of one conversation would otherwise each drop the other's receipt.
    private static readonly object Gate = new();

    /// <summary>Where a conversation's receipts are kept, beside its transcript and Ask Daoris's name and pin for it.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, Checked(session) + Suffix);

    /// <summary>Add a start's receipt after those kept.</summary>
    /// <exception cref="DriverException">An id that names no session.</exception>
    public void Keep(string session, HelpReceipt receipt)
    {
        lock (Gate)
        {
            var kept = Oldest(session);
            JsonFile.Write(PathOf(session), writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("receipts");
                foreach (var each in kept.Append(receipt)) Write(writer, each);
                writer.WriteEndArray();
                writer.WriteEndObject();
            });
        }
    }

    /// <summary>A conversation's receipts, the newest first; none for a conversation from before, an id no session's, or a file that does not read.</summary>
    public IReadOnlyList<HelpReceipt> Read(string session) => SessionEventText.IsId(session) ? [.. Oldest(session).Reverse()] : [];

    /// <summary>
    /// What <c>HELP_WIRED</c> answers for a conversation (HELPWIRED1): <c>{ session, recorded, receipts }</c>, the newest first, each
    /// field written as the file keeps it, a null kept as a null (the bridge would leave a null field out of an object).
    /// </summary>
    public static JsonElement Answer(string session, IReadOnlyList<HelpReceipt> receipts)
    {
        using var document = JsonDocument.Parse(JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("session", session);
            writer.WriteBoolean("recorded", receipts.Count > 0);
            writer.WriteStartArray("receipts");
            foreach (var receipt in receipts) Write(writer, receipt);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// The receipt of what a start handed (design §6): the servers each with its approval as the person's grants stand at this start,
    /// the skills by the plugin folders composed, and in restricted mode what it lacks with each door.
    /// </summary>
    /// <param name="catalog">The plugins as this start read them.</param>
    public static HelpReceipt Compose(HelpHanded handed, PluginCatalog catalog, HelpGrantSet grants, DateTimeOffset at)
    {
        var full = handed.Mode == HelpModes.Full;
        var rules = handed.RulesHanded && handed.Rules is { } lists ? new HelpWiredRules(lists.Allow, lists.Ask, lists.Deny) : null;
        var connector = $"mcp__{KnowledgeConnector.ServerName}__";
        IReadOnlyList<string> tools = rules is null ? [] : [.. rules.Allow.Where(rule => rule.StartsWith(connector, StringComparison.Ordinal))];

        var servers = new List<HelpWiredServer>();
        foreach (var server in handed.Servers)
        {
            if (server.Name == KnowledgeConnector.ServerName)
            {
                // Daoris's own: its tools are those the allow-list names; in full access every one it offers, so none is listed.
                IReadOnlyList<string>? own = rules is null || full ? null : [.. tools.Select(rule => rule[connector.Length..])];
                servers.Add(handed.ServersHanded
                    ? new HelpWiredServer(server.Name, null, Handed, own)
                    : new HelpWiredServer(server.Name, null, Withheld, own, NoDoor));
                continue;
            }

            var (plugin, declared) = Declared(catalog, server.Name);
            var approved = grants.All.Any(grant => grant.Server.Name == server.Name && grant.State == HelpGrants.Approved);
            servers.Add(new HelpWiredServer(
                server.Name, plugin, !handed.ServersHanded ? Withheld : approved || !full ? Handed : FullAccess, declared,
                handed.ServersHanded ? null : NoDoor));
        }

        if (!handed.Servers.Any(server => server.Name == KnowledgeConnector.ServerName))
        {
            servers.Insert(0, new HelpWiredServer(KnowledgeConnector.ServerName, null, Withheld, null, NotFound));
        }

        // The servers the browser's absence kept off it (D78), each with its reason's code, never an endpoint left unresolved.
        var (withheld, why) = BrowserWithheld(handed.Withheld);
        foreach (var name in withheld.Where(name => servers.All(server => server.Name != name)))
        {
            var (plugin, declared) = Declared(catalog, name);
            servers.Add(new HelpWiredServer(name, plugin, Withheld, declared, why));
        }

        // Restricted: each server declared for it and not approved, said with why.
        if (!full)
        {
            foreach (var grant in grants.All.Where(grant => grant.State != HelpGrants.Approved && servers.All(server => server.Name != grant.Server.Name)))
            {
                servers.Add(new HelpWiredServer(
                    grant.Server.Name, grant.Plugin, Withheld, Tools(grant.Server),
                    grant.State == HelpGrants.Refused ? Refused : grant.Changed ? Changed : NotApproved));
            }
        }

        var skills = handed.SkillPlugins.SelectMany(folder => SkillsIn(folder).Select(name => new HelpWiredSkill(
            name,
            !handed.SkillsHanded ? Withheld : handed.Wire == SessionWire.Pipe ? Handed : Unverified,
            handed.SkillsHanded ? null : NoDoor))).ToList();

        return new HelpReceipt
        {
            At = at,
            Start = handed.GoesOn ? GoingOn : New,
            Mode = handed.Mode,
            Harness = handed.Harness,
            Version = handed.Version,
            Account = handed.Account,
            Door = handed.Wire == SessionWire.Pipe ? Pipe : Acp,
            Posture = handed.Posture?.TrimStart(HelpModes.Required),
            PermissionMode = handed.PermissionMode,
            Folder = handed.Folder,
            Tools = tools,
            Skills = skills,
            Servers = servers,
            Rules = rules,
            Guarded = rules is not null && handed.Guarded,
            Checkouts = rules is null ? [] : handed.Reads,
            Lacks = full ? [] : Lacking(servers, catalog),
        };
    }

    /// <summary>Whether a spawn's command line or environment names <paramref name="path"/>: the native door took the file.</summary>
    public static bool Names(ProcessStartInfo info, string path) =>
        info.ArgumentList.Contains(path, StringComparer.Ordinal)
        || info.Arguments.Contains(path, StringComparison.Ordinal)
        || info.Environment.Values.Any(value => value?.Contains(path, StringComparison.Ordinal) == true);

    /// <summary>Whether a protocol door's <c>session/new</c> meta names <paramref name="path"/>: the agent was pointed at it.</summary>
    public static bool Carries(object? meta, string path) =>
        meta is not null && JsonSerializer.Serialize(meta).Contains(JsonSerializer.Serialize(path)[1..^1], StringComparison.Ordinal);

    /// <summary>
    /// What restricted mode lacks, each with its door: a server not approved on its plugin's page, one refused or for sessions alone
    /// by full access, one the browser's host would bring; and the shell and file edits, full access's until HELPMODE1c's grants.
    /// </summary>
    private static IReadOnlyList<HelpWiredLack> Lacking(IReadOnlyList<HelpWiredServer> servers, PluginCatalog catalog)
    {
        var lacks = new List<HelpWiredLack>();
        foreach (var server in servers.Where(server => server.State == Withheld))
        {
            var door = server.Why switch
            {
                NotApproved or Changed => DoorPlugins,
                Refused => DoorMode,
                NoDoor => DoorHarness,
                _ => DoorHost,
            };
            lacks.Add(new HelpWiredLack("server", server.Name, door, server.Why ?? "", door == DoorPlugins ? server.Plugin : null));
        }

        // Every server its plugins declare for sessions alone: full access hands each (D183 as it amends D170).
        var named = servers.Select(server => server.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var plugin in catalog.Contributing)
        {
            foreach (var server in plugin.Manifest.Servers.Where(server => !server.HandedTo(PluginServerTo.Help) && named.Add(server.Name)))
            {
                lacks.Add(new HelpWiredLack("server", server.Name, DoorMode, NotForHelp, plugin.Manifest.Id));
            }
        }

        lacks.Add(new HelpWiredLack("shell", "shell", DoorMode, RestrictedMode));
        lacks.Add(new HelpWiredLack("edits", "edits", DoorMode, RestrictedMode));
        return lacks;
    }

    /// <summary>The plugin declaring a server by this name, and its declared tools where it names any.</summary>
    private static (string? Plugin, IReadOnlyList<string>? Tools) Declared(PluginCatalog catalog, string name)
    {
        foreach (var plugin in catalog.Contributing)
        {
            if (plugin.Manifest.Servers.FirstOrDefault(server => server.Name == name) is { } server) return (plugin.Manifest.Id, Tools(server));
        }

        return (null, null);
    }

    private static IReadOnlyList<string>? Tools(PluginServer server) =>
        server.Allow.Count + server.Writes.Count == 0 ? null : [.. server.Allow, .. server.Writes];

    /// <summary>The servers a browser note says were withheld, and its reason's code: the host's, or the shell's own words.</summary>
    private static (IReadOnlyList<string> Names, string Why) BrowserWithheld(Noted? notice)
    {
        if (notice is null) return ([], BrowserFailed);
        var names = notice.Parts
            .Where(part => part.Code == NoteCodes.BrowserWithheld.Code)
            .SelectMany(part => part.Values.Where(value => value.Key == "servers").Select(value => value.Value))
            .OfType<IEnumerable<string>>()
            .SelectMany(each => each)
            .ToList();
        var why = notice.Parts.Select(part => part.Code).FirstOrDefault(code =>
            code == NoteCodes.BrowserNoHost.Code || code == NoteCodes.BrowserNoneUp.Code) ?? BrowserFailed;
        return (names, why);
    }

    /// <summary>A composed skills plugin's skills, each <c>plugin:skill</c>, by name.</summary>
    private static IEnumerable<string> SkillsIn(string folder)
    {
        var skills = Path.Combine(folder, "skills");
        var plugin = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        return Directory.Exists(skills)
            ? Directory.GetDirectories(skills).Select(path => $"{plugin}:{Path.GetFileName(path)}").Order(StringComparer.Ordinal)
            : [];
    }

    private IReadOnlyList<HelpReceipt> Oldest(string session)
    {
        if (!SessionEventText.IsId(session)) return [];
        try
        {
            var path = PathOf(session);
            if (!File.Exists(path)) return [];
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return [.. Json.Items(document.RootElement, "receipts").Select(ReadReceipt).OfType<HelpReceipt>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string Checked(string session) =>
        SessionEventText.IsId(session) ? session : throw new DriverException($"`{session}` names no session.");

    private static void Write(Utf8JsonWriter writer, HelpReceipt receipt)
    {
        writer.WriteStartObject();
        writer.WriteString("at", receipt.At);
        writer.WriteString("start", receipt.Start);
        writer.WriteString("mode", receipt.Mode);
        writer.WriteString("harness", receipt.Harness);
        Text(writer, "version", receipt.Version);
        Text(writer, "account", receipt.Account);
        writer.WriteString("door", receipt.Door);
        Text(writer, "posture", receipt.Posture);
        Text(writer, "permissionMode", receipt.PermissionMode);
        writer.WriteString("folder", receipt.Folder);
        Strings(writer, "tools", receipt.Tools);
        writer.WriteStartArray("skills");
        foreach (var skill in receipt.Skills)
        {
            writer.WriteStartObject();
            writer.WriteString("name", skill.Name);
            writer.WriteString("state", skill.State);
            Text(writer, "why", skill.Why);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("servers");
        foreach (var server in receipt.Servers)
        {
            writer.WriteStartObject();
            writer.WriteString("name", server.Name);
            Text(writer, "plugin", server.Plugin);
            writer.WriteString("state", server.State);
            if (server.Tools is { } tools) Strings(writer, "tools", tools);
            else writer.WriteNull("tools");
            Text(writer, "why", server.Why);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (receipt.Rules is { } rules)
        {
            writer.WriteStartObject("rules");
            Strings(writer, "allow", rules.Allow);
            Strings(writer, "ask", rules.Ask);
            Strings(writer, "deny", rules.Deny);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("rules");
        }

        writer.WriteBoolean("guarded", receipt.Guarded);
        writer.WriteStartArray("checkouts");
        foreach (var checkout in receipt.Checkouts)
        {
            writer.WriteStartObject();
            writer.WriteString("name", checkout.Name);
            writer.WriteString("workspace", checkout.Workspace);
            writer.WriteString("path", checkout.Path);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("lacks");
        foreach (var lack in receipt.Lacks)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", lack.Kind);
            writer.WriteString("name", lack.Name);
            writer.WriteString("door", lack.Door);
            writer.WriteString("why", lack.Why);
            Text(writer, "plugin", lack.Plugin);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void Text(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static void Strings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    /// <summary>One receipt as kept, field by field; null for one with no time, agent or folder.</summary>
    private static HelpReceipt? ReadReceipt(JsonElement element)
    {
        if (!element.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.String || !at.TryGetDateTimeOffset(out var when)) return null;
        if (Json.NonEmpty(element, "harness") is not { } harness || Json.Text(element, "folder") is not { } folder) return null;
        return new HelpReceipt
        {
            At = when,
            Start = Json.Text(element, "start") == GoingOn ? GoingOn : New,
            Mode = HelpModes.Recorded(Json.Text(element, "mode")),
            Harness = harness,
            Version = Json.NonEmpty(element, "version"),
            Account = Json.NonEmpty(element, "account"),
            Door = Json.Text(element, "door") == Acp ? Acp : Pipe,
            Posture = Json.NonEmpty(element, "posture"),
            PermissionMode = Json.NonEmpty(element, "permissionMode"),
            Folder = folder,
            Tools = Json.Strings(element, "tools"),
            Skills = [.. Json.Items(element, "skills")
                .Where(skill => Json.NonEmpty(skill, "name") is not null && Json.NonEmpty(skill, "state") is not null)
                .Select(skill => new HelpWiredSkill(Json.NonEmpty(skill, "name")!, Json.NonEmpty(skill, "state")!, Json.NonEmpty(skill, "why")))],
            Servers = [.. Json.Items(element, "servers")
                .Where(server => Json.NonEmpty(server, "name") is not null && Json.NonEmpty(server, "state") is not null)
                .Select(server => new HelpWiredServer(
                    Json.NonEmpty(server, "name")!, Json.NonEmpty(server, "plugin"), Json.NonEmpty(server, "state")!,
                    server.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array ? Json.Strings(server, "tools") : null,
                    Json.NonEmpty(server, "why")))],
            Rules = element.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Object
                ? new HelpWiredRules(Json.Strings(rules, "allow"), Json.Strings(rules, "ask"), Json.Strings(rules, "deny"))
                : null,
            Guarded = Json.Flag(element, "guarded"),
            Checkouts = [.. Json.Items(element, "checkouts")
                .Where(checkout => Json.NonEmpty(checkout, "name") is not null && Json.Text(checkout, "path") is not null)
                .Select(checkout => new HelpRead(Json.NonEmpty(checkout, "name")!, Json.Text(checkout, "workspace") ?? "", Json.Text(checkout, "path")!))],
            Lacks = [.. Json.Items(element, "lacks")
                .Where(lack => Json.NonEmpty(lack, "kind") is not null && Json.NonEmpty(lack, "door") is not null)
                .Select(lack => new HelpWiredLack(
                    Json.NonEmpty(lack, "kind")!, Json.Text(lack, "name") ?? "", Json.NonEmpty(lack, "door")!, Json.Text(lack, "why") ?? "",
                    Json.NonEmpty(lack, "plugin")))],
        };
    }
}
