using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The rule standing for a question, and where it was set: <c>workspace</c>, <c>machine</c> or <c>default</c>.</summary>
public sealed record ResolvedBrainstorms(BrainstormRule Rule, string Source);

/// <summary>
/// One change to a brainstorms rule, as every door makes it: this machine's, or a <see cref="Workspace"/>'s; set what it names, or
/// clear. A repository named is kept, to be refused in the twin's words.
/// </summary>
public sealed record BrainstormEdit
{
    public string? Workspace { get; init; }
    public string? Repository { get; init; }
    public BrainstormSet? Set { get; init; }
    public bool Clear { get; init; }
}

/// <summary>
/// What a set changes, before it is judged: each part null where it is not named. A mode that is no word is the empty word, agents
/// that are no list are <c>[null]</c>, and minutes that are no object are <see cref="BrainstormRules.NotMinutes"/>.
/// </summary>
public sealed record BrainstormSet
{
    public string? Mode { get; init; }
    public IReadOnlyList<string?>? Agents { get; init; }
    public IReadOnlyDictionary<string, object?>? Minutes { get; init; }

    public bool Names => Mode is not null || Agents is not null || Minutes is not null;
}

/// <summary>
/// The brainstorm policy, <c>brainstorms</c> (this machine's rule) and <c>workspaceBrainstorms</c> in <c>driver.json</c>: read,
/// refused, resolved, said and edited, the one place every door asks (D176 point 9: a workspace's whole over the machine's).
/// Modelled on <see cref="HelperRules"/>.
/// </summary>
/// <remarks>
/// One table, this suite's <c>fixtures/brainstorm-rules.json</c>, cell for cell (<c>BrainstormRulesTests</c>); its CLI twin,
/// <c>brainstorms.ts</c>, left with the driver family's TypeScript (PUSH1f2b, D194 point 7). The look reads the agents and the minutes; the mode is read by an
/// agent's ask, which is BRAINSTORM1d's.
/// </remarks>
public static class BrainstormRules
{
    public const string Ask = BrainstormKey.Ask;
    public const string Off = BrainstormKey.Off;

    public static readonly IReadOnlyList<string> Modes = BrainstormKey.Modes;

    public const string FromWorkspace = "workspace";
    public const string FromMachine = "machine";
    public const string DefaultSource = "default";

    /// <summary>The most agents one brainstorm runs (D176 point 9: at most four makers).</summary>
    public const int MostAgents = BrainstormKey.MostAgents;

    /// <summary>The minutes' names, in the order they are kept.</summary>
    public static readonly IReadOnlyList<string> MinuteNames = BrainstormKey.MinuteNames;

    /// <summary>What a set's minutes are where they are no object: refused as minutes.</summary>
    public static readonly IReadOnlyDictionary<string, object?> NotMinutes = BrainstormKey.NotMinutes;

    internal const string Scope = "a brainstorms rule is set for this machine or for a workspace — never for one repository.";
    private const string OneAtATime = "one change at a time: set its mode, its agents or its minutes, or clear.";

    /// <summary>An entry read as a rule, with its first problem: its mode, then its agents, then its minutes (the settings' <see cref="BrainstormKey.Read"/>).</summary>
    public static (BrainstormRule? Rule, string? Problem) Read(JsonElement value) => BrainstormKey.Read(value);

    /// <summary>One rule as kept, whole: its mode, its agents in order, and its four minutes (the settings' <see cref="BrainstormKey.Write"/>).</summary>
    public static void Write(Utf8JsonWriter writer, BrainstormRule rule) => BrainstormKey.Write(writer, rule);

    /// <summary>
    /// The rule standing for a question (D176 point 9): its repository's workspace's whole, else this machine's, else the default.
    /// A question with no repository (<paramref name="workspace"/> null) takes this machine's. Names matched in any case.
    /// </summary>
    public static ResolvedBrainstorms Resolve(DriverConfig config, string? workspace) =>
        workspace?.Trim() is { Length: > 0 } named && config.WorkspaceBrainstorms.TryGetValue(named, out var shared)
            ? new ResolvedBrainstorms(shared, FromWorkspace)
            : config.Brainstorms is { } machine
                ? new ResolvedBrainstorms(machine, FromMachine)
                : new ResolvedBrainstorms(BrainstormRule.Default, DefaultSource);

