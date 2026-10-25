using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>Where a repository's commit rule was set: <see cref="CommitRules.DefaultSource"/> where nothing was.</summary>
public sealed record ResolvedCommits(CommitRule Rule, string Source);

/// <summary>What a set changes, before it is judged: a field not given is null with its flag off; a convention that is no string is kept as <c>false</c>, to be refused.</summary>
public sealed record CommitSet
{
    public string? Mode { get; init; }
    public bool ModeGiven { get; init; }
    public object? Convention { get; init; }
    public bool ConventionGiven { get; init; }

    public bool Names => ModeGiven || ConventionGiven;
}

/// <summary>One change to a commit rule, as every door makes it: for a repository or a workspace, set what it names, or clear.</summary>
public sealed record CommitEdit
{
    public string? Repository { get; init; }
    public string? Workspace { get; init; }
    public CommitSet? Set { get; init; }
    public bool Clear { get; init; }
}

/// <summary>
/// The commit rule, <c>commits</c> and <c>workspaceCommits</c> in <c>driver.json</c>: read, refused, resolved, said and edited —
/// the one place every door asks. Modelled on <see cref="HelperRules"/>; a rule is a mode and an optional convention.
/// </summary>
/// <remarks>
/// One table, this suite's <c>fixtures/commit-rules.json</c>, cell for cell (<c>CommitRulesTests</c>); its CLI twin, <c>commits.ts</c>,
/// left with the driver family's TypeScript (PUSH1f2b, D194 point 7). Nothing here commits or tells a session: a driven session in a tree
/// of its own is told the rule <see cref="Resolve"/> answers by <see cref="ProcessWords"/> (PROCESS1d-i); a conversation's
/// briefing is PROCESS1d-ii's.
/// </remarks>
public static class CommitRules
{
    public const string AsYouGo = CommitKey.AsYouGo;
    public const string Ask = CommitKey.Ask;
    public const string AtEnd = CommitKey.AtEnd;

    public static readonly IReadOnlyList<string> Modes = CommitKey.Modes;

    /// <summary>The longest convention kept, in characters.</summary>
    public const int ConventionLimit = CommitKey.ConventionLimit;

    public const string DefaultSource = "default";

    internal const string Scope = "a commit rule is set for a repository or a workspace — name exactly one.";
    private const string OneAtATime = "one change at a time: set its mode or its convention, or clear.";

    /// <summary>An entry read as a rule, with its first problem: an object whose mode is one of the three (absent is `as-you-go`) and whose convention is one short paragraph (the settings' <see cref="CommitKey.Read"/>).</summary>
    public static (CommitRule? Rule, string? Problem) Read(JsonElement value) => CommitKey.Read(value);

    /// <summary>A map of commit rules as <c>driver.json</c> holds it (the settings' <see cref="CommitKey.Map"/>).</summary>
    internal static IReadOnlyDictionary<string, CommitRule> Map(JsonElement root, string name) => CommitKey.Map(root, name);

    /// <summary>A map written as <c>driver.json</c> keeps it (the settings' <see cref="CommitKey.WriteMap"/>).</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, CommitRule> map) => CommitKey.WriteMap(writer, name, map);

    /// <summary>One rule as kept (the settings' <see cref="CommitKey.Write"/>).</summary>
    public static void Write(Utf8JsonWriter writer, CommitRule rule) => CommitKey.Write(writer, rule);

