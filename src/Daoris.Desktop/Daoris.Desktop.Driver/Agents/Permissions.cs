using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>Which list a rule sits in — the harness's own three.</summary>
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

/// <summary>A rule set Daoris ships, with the reason it exists — switchable off by id unless fixed, removable by nothing else.</summary>
/// <param name="OptIn">
/// Off until the person switches it on (<c>defaultsOn</c> in the rules file): a limit Daoris adds to no agent of its own accord
/// (<c>no-push</c>, <c>no-publish</c>, the tree guard). Every other default is on until the person switches it off.
/// </param>
/// <param name="Hook">
/// The tools a hook default judges, when it is one — a hook adds no rule, so its
/// <paramref name="Rules"/> are empty and this says what it covers instead.
/// </param>
/// <param name="HardDeny">
/// What the harness's auto-mode classifier is told as a hard denial while this default is on:
/// prose it judges every action against, for the forms a rule cannot name. The driver's
/// alone, because only the driver writes the spawn file; the CLI's table carries no twin of it.
/// </param>
public sealed record PermissionDefault(
    string Id, RuleList List, IReadOnlyList<string> Rules, string Why, string? Hook = null, string? HardDeny = null, bool OptIn = false);

/// <summary>
/// `permissions.json` as it stands: the machine's rules, each circle's, each repository's, and the
/// defaults switched off.
/// </summary>
/// <param name="Problem">Why the file reads as empty, when it cannot be read — said, never thrown.</param>
public sealed record PermissionFile(
    RuleLists Machine,
    IReadOnlyDictionary<string, RuleLists> Workspaces,
    IReadOnlyDictionary<string, RuleLists> Repositories,
    IReadOnlyList<string> DefaultsOff,
    string? Problem = null)
{
    /// <summary>The opt-in defaults the person switched on (<see cref="PermissionDefault.OptIn"/>).</summary>
    public IReadOnlyList<string> DefaultsOn { get; init; } = [];

    /// <summary>Whatever a newer build wrote here, kept on every write.</summary>
    internal JsonObject Rest { get; init; } = [];

    /// <summary>
    /// The file as <see cref="PermissionRules.Load"/> understood it, written, or null for rules built in memory: what an edit is
    /// measured from, so a save makes only the edit onto the file as it stands.
    /// </summary>
    internal string? Understood { get; init; }

    /// <summary>Whether a file is there when it is read: with none then and none now, the save writes the rules whole.</summary>
    internal bool Found { get; init; }

    public static PermissionFile Empty { get; } = new(
        RuleLists.Empty,
        new Dictionary<string, RuleLists>(StringComparer.Ordinal),
        new Dictionary<string, RuleLists>(StringComparer.Ordinal),
        []);
}

/// <summary>
/// What an agent Daoris starts may do: Claude Code's own permission rules, held by Daoris
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
/// <para><b>THE FILE is the contract.</b> This is its one reader and writer since `daoris agent rules` is
/// the driver program's (<see cref="AgentsCommand"/>), and a hand edit keeps working. A file that cannot be read is empty
/// and says why, because wiring never stops a spawn — and the defaults, the guard among them,
/// still hold.</para>
/// </remarks>
public static class PermissionRules
{
    public const string FileName = "permissions.json";

    /// <summary>The tree guard's id — a default that is a hook rather than a rule.</summary>
    public const string TreeGuardId = "tree-guard";

    /// <summary>The push forms' default, fixed: git access's Write composes it (<see cref="GitAccessPolicy"/>).</summary>
    public const string NoPushId = "no-push";

    /// <summary>The publishing and release sentence's default, switched alone.</summary>
    public const string NoPublishId = "no-publish";

    /// <summary>The connector's own name, as its tools arrive: `mcp__&lt;server&gt;__&lt;tool&gt;`.</summary>
    private const string ConnectorPrefix = $"mcp__{KnowledgeConnector.ServerName}__";

