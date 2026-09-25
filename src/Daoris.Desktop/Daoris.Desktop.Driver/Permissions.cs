using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>Which list a rule sits in — the harness's own three (PERM1, D72).</summary>
public enum RuleList
{
    Allow,
    Ask,
    Deny,
}

/// <summary>Where a rule reaches: every session on this machine, one circle's, or one repository's.</summary>
public enum RuleScope
{
    Machine,
    Workspace,
    Repository,
}

/// <summary>Three lists of harness permission rules, in the order they were written.</summary>
public sealed record RuleLists(IReadOnlyList<string> Allow, IReadOnlyList<string> Ask, IReadOnlyList<string> Deny)
{
    public static RuleLists Empty { get; } = new([], [], []);

    public bool IsEmpty => Allow.Count == 0 && Ask.Count == 0 && Deny.Count == 0;
}

/// <summary>A rule set Daoris ships, with the reason it exists — switchable off by id, removable by nothing else.</summary>
/// <param name="Hook">
/// The tools a hook default judges, when it is one (PERM3) — a hook adds no rule, so its
/// <paramref name="Rules"/> are empty and this says what it covers instead.
/// </param>
public sealed record PermissionDefault(
    string Id, RuleList List, IReadOnlyList<string> Rules, string Why, string? Hook = null);

/// <summary>
/// `permissions.json` as it stands: the machine's rules, each circle's, each repository's, and the
/// defaults switched off.
/// </summary>
/// <param name="Problem">Why the file was read as empty, when it could not be read — said, never thrown.</param>
public sealed record PermissionFile(
    RuleLists Machine,
    IReadOnlyDictionary<string, RuleLists> Workspaces,
    IReadOnlyDictionary<string, RuleLists> Repositories,
    IReadOnlyList<string> DefaultsOff,
    string? Problem = null)
{
    /// <summary>Whatever a newer build wrote here, kept on every write.</summary>
    internal JsonObject Rest { get; init; } = [];

    public static PermissionFile Empty { get; } = new(
        RuleLists.Empty,
        new Dictionary<string, RuleLists>(StringComparer.Ordinal),
        new Dictionary<string, RuleLists>(StringComparer.Ordinal),
        []);
}

/// <summary>
/// What an agent Daoris starts may do (PERM1, D72): Claude Code's own permission rules, held by Daoris
/// in three scopes of its own and handed to the harness at spawn as one more scope.
/// </summary>
/// <remarks>
/// <para><b>Daoris owns the union, never the precedence.</b> The defaults, the machine's rules, the
/// session's circle's and its repository's are unioned and handed over as the harness's command-line
/// tier; the harness merges that with the person's own settings and the repository's, and `deny` beats
/// `ask` beats `allow` there. So a default `deny` survives a repository's `allow`, and a repository's
/// `deny` survives anything Daoris allows. A second precedence here would disagree with the harness's
/// exactly when it mattered.</para>
///
/// <para><b>THE FILE is the contract.</b> The CLI's `permissions.ts` reads and writes it by the same rules
/// (the two artefacts share no code), and a hand edit keeps working. A file that cannot be read is empty
/// and says why, because wiring never stops a spawn (D21) — and the defaults, the guard among them,
/// still hold.</para>
/// </remarks>
public static class PermissionRules
{
    public const string FileName = "permissions.json";

    /// <summary>The tree guard's id (PERM3) — a default that is a hook rather than a rule.</summary>
    public const string TreeGuardId = "tree-guard";

    /// <summary>The connector's own name, as its tools arrive: `mcp__&lt;server&gt;__&lt;tool&gt;`.</summary>
    private const string ConnectorPrefix = $"mcp__{KnowledgeConnector.ServerName}__";