    /// <summary>
    /// The rule standing for a repository: its own, else its workspace's (one in no workspace being in <c>default</c>), else
    /// <see cref="CommitRule.Default"/> from <see cref="DefaultSource"/> — names matched in any case, as every door matches them.
    /// </summary>
    public static ResolvedCommits Resolve(DriverConfig config, string repository, string? workspace) =>
        config.Commits.TryGetValue(repository.Trim(), out var own)
            ? new ResolvedCommits(own, OpinionSource.Repository)
            : config.WorkspaceCommits.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)
                ? new ResolvedCommits(shared, OpinionSource.Workspace)
                : new ResolvedCommits(CommitRule.Default, DefaultSource);

    /// <summary>A rule as the list says it: its mode, then its convention where it has one.</summary>
    public static string Listed(CommitRule rule) => rule.Convention is null ? rule.Mode : $"{rule.Mode}: {rule.Convention}";

    /// <summary>
    /// What the terminal and Ask Daoris's card say of a mode as it is set (TWINWORDS1, <c>landing-says.json</c>'s <c>commits</c>): one
    /// function, <c>daoris-driver driver commits</c>' and the card's.
    /// </summary>
    public static string Says(string mode) => mode switch
    {
        Ask => "Each commit waits for your yes before it is made.",
        AtEnd => "One commit is made when the work is done.",
        _ => "Each piece that passes its checks is committed as the work goes.",
    };

    /// <summary>
    /// One change made to the config, or its refusal thrown in the twin's words, nothing changed — the CLI's
    /// <c>applyCommitEdit</c>. A set changes what it names over the rule set there, or over the default; the key keeps the
    /// spelling first written.
    /// </summary>
    /// <exception cref="DriverException">The edit's refusal, in the twin's words.</exception>
    public static DriverConfig Apply(DriverConfig config, CommitEdit edit)
    {
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if ((repository is null) == (workspace is null)) throw new DriverException(Scope);

        var set = edit.Set is { Names: true } named ? named : null;
        if (new[] { set is not null, edit.Clear }.Count(change => change) != 1) throw new DriverException(OneAtATime);

        var map = repository is not null ? config.Commits : config.WorkspaceCommits;
        var scope = (repository ?? workspace)!;
        var key = map.Keys.FirstOrDefault(k => string.Equals(k, scope, StringComparison.OrdinalIgnoreCase)) ?? scope;
        var own = map.TryGetValue(key, out var standing) ? standing : null;

        CommitRule? next = null;
        if (!edit.Clear)
        {
            var judged = CommitKey.Judge(new CommitKey.Spelled(
                set!.ModeGiven ? (object?)set.Mode ?? false : own?.Mode,
                set.ConventionGiven ? set.Convention ?? false : own?.Convention));
            next = judged.Rule ?? throw new DriverException(judged.Problem!);
        }

        var changed = new Dictionary<string, CommitRule>(map, StringComparer.OrdinalIgnoreCase);
        if (next is null) changed.Remove(key);
        else changed[key] = next;
        return repository is not null ? config with { Commits = changed } : config with { WorkspaceCommits = changed };
    }

    /// <summary>
    /// An edit as the screen's route and the shared table spell it: <c>repository</c> or <c>workspace</c>, then <c>set</c> or
    /// <c>clear</c>. A mode or a convention that is not a string is kept to be refused in the twin's words.
    /// </summary>
    public static CommitEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new CommitEdit();

        CommitSet? set = null;
        if (root.TryGetProperty("set", out var given) && given.ValueKind == JsonValueKind.Object)
        {
            var modeGiven = given.TryGetProperty("mode", out var mode);
            var conventionGiven = given.TryGetProperty("convention", out var convention);
            set = new CommitSet
            {
                ModeGiven = modeGiven,
                // A mode that is no string is refused as one that is no mode: the empty word.
                Mode = modeGiven ? mode.ValueKind == JsonValueKind.String ? mode.GetString() : "" : null,
                ConventionGiven = conventionGiven,
                Convention = conventionGiven ? convention.ValueKind == JsonValueKind.String ? convention.GetString() : false : null,
            };
        }

        return new CommitEdit
        {
            Repository = Text(root, "repository"),
            Workspace = Text(root, "workspace"),
            Set = set,
            Clear = root.TryGetProperty("clear", out var clear) && clear.ValueKind == JsonValueKind.True,
        };
    }

    /// <summary>A rule as compact JSON, for a test to compare with the shared table's cells.</summary>
    public static string ToJson(CommitRule rule)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, rule);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
