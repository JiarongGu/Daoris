using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Which checks a development pipeline's sessions run (FABRIC-PIPELINE1b, D192 point 4; the pipeline design §3.1): <c>light</c>, the gates
/// the repository does not mark heavy and the checks a change reaches, filtered, the heavy gates left to the verification and refused to
/// the session; <c>heavy-at-end</c>, the heavy gates once, at the session's end, through <c>run-heavy</c>; absent, today's. Under Current
/// it is a repository's rule, <c>sessionChecks</c> in <c>driver.json</c> (D157 point 4: Current draws the rules), read, edited and written
/// here, the one place the terminal's <c>checks</c>, Setup's row, a driven start and <c>run-heavy</c> ask; the CLI's
/// <c>driverconfig.ts</c> reads and keeps it the same way (<c>fixtures/session-checks.json</c>).
/// </summary>
/// <remarks>
/// <para><b>Why a rule and not a sentence.</b> Driven sessions on this repository ran its whole test project inside their session until
/// their timeout and committed nothing, held back only by a standing answer's words (D192's case). A policy the start composes its words
/// from, which <c>run-heavy</c> and the harness refuse by, is the process's, not any agent's memory (D161).</para>
///
/// <para><b>A named workflow's run</b> takes its bound version's <c>checks</c> (<c>claimed</c>'s option in a graph version, the
/// <c>work</c> step's field in a steps version), never this rule, which is Current's. No version carries one in this build: a graph
/// version's option is read at the bind once the bind comes before the take (FABRIC-CUTOVER1g), and a steps version's <c>work</c> takes no
/// field yet, so a named workflow's sessions run as today (<see cref="Of"/>).</para>
///
/// <para><b>Read as a stored shape</b>: a value this build does not know is passed over, read as no rule, and kept in the file by the
/// keeping writer. Keyed as <c>heavyGates</c> is: the repository's name, trimmed, matched in any case, a key keeping the spelling first
/// written.</para>
/// </remarks>
public static class SessionChecks
{
    /// <summary>The file's key.</summary>
    public const string Key = "sessionChecks";

    /// <summary>The gates the repository does not mark heavy and the checks a change reaches; the heavy gates are the verification's.</summary>
    public const string Light = "light";

    /// <summary>The checks a change reaches while it works, then the heavy gates once, at the session's end.</summary>
    public const string HeavyAtEnd = "heavy-at-end";

    /// <summary>The choices, in the order a door offers them. The CLI's <c>SESSION_CHECKS</c> is a deliberate copy.</summary>
    public static IReadOnlyList<string> Choices { get; } = [Light, HeavyAtEnd];

    private const string Scope = "a session's checks are set for a repository: name one.";

    /// <summary>A repository's rule, its name trimmed and matched in any case; null where it sets none, which is today's.</summary>
    public static string? For(DriverConfig config, string repository) =>
        config.SessionChecks.TryGetValue(repository.Trim(), out var checks) ? checks : null;

    /// <summary>
    /// The checks a run takes (the pipeline design §3.1): under Current, or where no process was read, the repository's rule; a named
    /// workflow's, its bound version's, which none carries in this build, so today's.
    /// </summary>
    public static string? Of(DriverConfig config, string repository, WorkflowProcess? process) =>
        process is { Named: true } ? null : For(config, repository);

    /// <summary>
    /// One repository's rule set, a choice read in any case and kept in its own spelling, or taken away with null: the config changed and
    /// nothing else.
    /// </summary>
    /// <exception cref="DriverException">No repository, or a value that is no choice, in the words every door says.</exception>
    public static DriverConfig Apply(DriverConfig config, string? repository, string? checks)
    {
        var named = repository?.Trim() is { Length: > 0 } r ? r : throw new DriverException(Scope);
        var key = config.SessionChecks.Keys.FirstOrDefault(k => string.Equals(k, named, StringComparison.OrdinalIgnoreCase)) ?? named;
        var changed = new Dictionary<string, string>(config.SessionChecks, StringComparer.OrdinalIgnoreCase);
        if (checks is null)
        {
            changed.Remove(key);
        }
        else
        {
            changed[key] = Choice(checks) ?? throw new DriverException(
                $"`{checks}` is not a session's checks: they are `{Light}` (the light gates and the checks a change reaches, the heavy gates "
                + $"left to the verification) or `{HeavyAtEnd}` (the heavy gates once, at a session's end); none is today's.");
        }

        return config with { SessionChecks = changed };
    }

    /// <summary>A choice as kept, trimmed and matched in any case, in its own spelling; null for anything else.</summary>
    public static string? Choice(string? said) =>
        said?.Trim() is { } trimmed ? Choices.FirstOrDefault(choice => string.Equals(choice, trimmed, StringComparison.OrdinalIgnoreCase)) : null;

    /// <summary>
    /// The map as <c>driver.json</c> holds it: each repository's name trimmed, a blank one and a value no choice passed over, a name written
    /// twice in any case read where first readable; none where the key is absent or no object.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Map(JsonElement root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(Key, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || map.ContainsKey(named) || property.Value.ValueKind != JsonValueKind.String) continue;
            if (Choice(property.Value.GetString()) is { } choice) map[named] = choice;
        }

        return map;
    }

    /// <summary>The map written as <c>driver.json</c> keeps it, only when set, repositories in ordinal order.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(Key);
        foreach (var (repository, checks) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(repository, checks);
        writer.WriteEndObject();
    }
}
