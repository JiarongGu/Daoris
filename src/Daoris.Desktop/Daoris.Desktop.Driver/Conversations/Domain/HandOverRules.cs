using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What an agent's ask to hand its session over to another agent does (MIXAGENT1c, D163 point 3; the mixed agents design §5):
/// <c>ask</c> (the default) waits for the person's yes or no, <c>on</c> takes it when the agent's turn ends, to the next agent in
/// the person's order, <c>off</c> refuses it. Daoris's own offer when an account is near or cut is not this rule's: an agent's ask
/// is judged by the rule alone (D191's settling of the design).
/// </summary>
/// <remarks>An object of one field, so a later field is added beside the mode, never in its place.</remarks>
public sealed record HandOverRule(string Mode)
{
    /// <summary>What stands where nothing is set: an agent's ask waits for the person's yes.</summary>
    public static HandOverRule Default { get; } = new(HandOverRules.Ask);
}

/// <summary>Where a repository's hand-over rule was set: <see cref="HandOverRules.DefaultSource"/> where nothing was.</summary>
public sealed record ResolvedHandOver(HandOverRule Rule, string Source);

/// <summary>What a set changes, before it is judged: the mode, kept as said to be refused in the twin's words.</summary>
public sealed record HandOverSet
{
    public string? Mode { get; init; }
    public bool ModeGiven { get; init; }
}

/// <summary>One change to a hand-over rule, as every door makes it: for a repository or a workspace, set its mode, or clear.</summary>
public sealed record HandOverEdit
{
    public string? Repository { get; init; }
    public string? Workspace { get; init; }
    public HandOverSet? Set { get; init; }
    public bool Clear { get; init; }
}

/// <summary>
/// The hand-over rule, <c>handOver</c> and <c>workspaceHandOver</c> in <c>driver.json</c>: read, refused, resolved and edited, the
/// one place every door asks. Modelled on <see cref="HelperRules"/>, simpler: a rule is its mode.
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>handover.ts</c>: both hold ONE table, this suite's <c>fixtures/hand-over-rules.json</c>, cell for cell
/// (<c>HandOverRulesTests</c> and <c>driverconfig.test.ts</c>). The look's judge of an agent's ask reads it (<c>HandOverAskJudge</c>).
/// </remarks>
public static class HandOverRules
{
    public const string Ask = "ask";
    public const string On = "on";
    public const string Off = "off";

    public static readonly IReadOnlyList<string> Modes = [Ask, On, Off];

    public const string DefaultSource = "default";

    private const string RuleShape = "a hand-over rule is an object with its `mode`.";
    private const string ModeShape =
        "`mode` is `ask`, `on` or `off`: whether an agent's ask to hand its session over waits for your yes, is taken when its turn ends, or is refused.";
    internal const string Scope = "a hand-over rule is set for a repository or a workspace — name exactly one.";
    private const string OneAtATime = "one change at a time: set its mode, or clear.";

    /// <summary>An entry read as a rule, with its first problem: an object whose mode is one of the three (absent or null is `ask`).</summary>
    public static (HandOverRule? Rule, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return (null, RuleShape);
        object? mode = null;
        if (value.TryGetProperty("mode", out var written) && written.ValueKind != JsonValueKind.Null)
        {
            mode = written.ValueKind == JsonValueKind.String ? written.GetString() : false;
        }

        return Judge(mode);
    }

    private static (HandOverRule? Rule, string? Problem) Judge(object? mode) =>
        mode is not null && (mode is not string text || !Modes.Contains(text))
            ? (null, ModeShape)
            : (new HandOverRule((string?)mode ?? Ask), null);

