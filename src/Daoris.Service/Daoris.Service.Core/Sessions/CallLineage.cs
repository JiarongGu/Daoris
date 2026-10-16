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
/// <para><b>What each harness writes.</b> Claude Code names the call, <c>claudecode/toolUseId</c>, which its
/// <c>PreToolUse</c> hook names beside the <c>agent_id</c> that made it: the join is the driver's (<c>ChildLineage</c>).
/// Codex names the thread, its parent, the spawn kind and the root's session in <c>x-codex-turn-metadata</c>, as JSON text
/// or an object. Measured on Claude Code 2.1.299 and Codex 0.160.0; a harness that writes neither has no lineage here, which
/// is said as none, never guessed (canary 9).</para>
///
/// <para>🔴 <b>Ids only.</b> Codex's metadata also names the workspace's absolute path and commit: neither is read, and a value
/// that is not shaped as an id (a path, a blank, a sentence) is not kept (D46, D47).</para>
/// </remarks>
/// <param name="Harness"><see cref="Claude"/> or <see cref="Codex"/>.</param>
public sealed record CallLineage(string Harness)
{
    /// <summary>Claude Code, by the name the driver gives its harness.</summary>
    public const string Claude = "claude-code";

    /// <summary>Codex.</summary>
    public const string Codex = "codex";

    /// <summary>Claude Code's id of the call, the hook's <c>tool_use_id</c>.</summary>
    public const string ClaudeToolUse = "claudecode/toolUseId";

    /// <summary>Codex's turn metadata.</summary>
    public const string CodexTurn = "x-codex-turn-metadata";

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

    /// <summary>The lineage a call's <c>_meta</c> carries, or null when it names neither harness's.</summary>
    public static CallLineage? Read(JsonObject? meta)
    {
        if (meta is null) return null;
        if (Id(meta[ClaudeToolUse]) is { } toolUse) return new CallLineage(Claude) { ToolUse = toolUse };
        if (Turn(meta[CodexTurn]) is not { } turn) return null;

        var lineage = new CallLineage(Codex)
        {
            Thread = Id(turn["thread_id"]),
            Parent = Id(turn["parent_thread_id"]),
            Kind = Id(turn["subagent_kind"]),
            Source = Id(turn["thread_source"]),
            Root = Id(turn["session_id"]),
        };
        return lineage.Thread is null && lineage.Root is null ? null : lineage;
    }

    /// <summary>A value kept only where it is a string shaped as an id.</summary>
    public static string? Id(JsonNode? value) =>
        value is JsonValue text && text.TryGetValue<string>(out var said) ? Id(said) : null;

    /// <summary>A string kept only where it is shaped as an id.</summary>
    public static string? Id(string? value) => value is not null && IdShape.IsMatch(value) ? value : null;

    /// <summary>Codex's metadata, an object or JSON text holding one; anything else is none.</summary>
    private static JsonObject? Turn(JsonNode? value)
    {
        if (value is JsonObject turn) return turn;
        if (value is not JsonValue text || !text.TryGetValue<string>(out var json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