    /// <summary>
    /// What Daoris ships: the one table, which <c>daoris agent rules</c> lists and switches.
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
                "helper_ask", "helper_answer", "check_run", "partner_answer", "channel_post", "channel_read", "handover_propose", "line_read", "handback_read",
                "question_ask", "memory_search", "memory_get", "memory_write", "memory_end", "memory_propose", "system_plan", "team_plan",
            }.Select(tool => ConnectorPrefix + tool)],
            // Not `knowledge_refresh`: it rebuilds the index every session shares, so the harness asks. And
            // `permission_propose` only PROPOSES — a widening still waits for the person. `go_ahead_ask`
            // only ASKS — the act it names still waits for the person's yes. `review_serve` and
            // `review_ready` are a set-up step's, answered for its own session only:
            // serving shows its build to its own tab, and saying what it showed gives no verdict, which is the person's.
            // `opinion_give` and `opinion_answer` are answered
            // for an opinion's own reviewer and for the working session handed it only: each says a claim, and neither
            // asks for an opinion, lands the work or answers a dispute, which is the person's. `helper_ask` and `helper_answer`
            // write a file each under the home and nothing else: an ask the driver takes under the
            // person's `helpers` rule, and a helper's answer, which reaches the asker as another agent's claim, never the person's.
            // `partner_answer` writes one file under the home, for the piece the driver started its
            // session for, and is refused to every other session: the partner's claim beside the files Daoris reads itself.
            // `channel_post` and `channel_read` speak and read for the session's own ask only, a
            // helper and a reviewer refused: a message is that agent's claim, reaches no run it does not already have, and asks
            // nothing of anyone, so work and requests stay quests. `handover_propose` writes one file
            // under the home: an ask to go on with another agent, its note the agent's claim, settled by the person's `handOver` rule.
            // `line_read` changes nothing: it reads the event files of the session's own line on this machine,
            // what its brief's bound left out, and the service refuses every other record. `handback_read` changes
            // nothing: it reads one quest's handback of the session's own workspace, as `quest_list` reads its quests. `question_ask`
            // writes one file under the home: the question its park waits on, the agent's words, which the person answers; it
            // decides nothing. The memory tools answer within the session's own workspace: `memory_search` and `memory_get`
            // change nothing, `memory_write` is held to the session's commit point, `memory_end` waits for its approver, and `memory_propose`
            // is a quest to an entry's owner; the service refuses each to a session whose work may not call it.
            "A session takes and closes its own quest, publishes what it finds for others and proposes a "
            + "change to these rules, through Daoris's connector — and anything it would have to ask for is "
            + "refused."),
        // 🔴 From a measured failure: in a folder the agent had
        // never trusted, a real driven session made its edit, is refused the commit — the repository's
        // own allow-list does not apply there — and declines. `cd` because the agent prefixes its commit
        // with one, and every part of a compound command must be allowed. `git mv`:
        // a rename stays in the tree, is taken back by another, and git refuses a path outside the
        // repository. A merge, a discard and a switch of branch stay out.
        new(
            "commit", RuleList.Allow,
            ["Bash(cd:*)", "Bash(git add:*)", "Bash(git commit:*)", "Bash(git mv:*)"],
            "A session commits its own work in its own tree, because committing is part of finishing a task "
            + "— its push is the workflow's step."),
        // 🔴 The harness matches a Bash rule against the command as written, so
        // `git push …` alone lets `git -C . push` and `git -c <key>=<value> push` through, and auto mode's
        // classifier allows a push to the working repository by default. The option-first rules take
        // every push with options before its subcommand, on both doors, whatever the mode. A deny's `*`
        // before the subcommand is the maker's own shape for that; only an ALLOW so written draws its
        // warning. The price, stated: a `git -C <dir> commit` whose message has "push" as a word before
        // another is refused too. What no rule can name — a quoted subcommand, an alias, a path to git, a
        // shell running it — is the classifier's, told below; on the pipe door nothing allows it anyway.
        // These three are limits the person turns on, never defaults an agent meets: a push is a workflow step (push, pull request), and
        // what an agent may do beyond it is its harness's own approval mode. Switched on, the git access Write still opens a push's plain forms.
        new(
            NoPushId, RuleList.Deny,
            ["Bash(git push)", "Bash(git push:*)", "Bash(git -* push)", "Bash(git -* push *)"],
            "Off until you switch it on: a push leaves this machine, and while this is on a session may not push unless git access's Write, "
            + "set in Settings → Git, lets it.",
            HardDeny: "Pushing to any remote in any form (for example git -C <dir> push, git -c <key>=<value> "
            + "push): this stays the person's.",
            OptIn: true),
        new(
            NoPublishId, RuleList.Deny,
            [],
            "Off until you switch it on: publishing a package or creating a release leaves this machine, and while this is on that stays your call.",
            HardDeny: "Publishing a package or creating a release: these stay the person's.",
            OptIn: true),
        new(
            TreeGuardId, RuleList.Deny,
            [],
            "Off until you switch it on: a session writes files only inside its own tree, and an edit, a write or a shell command's write "
            + "anywhere else is refused, through links as well. A change needed elsewhere is a quest.",
            Hook: TreeGuardHook.Matcher,
            OptIn: true),
        .. OwnHomeDefaults.Defaults,
    ];

    /// <summary>
    /// A tool name, then an optional parenthesised specifier on the same line — the harness's own shape, which the CLI's
    /// <c>SHAPE</c> and the service's <c>RuleProposalBox</c> twin. <c>\z</c>, since .NET's <c>$</c> also passes a final
    /// line break the CLI's refuses, and the screen's door hands the rule untrimmed.
    /// </summary>
    private static readonly Regex Shape = new(@"^[A-Za-z][A-Za-z0-9_-]*(\([^\r\n]+\))?\z", RegexOptions.CultureInvariant);

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Permissions);

    /// <summary>
    /// What the harness's auto-mode classifier is told as hard denials: the sentence
    /// of each default on that carries one — `no-push`'s and `no-publish`'s only where the person switched them on. The harness's own list is kept by Claude Code's rule language (<see cref="ClaudeRules.Render"/>), never here.
    /// </summary>
    public static IReadOnlyList<string> HardDeny(PermissionFile file) =>
    [
        .. Defaults
            .Where(shipped => shipped.HardDeny is not null && IsOn(file, shipped))
            .Select(shipped => shipped.HardDeny!),
    ];

    /// <summary>Whether a shipped default is on for this file: an opt-in one only where the person switched it on, any other unless switched off.</summary>
    public static bool IsOn(PermissionFile file, PermissionDefault shipped) => shipped.OptIn
        ? file.DefaultsOn.Contains(shipped.Id, StringComparer.Ordinal)
        : !file.DefaultsOff.Contains(shipped.Id, StringComparer.Ordinal);

    /// <summary>Whether sessions are handed the tree guard: only where the person switched it on.</summary>
    public static bool GuardsTree(PermissionFile file) => file.DefaultsOn.Contains(TreeGuardId, StringComparer.Ordinal);

    /// <summary>
    /// Whether a session handed <paramref name="rules"/> may call the connector's <paramref name="tool"/>
    /// without being asked: allowed by the tool's own rule, the server's, or its wildcard, and
    /// named by no ask or deny rule, since an asked permission is refused over either door.
    /// </summary>
    public static bool AllowsConnector(RuleLists rules, string tool)
    {
        string[] names = [ConnectorPrefix + tool, $"mcp__{KnowledgeConnector.ServerName}", ConnectorPrefix + "*"];
        bool Named(IReadOnlyList<string> list) => list.Any(rule => names.Contains(rule, StringComparer.Ordinal));
        return Named(rules.Allow) && !Named(rules.Ask) && !Named(rules.Deny);
    }

    /// <summary>
    /// A read of everything under <paramref name="directory"/>, in the harness's own absolute form:
    /// the folder a session's own quest or ask keeps its files in, which lies outside its tree.
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
    /// <see cref="ReadRule"/>: allowed in a declared write target, denied in every other checkout.
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
                if (key is not ("machine" or "workspaces" or "repositories" or "defaultsOff" or "defaultsOn"))
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
                DefaultsOn = Json.TrimmedStrings(root["defaultsOn"]),
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
    /// Written beside and renamed over, BOM-less and LF, keeping what a newer build wrote: the edit from what is read made onto the file as
    /// it stands (<see cref="JsonKeeping.Save"/>), so a key this build has no field for stays where it is at every level, a
    /// change another writer made since the read stays, and no edit writes nothing. Rules built in memory are written whole.
    /// </summary>
    /// <exception cref="DriverException">
    /// The file could not be read when <paramref name="file"/> is loaded. What it holds is the
    /// empty read, and writing it would drop every rule the file had. The CLI refuses the same edit. Or the file there now does not read,
    /// holds a key twice, or makes a scope this edit changes something other than an object.
    /// </exception>
    public static void Save(string home, PermissionFile file, bool holding = false)
    {
        if (file.Problem is not null)
        {
            throw new DriverException(
                $"{FileName} could not be read, so this edit was not written — it would have replaced every "
                + "rule in the file with this one change. Fix the file or remove it, then make the change again.");
        }

        // The home and the folder the rules are in (`data/`), which a new home has not made yet.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathOf(home)))!);
        if (file.Understood is { } understood && holding) JsonKeeping.SaveHolding(PathOf(home), file.Found, understood, Written(file));
        else if (file.Understood is { } kept) JsonKeeping.Save(PathOf(home), file.Found, kept, Written(file));
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
        if (file.DefaultsOn.Count > 0) root["defaultsOn"] = new JsonArray([.. file.DefaultsOn.Order(StringComparer.Ordinal).Select(id => (JsonNode)id)]);
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
        foreach (var shipped in Defaults.Where(d => IsOn(file, d)))
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
        // insensitive. Ordinal, a quest to `engine` would be handed none of `Engine`'s denies. Every
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
        }, others: lists => Drop(lists, rule));
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

        if (Defaults.First(d => d.Id == id).OptIn)
        {
            var onIds = file.DefaultsOn.Where(existing => existing != id).ToList();
            if (on) onIds.Add(id);
            return file with { DefaultsOn = onIds };
        }

        var off = file.DefaultsOff.Where(existing => existing != id).ToList();
        if (!on) off.Add(id);
        return file with { DefaultsOff = off };
    }

    private static PermissionFile Edit(
        PermissionFile file, RuleScope scope, string? name, Func<RuleLists, RuleLists> change, Func<RuleLists, RuleLists>? others = null)
    {
        others ??= change;
        if (scope == RuleScope.Machine) return file with { Machine = change(file.Machine) };

        if (name is not { } given || string.IsNullOrWhiteSpace(given))
        {
            throw new DriverException(
                $"a {(scope == RuleScope.Workspace ? "workspace" : "repository")} scope needs its name.");
        }

        var held = scope == RuleScope.Workspace ? file.Workspaces : file.Repositories;
        // The scope's existing spelling, when it has one in another case: one scope, never two.
        var spellings = held.Keys.Where(k => string.Equals(k.Trim(), given.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var key = spellings.FirstOrDefault() ?? given.Trim();
        var next = new Dictionary<string, RuleLists>(held, StringComparer.Ordinal)
        {
            [key] = change(held.GetValueOrDefault(key) ?? RuleLists.Empty),
        };
        if (next[key].IsEmpty) next.Remove(key);
        // A file that holds the name in more than one spelling composes them all, so the others lose what the edit took from the first.
        foreach (var other in spellings.Skip(1))
        {
            var rest = others(held[other]);
            if (rest.IsEmpty) next.Remove(other);
            else next[other] = rest;
        }

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
            if (string.IsNullOrWhiteSpace(key) || lists.IsEmpty) continue;
            scopes[key.Trim()] = scopes.TryGetValue(key.Trim(), out var held) ? held.Joined(lists) : lists;
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
