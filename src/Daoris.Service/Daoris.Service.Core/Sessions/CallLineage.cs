using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// The lineage one MCP call carried in its <c>_meta</c>, as its harness wrote it (FABRIC-C1a, D173; the C0 audit §2).
/// </summary>
/// <remarks>
/// <para><b>Why <c>_meta</c>, and never the environment or the arguments.</b> A native child calls with its parent's
/// <c>DAORIS_SESSION_ID</c> on both harnesses: Claude Code's subagent through the parent's very connection, Codex's thread
/// through a process started from the parent's entry. What tells calls apart is what the harness writes on each one. The
/// arguments are the model's, so nothing there is read.</para>
///
/// <para><b>What each harness writes</b> is its own file's (<see cref="ICallKeys"/>), read in <see cref="Readers"/>' order: the
/// first key a call carries is the one read, so a call carrying both is read as the first agent's. A harness that writes none
/// has no lineage here, which is said as none, never guessed (canary 9).</para>
///
/// <para>🔴 <b>Ids only.</b> A value that is not shaped as an id (a path, a blank, a sentence) is not kept (D46, D47), only named
/// in <see cref="Malformed"/>.</para>
/// </remarks>
/// <param name="Harness">The agent whose key the call carried, one of <see cref="Readers"/>' <see cref="ICallKeys.Harness"/>.</param>
public sealed record CallLineage(string Harness)
{
    /// <summary>Claude Code, by the name the driver gives its harness.</summary>
    public const string Claude = ClaudeCallKeys.Agent;

    /// <summary>Codex.</summary>
    public const string Codex = CodexCallKeys.Agent;

    /// <summary>
    /// The service's one ordered list of call keys, one agent's each, held by the twin fixture the driver's census reads
    /// (<c>provider-census.json</c>'s <c>service.callKeys</c>).
    /// </summary>
    public static IReadOnlyList<ICallKeys> Readers { get; } = [new ClaudeCallKeys(), new CodexCallKeys()];

    /// <summary>
    /// What an id is here: a letter or digit, then letters, digits and <c>_ . : -</c>, at most 128 in all. A harness's ids are
    /// (<c>toolu_…</c>, a UUID, a hex agent id); a path, which has a separator, never is.
    /// </summary>
    private static readonly Regex IdShape = new("^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$", RegexOptions.CultureInvariant);

    /// <summary>Claude Code's id of the call.</summary>
    public string? ToolUse { get; init; }

    /// <summary>Codex's thread that made the call.</summary>
    public string? Thread { get; init; }

    /// <summary>Codex's thread that spawned it; none for the root's.</summary>
    public string? Parent { get; init; }

    /// <summary>Codex's spawn kind (<c>thread_spawn</c>).</summary>
    public string? Kind { get; init; }

    /// <summary>Codex's thread source (<c>user</c> for the root's, <c>subagent</c> for a child's).</summary>
    public string? Source { get; init; }

    /// <summary>Codex's root session.</summary>
    public string? Root { get; init; }

    /// <summary>
    /// The fields the harness sent that are not ids, comma-joined, or null. Kept as malformed, never dropped to look absent: an
    /// absent parent is what a root has (FABRIC-C2a, D173's note, point 1).
    /// </summary>
    public string? Malformed { get; init; }

    /// <summary>The lineage a call's <c>_meta</c> carries under the first of <see cref="Readers"/>' keys it sent, or null for none.</summary>
    public static CallLineage? Read(JsonObject? meta)
    {
        if (meta is null) return null;
        foreach (var reader in Readers)
        {
            if (meta[reader.Key] is { } value && value.GetValueKind() != JsonValueKind.Null) return reader.Read(value);
        }

        return null;
    }

    /// <summary>A value kept only where it is a string shaped as an id.</summary>
    public static string? Id(JsonNode? value) =>
        value is JsonValue text && text.TryGetValue<string>(out var said) ? Id(said) : null;

    /// <summary>A string kept only where it is shaped as an id.</summary>
    public static string? Id(string? value) => value is not null && IdShape.IsMatch(value) ? value : null;
}
