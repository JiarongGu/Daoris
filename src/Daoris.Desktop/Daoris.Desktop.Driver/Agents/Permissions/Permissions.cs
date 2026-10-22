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

    /// <summary>These lists and <paramref name="more"/>'s, each rule once, in the order first met.</summary>
    public RuleLists Joined(RuleLists more) => new(
        [.. Allow.Union(more.Allow, StringComparer.Ordinal)],
        [.. Ask.Union(more.Ask, StringComparer.Ordinal)],
        [.. Deny.Union(more.Deny, StringComparer.Ordinal)]);
}

/// <summary>A rule set Daoris ships, with the reason it exists — switchable off by id, removable by nothing else.</summary>
/// <param name="Hook">
/// The tools a hook default judges, when it is one (PERM3) — a hook adds no rule, so its
/// <paramref name="Rules"/> are empty and this says what it covers instead.
/// </param>
/// <param name="HardDeny">
/// What the harness's auto-mode classifier is told as a hard denial while this default is on (UNBLOCK4,
/// D122 §3.7): prose it judges every action against, for the forms a rule cannot name. The driver's
/// alone, because only the driver writes the spawn file; the CLI's table carries no twin of it.
/// </param>
public sealed record PermissionDefault(
    string Id, RuleList List, IReadOnlyList<string> Rules, string Why, string? Hook = null, string? HardDeny = null);

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

    /// <summary>
    /// The file as <see cref="PermissionRules.Load"/> understood it, written, or null for rules built in memory (KEEPALL1): what an edit is
    /// measured from, so a save makes only the edit onto the file as it stands.
    /// </summary>
    internal string? Understood { get; init; }

    /// <summary>Whether a file was there when it was read (KEEPALL1): with none then and none now, the save writes the rules whole.</summary>
    internal bool Found { get; init; }

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
/// <para><b>THE FILE is the contract.</b> This is its one reader and writer since `daoris agent rules` became
/// the driver program's (PUSH1f2d, <see cref="AgentsCommand"/>), and a hand edit keeps working. A file that cannot be read is empty
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
    /// What Daoris ships: the one table, which <c>daoris agent rules</c> lists and switches (PUSH1f2d).
    /// </summary>
    public static IReadOnlyList<PermissionDefault> Defaults { get; } =
    [
        new(
            "connector", RuleList.Allow,
            [.. new[]
            {
                "registry", "knowledge_search", "knowledge_get", "knowledge_repositories",
                "knowledge_convergence", "quest_list", "quest_respond", "quest_publish", "permission_propose", "go_ahead_ask",
                "review_serve", "review_ready", "opinion_give", "opinion_answer",
                "helper_ask", "helper_answer", "partner_answer", "channel_post", "channel_read", "handover_propose", "line_read", "question_ask",
            }.Select(tool => ConnectorPrefix + tool)],
            // Not `knowledge_refresh`: rebuilding the index is the machine's job, never a session's. And
            // `permission_propose` (PERM2, D74) only PROPOSES — a widening still waits for the person. `go_ahead_ask`
            // (KNOWUSE1a, D135 §2) only ASKS — the act it names still waits for the person's yes. `review_serve` and
            // `review_ready` (REVIEWENV1b2, D154 points 5 and 8) are a set-up step's, answered for its own session only:
            // serving shows its build to its own tab, and saying what it showed gives no verdict, which is the person's.
            // `opinion_give` and `opinion_answer` (XAGENT1c2, D155; the second agent design §5.4, §6.1, §6.4) are answered
            // for an opinion's own reviewer and for the working session handed it only: each says a claim, and neither
            // asks for an opinion, lands the work or answers a dispute, which is the person's. `helper_ask` and `helper_answer`
            // (MIXAGENT2a-i, D163 point 2) write a file each under the home and nothing else: an ask the driver takes under the
            // person's `helpers` rule, and a helper's answer, which reaches the asker as another agent's claim, never the person's.
            // `partner_answer` (COAGENT1b, D199 point 8) writes one file under the home, for the piece the driver started its
            // session for, and is refused to every other session: the partner's claim beside the files Daoris reads itself.
            // `channel_post` and `channel_read` (SESSONE2a/2b, D167 point 7) speak and read for the session's own ask only, a
            // helper and a reviewer refused: a message is that agent's claim, reaches no run it does not already have, and asks
            // nothing of anyone, so work and requests stay quests. `handover_propose` (MIXAGENT1c, D163 point 3) writes one file
            // under the home: an ask to go on with another agent, its note the agent's claim, settled by the person's `handOver` rule.
            // `line_read` (SESSCTX1f, D189's note) changes nothing: it reads the event files of the session's own line on this machine,
            // what its brief's bound left out, and the service refuses every other record. `question_ask` (PARKVIEW1) writes one file
            // under the home: the question its park waits on, the agent's words, which the person answers; it decides nothing.
            "A session takes and closes its own quest, publishes what it finds for others and proposes a "
            + "change to these rules, through Daoris's connector — and anything it would have to ask for is "
            + "refused."),
        // 🔴 The owner's answer to PERM4 (2026-09-24), from a measured failure: in a folder the agent had
        // never trusted, a real driven session made its edit, was refused the commit — the repository's
        // own allow-list does not apply there — and declined. `cd` because the agent prefixes its commit
        // with one, and every part of a compound command must be allowed. `git mv` (UNBLOCK4, D122 §3.6):
        // a rename stays in the tree, is taken back by another, and git refuses a path outside the
        // repository. A merge, a discard and a switch of branch stay out.
        new(
            "commit", RuleList.Allow,
            ["Bash(cd:*)", "Bash(git add:*)", "Bash(git commit:*)", "Bash(git mv:*)"],
            "A session commits its own work in its own tree, because committing is part of finishing a task "
            + "— the push is still refused."),
        // 🔴 UNBLOCK4 (D122 §3.7): the harness matches a Bash rule against the command as written, so
        // `git push …` alone let `git -C . push` and `git -c <key>=<value> push` through, and auto mode's
        // classifier allows a push to the working repository by default. The option-first rules take
        // every push with options before its subcommand, on both doors, whatever the mode. A deny's `*`
        // before the subcommand is the maker's own shape for that; only an ALLOW so written draws its
        // warning. The price, stated: a `git -C <dir> commit` whose message has "push" as a word before
        // another is refused too. What no rule can name — a quoted subcommand, an alias, a path to git, a
        // shell running it — is the classifier's, told below; on the pipe door nothing allows it anyway.
        new(
            "no-push", RuleList.Deny,
            ["Bash(git push)", "Bash(git push:*)", "Bash(git -* push)", "Bash(git -* push *)"],
            // UNBLOCK4 left the reason as it was: the page's Chinese catalogue translates it by id, and the
            // rules say the rest.
            "A push leaves this machine, and that stays the person's call.",
            HardDeny: "Pushing to any remote in any form (for example git -C <dir> push, git -c <key>=<value> "
            + "push), publishing a package, or creating a release: these stay the person's."),
        new(
            TreeGuardId, RuleList.Deny,
            [],
            "A session writes files only inside its own tree: an edit or a write anywhere else is refused, "
            + "through links as well. A change needed elsewhere is a quest.",
            Hook: TreeGuard.Matcher),
    ];

    /// <summary>
    /// A tool name, then an optional parenthesised specifier on the same line — the harness's own shape, which the CLI's
    /// <c>SHAPE</c> and the service's <c>RuleProposalBox</c> twin. <c>\z</c>, since .NET's <c>$</c> also passes a final
    /// line break the CLI's refuses, and the screen's door hands the rule untrimmed (PERMSHAPE1).
    /// </summary>
    private static readonly Regex Shape = new(@"^[A-Za-z][A-Za-z0-9_-]*(\([^\r\n]+\))?\z", RegexOptions.CultureInvariant);

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Permissions);

    /// <summary>
    /// What the harness's auto-mode classifier is told as hard denials (UNBLOCK4, D122 §3.7): the sentence
    /// of each default still on that carries one — today `no-push`'s, so nothing while the person has
    /// switched it off. The harness's own list is kept by <see cref="SpawnSettings.Write"/>, never here.
    /// </summary>
    public static IReadOnlyList<string> HardDeny(PermissionFile file) =>
    [
        .. Defaults
            .Where(shipped => shipped.HardDeny is not null && !file.DefaultsOff.Contains(shipped.Id, StringComparer.Ordinal))
            .Select(shipped => shipped.HardDeny!),
    ];

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
    public static string ReadRule(string directory) => $"Read({Absolute(directory)})";

    /// <summary>
    /// An edit of everything under <paramref name="directory"/>, in the same absolute form as
    /// <see cref="ReadRule"/> (D107): allowed in a declared write target, denied in every other checkout.
    /// The harness applies an Edit rule to each of its tools that writes a file.
    /// </summary>
    public static string EditRule(string directory) => $"Edit({Absolute(directory)})";

    /// <summary>Everything under a directory as the harness's absolute pattern: <c>//c/x/**</c> for <c>C:\x</c>.</summary>
    private static string Absolute(string directory)
    {
        var posix = directory.Replace('\\', '/').TrimEnd('/');
        if (posix.Length >= 2 && char.IsAsciiLetter(posix[0]) && posix[1] == ':')
        {
            posix = "/" + char.ToLowerInvariant(posix[0]) + posix[2..];
        }

        return $"/{posix}/**";
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
        if (!File.Exists(path)) return PermissionFile.Empty with { Understood = Written(PermissionFile.Empty) };

        try
        {
            if (JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject root)
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

            var file = new PermissionFile(
                Lists(root["machine"]),
                Scopes(root["workspaces"]),
                Scopes(root["repositories"]),
                Json.TrimmedStrings(root["defaultsOff"]))
            {
                Rest = rest,
                Found = true,
            };
            return file with { Understood = Written(file) };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return PermissionFile.Empty with
            {
                Problem = $"{FileName} could not be read ({error.Message}), so no rule of it applies. The defaults still do.",
            };
        }
    }

    /// <summary>
    /// Written beside and renamed over, BOM-less and LF, keeping what a newer build wrote: the edit from what was read made onto the file as
    /// it stands (KEEPALL1, <see cref="JsonKeeping.Save"/>), so a key this build has no field for stays where it is at every level, a
    /// change another writer made since the read stays, and no edit writes nothing. Rebuilt from the fields it reads, it moved what it did
    /// not know to the front and deleted a scope's own unknown keys. Rules built in memory are written whole.
    /// </summary>
    /// <exception cref="DriverException">
    /// The file could not be read when <paramref name="file"/> was loaded (REV3). What it holds is the
    /// empty read, and writing it would drop every rule the file had. The CLI refuses the same edit. Or the file there now does not read,
    /// holds a key twice, or made a scope this edit changes no longer an object.
    /// </exception>
    public static void Save(string home, PermissionFile file)
    {
        if (file.Problem is not null)
        {
            throw new DriverException(
                $"{FileName} could not be read, so this edit was not written — it would have replaced every "
                + "rule in the file with this one change. Fix the file or remove it, then make the change again.");
        }

        // The home and the folder the rules are in (`data/` since HOMELAYOUT3), which a new home has not made yet.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathOf(home)))!);
        if (file.Understood is { } understood) JsonKeeping.Save(PathOf(home), file.Found, understood, Written(file));
        else JsonFile.Write(PathOf(home), Root(file));
    }

    /// <summary>The rules' own shape, written: what neither knows first, then each scope only where it holds a rule.</summary>
    private static string Written(PermissionFile file) => JsonFile.Serialize(Root(file));

    private static JsonObject Root(PermissionFile file)
    {
        var root = new JsonObject();
        foreach (var (key, value) in file.Rest) root[key] = value?.DeepClone();

        if (!file.Machine.IsEmpty) root["machine"] = Node(file.Machine);
        if (ScopesNode(file.Workspaces) is { } workspaces) root["workspaces"] = workspaces;
        if (ScopesNode(file.Repositories) is { } repositories) root["repositories"] = repositories;
        if (file.DefaultsOff.Count > 0) root["defaultsOff"] = new JsonArray([.. file.DefaultsOff.Order(StringComparer.Ordinal).Select(id => (JsonNode)id)]);
        return root;
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
        [.. Json.TrimmedStrings(node).Where(rule => Refusal(rule) is null).Distinct(StringComparer.Ordinal)];

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
    /// <summary>The harness's own hard denials, kept by naming them in the list (UNBLOCK4).</summary>
    public const string HarnessDefaults = "$defaults";

    /// <summary>Write the file for one session, or answer null when there is nothing to hand.</summary>
    /// <param name="hardDeny">
    /// What auto mode's classifier is told as hard denials (<see cref="PermissionRules.HardDeny"/>), or
    /// empty for none. Required, so no spawn forgets to say (UNBLOCK4): a session on either door is handed
    /// the same file, and the classifier reads it from this command-line tier.
    /// </param>
    /// <param name="guard">The tree guard for this session (PERM3), or null when it is switched off.</param>
    /// <param name="lineage">
    /// The lineage hook for this session (FABRIC-C1a), or null for none: on the connector's calls and each subagent's start and
    /// stop, beside the guard, never instead of it.
    /// </param>
    public static string? Write(
        string home, string sessionId, RuleLists rules, IReadOnlyList<string> hardDeny, TreeGuardHook? guard = null, LineageHook? lineage = null)
    {
        var told = hardDeny.Where(entry => entry != HarnessDefaults).Distinct(StringComparer.Ordinal).ToList();
        if (rules.IsEmpty && guard is null && lineage is null && told.Count == 0) return null;

        var folder = DaorisHome.Entry(home, HomeEntry.Spawn);
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

        // 🔴 `$defaults` FIRST, always (D122 §3.7): a `hard_deny` list without it replaces the harness's
        // built-in list, whose entry is the rule against sending data out. No `autoMode` key at all when
        // nothing is told, so the harness's own lists stand untouched.
        if (told.Count > 0)
        {
            document["autoMode"] = new JsonObject
            {
                ["hard_deny"] = new JsonArray([HarnessDefaults, .. told.Select(entry => (JsonNode)entry)]),
            };
        }

        // 🔴 EXEC form — `args` present — so the harness spawns node directly with the script and the
        // tree as one argument each. Shell form would run the line through Git Bash or PowerShell on
        // Windows, and a path survives neither's quoting reliably.
        if (guard is not null || lineage is not null)
        {
            var before = new JsonArray();
            if (guard is not null)
            {
                before.Add(new JsonObject
                {
                    ["matcher"] = TreeGuard.Matcher,
                    ["hooks"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "command",
                        // The node Tools resolves, by its whole path (TOOLS5): the guard runs on the person's choice.
                        ["command"] = guard.Node,
                        // The declared write targets follow the tree, one argument each (D107).
                        ["args"] = new JsonArray([guard.Script, guard.Tree, .. guard.Also.Select(also => (JsonNode)also)]),
                        ["timeout"] = 30,
                    }),
                });
            }

            var hooks = new JsonObject();
            if (lineage is not null)
            {
                // FABRIC-C1a: which agent made each call to the connector, and each subagent's start and stop, appended to the
                // session's lineage file. It decides nothing, so it is the guard's neighbour and never in its entry.
                JsonArray Lineage() => new(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = lineage.Node,
                    ["args"] = new JsonArray(lineage.Script, lineage.File),
                    ["timeout"] = 10,
                });
                before.Add(new JsonObject { ["matcher"] = ChildLineage.Matcher, ["hooks"] = Lineage() });
                hooks["PreToolUse"] = before;
                hooks["SubagentStart"] = new JsonArray(new JsonObject { ["hooks"] = Lineage() });
                hooks["SubagentStop"] = new JsonArray(new JsonObject { ["hooks"] = Lineage() });
            }
            else
            {
                hooks["PreToolUse"] = before;
            }

            document["hooks"] = hooks;
        }

        JsonFile.Write(path, document);
        return path;
    }

    /// <summary>The file is the session's; it goes when the session does.</summary>
    public static void Remove(string? path) => SpawnServers.Remove(path);
}