    /// <summary>A rule as the list says it: its mode; its agents in order, or that each request names its own; its minutes.</summary>
    public static string Listed(BrainstormRule rule)
    {
        var text = new StringBuilder(rule.Mode).Append("; ");
        text.Append(rule.Agents.Count == 0 ? "the agents each request names" : string.Join(", then ", rule.Agents.Select(agent => $"`{agent}`")));
        var minutes = rule.Minutes;
        return text.Append(CultureInfo.InvariantCulture,
            $"; {minutes.Pass} minutes a pass, {minutes.Synthesis} the synthesis, {minutes.Challenge} the challenge, {minutes.Request} the request").ToString();
    }

    /// <summary>
    /// One change made to the config, or its refusal thrown in the twin's words, nothing changed — the CLI's
    /// <c>applyBrainstormEdit</c>. A set changes what it names over the rule set there, or over the default, the minutes one by
    /// one; a workspace's key keeps the spelling first written.
    /// </summary>
    /// <exception cref="DriverException">The edit's refusal, in the twin's words.</exception>
    public static DriverConfig Apply(DriverConfig config, BrainstormEdit edit)
    {
        if (edit.Repository?.Trim() is { Length: > 0 }) throw new DriverException(Scope);
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;

        var set = edit.Set is { Names: true } named ? named : null;
        if (new[] { set is not null, edit.Clear }.Count(change => change) != 1) throw new DriverException(OneAtATime);

        var key = workspace is null ? null : config.WorkspaceBrainstorms.Keys.FirstOrDefault(k => string.Equals(k, workspace, StringComparison.OrdinalIgnoreCase)) ?? workspace;
        var own = key is null ? config.Brainstorms : config.WorkspaceBrainstorms.TryGetValue(key, out var standing) ? standing : null;

        BrainstormRule? next = null;
        if (!edit.Clear)
        {
            var judged = BrainstormKey.Judge(
                new BrainstormKey.Spelled(set!.Mode ?? own?.Mode, set.Agents ?? (own is null ? null : [.. own.Agents]), set.Minutes),
                own?.Minutes);
            next = judged.Rule ?? throw new DriverException(judged.Problem!);
        }

        if (key is null) return config with { Brainstorms = next };
        var changed = new Dictionary<string, BrainstormRule>(config.WorkspaceBrainstorms, StringComparer.OrdinalIgnoreCase);
        if (next is null) changed.Remove(key);
        else changed[key] = next;
        return config with { WorkspaceBrainstorms = changed };
    }

    /// <summary>
    /// An edit as the shared table spells it: <c>workspace</c> or none for this machine, then <c>set</c> or <c>clear</c>. A mode
    /// that is not a string is kept to be refused in the twin's words, and so are agents that are no list and minutes that are no
    /// object.
    /// </summary>
    public static BrainstormEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new BrainstormEdit();

        BrainstormSet? set = null;
        if (root.TryGetProperty("set", out var given) && given.ValueKind == JsonValueKind.Object)
        {
            set = new BrainstormSet
            {
                // A mode that is no string is refused as one that is no mode: the empty word.
                Mode = given.TryGetProperty("mode", out var mode) ? mode.ValueKind == JsonValueKind.String ? mode.GetString() : "" : null,
                Agents = given.TryGetProperty("agents", out var agents) ? agents.ValueKind == JsonValueKind.Array ? BrainstormKey.Texts(agents) : [null] : null,
                Minutes = given.TryGetProperty("minutes", out var minutes) ? BrainstormKey.MinutesOf(minutes) : null,
            };
        }

        return new BrainstormEdit
        {
            Workspace = Json.Text(root, "workspace"),
            Repository = Json.Text(root, "repository"),
            Set = set,
            Clear = root.TryGetProperty("clear", out var clear) && clear.ValueKind == JsonValueKind.True,
        };
    }

    /// <summary>A rule as compact JSON, for a test to compare with the shared table's cells.</summary>
    public static string ToJson(BrainstormRule rule)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, rule);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
