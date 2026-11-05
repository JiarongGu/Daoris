using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's rule language (the agent-provider design §4.5): one session's composed rules as its own settings file under the home,
/// beside the servers file the same session is handed, with its hooks and the hard denials its classifier is told, and gone when the
/// session is. Its native door carries the file by <c>--settings</c>, its protocol door in <c>_meta.claudeCode.options.settings</c>.
/// </summary>
/// <remarks>
/// <b>Its own context</b> (CONTEXTONE1e, D230 point 3): every file also says <see cref="OwnContext"/>, so a session's skills are its
/// tree's and its per-session plugin copy's (<see cref="ClaudeSkills"/>), its connectors the ones it is handed, and its memory Daoris's.
/// Claude Code 2.1.314 reads each key from the command-line tier for that invocation alone, and moves nothing in the account's home; the
/// account's home is never written (<see cref="HarnessSettings.ProfileHome"/>).
/// </remarks>
public sealed class ClaudeRules : IRuleLanguage
{
    /// <summary>The one language Claude Code's doors take.</summary>
    public static ClaudeRules Language { get; } = new();

    /// <summary>The end of the file a session's rules are handed in, in the spawn folder.</summary>
    public const string FileSuffix = ".settings.json";

    /// <summary>The harness's own hard denials, kept by naming them in the list (UNBLOCK4).</summary>
    public const string HarnessDefaults = TraceSpawnFiles.HarnessDefaults;

    /// <summary>
    /// The keys that keep a session's context its own: no auto-memory, and none of the skills, plugins or connectors the vendor syncs to
    /// the account it runs as.
    /// </summary>
    public static IReadOnlyList<(string Key, bool Value)> OwnContext { get; } =
    [
        ("autoMemoryEnabled", false),
        ("syncClaudeAiSkills", false),
        ("syncClaudeAiPlugins", false),
        ("disableClaudeAiConnectors", true),
    ];

    /// <inheritdoc/>
    /// <remarks>The lineage hook is Claude Code's own (<see cref="ClaudeLineageHook"/>), installed here for the file it is handed.</remarks>
    public string Write(string home, string sessionId, HandedRules rules) =>
        Render(
            home, sessionId, rules.Rules, rules.HardDeny, rules.Guard,
            rules.Lineage is { } file ? ClaudeLineageHook.For(home, sessionId, file) : null);

    /// <summary>Write the file for one session, which every session is handed for its own context at least.</summary>
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
    public static string Render(
        string home, string sessionId, RuleLists rules, IReadOnlyList<string> hardDeny, TreeGuardHook? guard = null, LineageHook? lineage = null)
    {
        var told = hardDeny.Where(entry => entry != HarnessDefaults).Distinct(StringComparer.Ordinal).ToList();

        var folder = DaorisHome.Entry(home, HomeEntry.Spawn);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{sessionId}{FileSuffix}");

        var document = new JsonObject
        {
            ["permissions"] = new JsonObject
            {
                ["allow"] = new JsonArray([.. rules.Allow.Select(rule => (JsonNode)rule)]),
                ["ask"] = new JsonArray([.. rules.Ask.Select(rule => (JsonNode)rule)]),
                ["deny"] = new JsonArray([.. rules.Deny.Select(rule => (JsonNode)rule)]),
            },
        };
        foreach (var (key, value) in OwnContext) document[key] = value;

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
                    ["matcher"] = TreeGuardHook.Matcher,
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
                before.Add(new JsonObject { ["matcher"] = LineageHook.Matcher, ["hooks"] = Lineage() });
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
}