    /// <summary>
    /// What Daoris ships. 🔴 The CLI's `permissions.ts` holds the same table, and a test reads it.
    /// </summary>
    public static IReadOnlyList<PermissionDefault> Defaults { get; } =
    [
        new(
            "connector", RuleList.Allow,
            [.. new[]
            {
                "registry", "knowledge_search", "knowledge_get", "knowledge_repositories",
                "knowledge_convergence", "quest_list", "quest_respond", "quest_publish", "permission_propose",
            }.Select(tool => ConnectorPrefix + tool)],
            // Not `knowledge_refresh`: rebuilding the index is the machine's job, never a session's. And
            // `permission_propose` (PERM2, D74) only PROPOSES — a widening still waits for the person.
            "A session takes and closes its own quest, publishes what it finds for others and proposes a "
            + "change to these rules, through Daoris's connector — and anything it would have to ask for is "
            + "refused."),
        // 🔴 The owner's answer to PERM4 (2026-09-24), from a measured failure: in a folder the agent had
        // never trusted, a real driven session made its edit, was refused the commit — the repository's
        // own allow-list does not apply there — and declined. `cd` because the agent prefixes its commit
        // with one, and every part of a compound command must be allowed.
        new(
            "commit", RuleList.Allow,
            ["Bash(cd:*)", "Bash(git add:*)", "Bash(git commit:*)"],
            "A session commits its own work in its own tree, because committing is part of finishing a task "
            + "— the push is still refused."),
        new(
            "no-push", RuleList.Deny,
            ["Bash(git push)", "Bash(git push:*)"],
            "A push leaves this machine, and that stays the person's call."),
        new(
            TreeGuardId, RuleList.Deny,
            [],
            "A session writes files only inside its own tree: an edit or a write anywhere else is refused, "
            + "through links as well. A change needed elsewhere is a quest.",
            Hook: TreeGuard.Matcher),
    ];

    /// <summary>A tool name, then an optional parenthesised specifier on the same line — the harness's own shape.</summary>
    private static readonly Regex Shape = new(@"^[A-Za-z][A-Za-z0-9_-]*(\([^\r\n]+\))?$", RegexOptions.CultureInvariant);

    public static string PathOf(string home) => Path.Combine(home, FileName);

    /// <summary>Whether sessions are handed the tree guard (PERM3): on unless the person switched it off.</summary>
    public static bool GuardsTree(PermissionFile file) => !file.DefaultsOff.Contains(TreeGuardId, StringComparer.Ordinal);

    /// <summary>
    /// Whether a session handed <paramref name="rules"/> may call the connector's <paramref name="tool"/>
    /// without being asked (D73): allowed by the tool's own rule, the server's, or its wildcard, and
    /// named by no ask or deny rule, since an asked permission is refused over either door (D52).
    /// </summary>
    public static bool AllowsConnector(RuleLists rules, string tool)
    {
        string[] names = [ConnectorPrefix + tool, $"mcp__{KnowledgeConnector.ServerName}", ConnectorPrefix + "*"];
        bool Named(IReadOnlyList<string> list) => list.Any(rule => names.Contains(rule, StringComparer.Ordinal));
        return Named(rules.Allow) && !Named(rules.Ask) && !Named(rules.Deny);
    }

    /// <summary>
    /// A read of everything under <paramref name="directory"/>, in the harness's own absolute form
    /// (INT4j): the folder a session's own quest or ask keeps its files in, which lies outside its tree.
    /// </summary>
    /// <remarks>
    /// <para><b>The form is Claude Code's, read from its bundle (2.1.281), not guessed.</b> A pattern
    /// that starts with <c>//</c> is an absolute path from the filesystem root (its rule resolver strips
    /// one slash), and on Windows a Read's target is normalised to POSIX form before it is compared —
    /// <c>C:\x</c> becomes <c>/c/x</c>, the drive lower-cased — so the rule is written the same way.</para>
    ///
    /// <para>🔴 <b>Nothing else is re-cased.</b> An allow is compared case-sensitively there (only an ask or
    /// a deny is case-folded on Windows), so the path keeps exactly the case the service answered it in,
    /// which is the case the session's prompt shows it.</para>
    /// </remarks>
    public static string ReadRule(string directory)
    {
        var posix = directory.Replace('\\', '/').TrimEnd('/');
        if (posix.Length >= 2 && char.IsAsciiLetter(posix[0]) && posix[1] == ':')
        {
            posix = "/" + char.ToLowerInvariant(posix[0]) + posix[2..];
        }

        return $"Read(/{posix}/**)";
    }

