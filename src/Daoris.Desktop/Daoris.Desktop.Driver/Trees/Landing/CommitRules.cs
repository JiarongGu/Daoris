using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// How work is committed in a repository (PROCESS1a, D164's amendment point 1): <c>as-you-go</c> (the default) commits each piece
/// that passes its checks, <c>ask</c> waits for the person's yes before each commit, <c>at-end</c> makes one commit when the work
/// is done. <see cref="Convention"/> is the person's words on how a commit is written: one paragraph, trimmed, none where blank.
/// </summary>
public sealed record CommitRule(string Mode, string? Convention)
{
    /// <summary>What stands where nothing is set: commits as the work goes, with no convention named.</summary>
    public static CommitRule Default { get; } = new(CommitRules.AsYouGo, null);
}

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
/// A TWIN with the CLI's <c>commits.ts</c>: both hold ONE table, this suite's <c>fixtures/commit-rules.json</c>, cell for cell
/// (<c>CommitRulesTests</c> and <c>driverconfig.test.ts</c>). Nothing here commits or tells a session: a driven session in a tree
/// of its own is told the rule <see cref="Resolve"/> answers by <see cref="ProcessWords"/> (PROCESS1d-i); a conversation's
/// briefing is PROCESS1d-ii's.
/// </remarks>
public static class CommitRules
{
    public const string AsYouGo = "as-you-go";
    public const string Ask = "ask";
    public const string AtEnd = "at-end";

    public static readonly IReadOnlyList<string> Modes = [AsYouGo, Ask, AtEnd];

    /// <summary>The longest convention kept, in characters.</summary>
    public const int ConventionLimit = 400;

    public const string DefaultSource = "default";

    private const string RuleShape = "a commit rule is an object with its `mode` and an optional `convention`.";
    private const string ModeShape = "`mode` is `as-you-go`, `ask` or `at-end`: whether commits are made as the work goes, wait for your yes first, or are made once at the end.";
    private const string ConventionShape = "`convention` is your words on how a commit is written: one paragraph with no line break, at most 400 characters.";
    internal const string Scope = "a commit rule is set for a repository or a workspace — name exactly one.";
    private const string OneAtATime = "one change at a time: set its mode or its convention, or clear.";

    /// <summary>A rule as an entry or a door spells it, before it is judged: a null mode or convention is absent.</summary>
    private sealed record Spelled(object? Mode, object? Convention);

    /// <summary>An entry read as a rule, with its first problem: an object whose mode is one of the three (absent is `as-you-go`) and whose convention is one short paragraph.</summary>
    public static (CommitRule? Rule, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return (null, RuleShape);
        return Judge(new Spelled(Spell(value, "mode"), Spell(value, "convention")));
    }

    private static object? Spell(JsonElement value, string name) =>
        !value.TryGetProperty(name, out var written) || written.ValueKind == JsonValueKind.Null
            ? null
            : written.ValueKind == JsonValueKind.String ? written.GetString() : false;

    private static (CommitRule? Rule, string? Problem) Judge(Spelled spelled)
    {
        if (spelled.Mode is not null && (spelled.Mode is not string text || !Modes.Contains(text))) return (null, ModeShape);
        string? convention = null;
        if (spelled.Convention is not null)
        {
            if (spelled.Convention is not string words) return (null, ConventionShape);
            convention = words.Trim();
            if (convention.Length > ConventionLimit || convention.Contains('\n') || convention.Contains('\r')) return (null, ConventionShape);
            if (convention.Length == 0) convention = null;
        }

        return (new CommitRule((string?)spelled.Mode ?? AsYouGo, convention), null);
    }

    /// <summary>A map of commit rules as <c>driver.json</c> holds it: each name trimmed, a blank one and a rule with a problem not read, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, CommitRule> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, CommitRule>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || Read(property.Value) is not ({ } rule, null) || map.ContainsKey(named)) continue;
            map[named] = rule;
        }

        return map;
    }

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, CommitRule> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, rule) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            Write(writer, rule);
        }

        writer.WriteEndObject();
    }

    /// <summary>One rule as kept: its mode, then its convention where it has one.</summary>
    public static void Write(Utf8JsonWriter writer, CommitRule rule)
    {
        writer.WriteStartObject();
        writer.WriteString("mode", rule.Mode);
        if (rule.Convention is not null) writer.WriteString("convention", rule.Convention);
        writer.WriteEndObject();
    }

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
            var judged = Judge(new Spelled(
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