    /// <summary>A map of hand-over rules as <c>driver.json</c> holds it: each name trimmed, a blank one and a rule with a problem not read, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, HandOverRule> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, HandOverRule>(StringComparer.OrdinalIgnoreCase);
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
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, HandOverRule> map)
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

    /// <summary>One rule as kept: its mode.</summary>
    public static void Write(Utf8JsonWriter writer, HandOverRule rule)
    {
        writer.WriteStartObject();
        writer.WriteString("mode", rule.Mode);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The rule standing for a repository: its own, else its workspace's (one in no workspace being in <c>default</c>), else
    /// <see cref="HandOverRule.Default"/> from <see cref="DefaultSource"/> — names matched in any case, as every door matches them.
    /// </summary>
    public static ResolvedHandOver Resolve(DriverConfig config, string repository, string? workspace) =>
        config.HandOver.TryGetValue(repository.Trim(), out var own)
            ? new ResolvedHandOver(own, OpinionSource.Repository)
            : config.WorkspaceHandOver.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)
                ? new ResolvedHandOver(shared, OpinionSource.Workspace)
                : new ResolvedHandOver(HandOverRule.Default, DefaultSource);

    /// <summary>
    /// One change made to the config, or its refusal thrown in the twin's words, nothing changed — the CLI's
    /// <c>applyHandOverEdit</c>. The key keeps the spelling first written.
    /// </summary>
    /// <exception cref="DriverException">The edit's refusal, in the twin's words.</exception>
    public static DriverConfig Apply(DriverConfig config, HandOverEdit edit)
    {
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if ((repository is null) == (workspace is null)) throw new DriverException(Scope);

        var set = edit.Set is { ModeGiven: true } named ? named : null;
        if (new[] { set is not null, edit.Clear }.Count(change => change) != 1) throw new DriverException(OneAtATime);

        var map = repository is not null ? config.HandOver : config.WorkspaceHandOver;
        var scope = (repository ?? workspace)!;
        var key = map.Keys.FirstOrDefault(k => string.Equals(k, scope, StringComparison.OrdinalIgnoreCase)) ?? scope;

        HandOverRule? next = null;
        if (!edit.Clear)
        {
            // A mode given as anything but a word is no mode, refused in the twin's words.
            var judged = Judge(set!.Mode ?? "");
            next = judged.Rule ?? throw new DriverException(judged.Problem!);
        }

        var changed = new Dictionary<string, HandOverRule>(map, StringComparer.OrdinalIgnoreCase);
        if (next is null) changed.Remove(key);
        else changed[key] = next;
        return repository is not null ? config with { HandOver = changed } : config with { WorkspaceHandOver = changed };
    }

    /// <summary>
    /// An edit as the screen's route and the shared table spell it: <c>repository</c> or <c>workspace</c>, then <c>set</c> with its
    /// <c>mode</c>, or <c>clear</c>. A mode that is not a string is kept to be refused in the twin's words.
    /// </summary>
    public static HandOverEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new HandOverEdit();

        HandOverSet? set = null;
        if (root.TryGetProperty("set", out var given) && given.ValueKind == JsonValueKind.Object && given.TryGetProperty("mode", out var mode))
        {
            set = new HandOverSet { ModeGiven = true, Mode = mode.ValueKind == JsonValueKind.String ? mode.GetString() : "" };
        }

        return new HandOverEdit
        {
            Repository = Json.Text(root, "repository"),
            Workspace = Json.Text(root, "workspace"),
            Set = set,
            Clear = root.TryGetProperty("clear", out var clear) && clear.ValueKind == JsonValueKind.True,
        };
    }

    /// <summary>A rule as compact JSON, for a test to compare with the shared table's cells.</summary>
    public static string ToJson(HandOverRule rule)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, rule);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>What the terminal and the page say of a mode as it is set: <c>handOverModeSays</c>, the CLI's words.</summary>
    public static string Says(string mode) => mode switch
    {
        Ask => "An agent's ask to hand its session over waits for your yes or no on the session's page.",
        On => "An agent's ask to hand its session over is taken when its turn ends: it goes on with the next agent in your order.",
        _ => "An agent's ask to hand its session over is refused: only your own press hands one over.",
    };
}