    /// <summary>Why a rule is refused, or null when it is the harness's shape.</summary>
    public static string? Refusal(string rule) =>
        Shape.IsMatch(rule ?? "")
            ? null
            : $"`{rule}` is not a permission rule — one is a tool name with an optional specifier in "
              + "parentheses, like `Bash(npm run test:*)`, `Edit(/src/**)` or `mcp__daoris-knowledge__quest_list`.";

    /// <summary>The file as it stands: absent is empty, and unreadable is empty and says why.</summary>
    public static PermissionFile Load(string home)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return PermissionFile.Empty;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root)
            {
                return PermissionFile.Empty with { Problem = $"{FileName} is not a JSON object, so no rule of it applies." };
            }

            var rest = new JsonObject();
            foreach (var (key, value) in root)
            {
                if (key is not ("machine" or "workspaces" or "repositories" or "defaultsOff"))
                {
                    rest[key] = value?.DeepClone();
                }
            }

            return new PermissionFile(
                Lists(root["machine"]),
                Scopes(root["workspaces"]),
                Scopes(root["repositories"]),
                Strings(root["defaultsOff"]))
            {
                Rest = rest,
            };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return PermissionFile.Empty with
            {
                Problem = $"{FileName} could not be read ({error.Message}), so no rule of it applies. The defaults still do.",
            };
        }
    }

    /// <summary>Written beside and renamed over, BOM-less and LF, keeping what a newer build wrote.</summary>
    /// <exception cref="DriverException">
    /// The file could not be read when <paramref name="file"/> was loaded (REV3). What it holds is the
    /// empty read, and writing it would drop every rule the file had. The CLI refuses the same edit.
    /// </exception>
    public static void Save(string home, PermissionFile file)
    {
        if (file.Problem is not null)
        {
            throw new DriverException(
                $"{FileName} could not be read, so this edit was not written — it would have replaced every "
                + "rule in the file with this one change. Fix the file or remove it, then make the change again.");
        }

        Directory.CreateDirectory(home);
        var root = new JsonObject();
        foreach (var (key, value) in file.Rest) root[key] = value?.DeepClone();

        if (!file.Machine.IsEmpty) root["machine"] = Node(file.Machine);
        if (ScopesNode(file.Workspaces) is { } workspaces) root["workspaces"] = workspaces;
        if (ScopesNode(file.Repositories) is { } repositories) root["repositories"] = repositories;
        if (file.DefaultsOff.Count > 0) root["defaultsOff"] = new JsonArray([.. file.DefaultsOff.Order(StringComparer.Ordinal).Select(id => (JsonNode)id)]);

        var path = PathOf(home);
        AtomicFile.WriteText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>
    /// The union one session is handed: the defaults still on, the machine's rules, its circle's and its
    /// repository's — each rule once, in the order first met.
    /// </summary>
    /// <param name="workspace">The session's circle; unstated is the default circle, as everywhere.</param>
    /// <param name="repository">The session's repository, or null for one that serves no repository (an intake).</param>
    public static RuleLists Compose(PermissionFile file, string? workspace, string? repository)
    {
        var layers = new List<RuleLists>();
        foreach (var shipped in Defaults.Where(d => !file.DefaultsOff.Contains(d.Id, StringComparer.Ordinal)))
        {
            layers.Add(shipped.List switch
            {
                RuleList.Allow => new RuleLists(shipped.Rules, [], []),
                RuleList.Ask => new RuleLists([], shipped.Rules, []),
                _ => new RuleLists([], [], shipped.Rules),
            });
        }

        layers.Add(file.Machine);
        // 🔴 A scope is a person's name, compared as every other layer compares one — trimmed, case-
        // insensitive (REV3). Ordinal, a quest to `engine` was handed none of `Engine`'s denies. Every
        // matching scope is taken, so a file that holds both spellings loses no rule from either.
        layers.AddRange(Named(file.Workspaces, Circle(workspace)));
        if (repository is { Length: > 0 }) layers.AddRange(Named(file.Repositories, repository.Trim()));

        IReadOnlyList<string> Union(Func<RuleLists, IReadOnlyList<string>> pick) =>
            [.. layers.SelectMany(pick).Distinct(StringComparer.Ordinal)];

        return new RuleLists(Union(l => l.Allow), Union(l => l.Ask), Union(l => l.Deny));
    }

    /// <summary>
    /// A rule into one list of one scope — and out of that scope's other lists, so a rule has one place
    /// per scope.
    /// </summary>
    public static PermissionFile Add(PermissionFile file, RuleScope scope, string? name, RuleList list, string rule)
    {
        if (Refusal(rule) is { } refused) throw new DriverException(refused);
        return Edit(file, scope, name, lists =>
        {
            var without = Drop(lists, rule);
            return list switch
            {
                RuleList.Allow => without with { Allow = [.. without.Allow, rule] },
                RuleList.Ask => without with { Ask = [.. without.Ask, rule] },
                _ => without with { Deny = [.. without.Deny, rule] },
            };
        });
    }

    /// <summary>A rule out of every list of one scope.</summary>
    public static PermissionFile Remove(PermissionFile file, RuleScope scope, string? name, string rule) =>
        Edit(file, scope, name, lists => Drop(lists, rule));

    /// <summary>A default switched on or off for this machine, by the id it ships under.</summary>
    public static PermissionFile SwitchDefault(PermissionFile file, string id, bool on)
    {
        if (!Defaults.Any(d => d.Id == id))
        {
            throw new DriverException(
                $"no default `{id}` — Daoris ships {string.Join(", ", Defaults.Select(d => $"`{d.Id}`"))}.");
        }

        var off = file.DefaultsOff.Where(existing => existing != id).ToList();
        if (!on) off.Add(id);
        return file with { DefaultsOff = off };
    }

    private static PermissionFile Edit(PermissionFile file, RuleScope scope, string? name, Func<RuleLists, RuleLists> change)
    {
        if (scope == RuleScope.Machine) return file with { Machine = change(file.Machine) };

        if (name is not { } given || string.IsNullOrWhiteSpace(given))
        {
            throw new DriverException(
                $"a {(scope == RuleScope.Workspace ? "workspace" : "repository")} scope needs its name.");
        }

        var held = scope == RuleScope.Workspace ? file.Workspaces : file.Repositories;
        // The scope's existing spelling, when it has one in another case: one scope, never two.
        var key = held.Keys.FirstOrDefault(k => string.Equals(k.Trim(), given.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? given.Trim();
        var next = new Dictionary<string, RuleLists>(held, StringComparer.Ordinal)
        {
            [key] = change(held.GetValueOrDefault(key) ?? RuleLists.Empty),
        };
        if (next[key].IsEmpty) next.Remove(key);

        return scope == RuleScope.Workspace ? file with { Workspaces = next } : file with { Repositories = next };
    }

    private static RuleLists Drop(RuleLists lists, string rule) => new(
        [.. lists.Allow.Where(r => r != rule)], [.. lists.Ask.Where(r => r != rule)], [.. lists.Deny.Where(r => r != rule)]);

    /// <summary>Every scope held under <paramref name="name"/>, in any case.</summary>
    private static IEnumerable<RuleLists> Named(IReadOnlyDictionary<string, RuleLists> scopes, string name) =>
        scopes.Where(pair => string.Equals(pair.Key.Trim(), name, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value);

    private static string Circle(string? workspace) =>
        workspace is { } named && !string.IsNullOrWhiteSpace(named) ? named.Trim() : RemoteTarget.DefaultWorkspace;

    private static RuleLists Lists(JsonNode? node) =>
        node is JsonObject lists
            ? new RuleLists(Rules(lists["allow"]), Rules(lists["ask"]), Rules(lists["deny"]))
            : RuleLists.Empty;

    /// <summary>A hand edit's rule that is not the shape is left out rather than handed to the harness.</summary>
    private static IReadOnlyList<string> Rules(JsonNode? node) =>
        [.. Strings(node).Where(rule => Refusal(rule) is null).Distinct(StringComparer.Ordinal)];

    private static IReadOnlyList<string> Strings(JsonNode? node) =>
        node is JsonArray items
            ? [.. items.OfType<JsonValue>().Select(item => item.TryGetValue<string>(out var text) ? text : null)
                .Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text!.Trim())]
            : [];

    private static IReadOnlyDictionary<string, RuleLists> Scopes(JsonNode? node)
    {
        var scopes = new Dictionary<string, RuleLists>(StringComparer.Ordinal);
        if (node is not JsonObject named) return scopes;
        foreach (var (key, value) in named)
        {
            var lists = Lists(value);
            if (!string.IsNullOrWhiteSpace(key) && !lists.IsEmpty) scopes[key.Trim()] = lists;
        }

        return scopes;
    }

    private static JsonObject Node(RuleLists lists)
    {
        var node = new JsonObject();
        if (lists.Allow.Count > 0) node["allow"] = Array(lists.Allow);
        if (lists.Ask.Count > 0) node["ask"] = Array(lists.Ask);
        if (lists.Deny.Count > 0) node["deny"] = Array(lists.Deny);
        return node;
    }

    private static JsonObject? ScopesNode(IReadOnlyDictionary<string, RuleLists> scopes)
    {
        var node = new JsonObject();
        foreach (var (key, lists) in scopes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!lists.IsEmpty) node[key] = Node(lists);
        }

        return node.Count > 0 ? node : null;
    }

    private static JsonArray Array(IEnumerable<string> items) => new([.. items.Select(item => (JsonNode)item)]);
}

/// <summary>
/// One session's composed rules as the harness's own settings file, under the home beside the servers
/// file the same session is handed (<see cref="SpawnServers"/>) — and gone when the session is.
/// </summary>
public static class SpawnSettings
{
    /// <summary>Write the file for one session, or answer null when there is nothing to hand.</summary>
    /// <param name="guard">The tree guard for this session (PERM3), or null when it is switched off.</param>
    public static string? Write(string home, string sessionId, RuleLists rules, TreeGuardHook? guard = null)
    {
        if (rules.IsEmpty && guard is null) return null;

        var folder = Path.Combine(home, SpawnServers.Folder);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{sessionId}.settings.json");

        var document = new JsonObject
        {
            ["permissions"] = new JsonObject
            {
                ["allow"] = new JsonArray([.. rules.Allow.Select(rule => (JsonNode)rule)]),
                ["ask"] = new JsonArray([.. rules.Ask.Select(rule => (JsonNode)rule)]),
                ["deny"] = new JsonArray([.. rules.Deny.Select(rule => (JsonNode)rule)]),
            },
        };

        // 🔴 EXEC form — `args` present — so the harness spawns node directly with the script and the
        // tree as one argument each. Shell form would run the line through Git Bash or PowerShell on
        // Windows, and a path survives neither's quoting reliably.
        if (guard is not null)
        {
            document["hooks"] = new JsonObject
            {
                ["PreToolUse"] = new JsonArray(new JsonObject
                {
                    ["matcher"] = TreeGuard.Matcher,
                    ["hooks"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "command",
                        ["command"] = "node",
                        ["args"] = new JsonArray(guard.Script, guard.Tree),
                        ["timeout"] = 30,
                    }),
                }),
            };
        }

        AtomicFile.WriteText(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
        return path;
    }

    /// <summary>The file is the session's; it goes when the session does.</summary>
    public static void Remove(string? path) => SpawnServers.Remove(path);
}
