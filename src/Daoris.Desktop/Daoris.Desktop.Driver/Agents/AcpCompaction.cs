using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A compaction as Claude Code's adapter reports it over the protocol: the legacy tool call it sends a client that declares no compaction
/// updates, whose <c>_meta.claudeCode.toolName</c> is <c>compact</c>, done when a report of it says <c>completed</c>. The terminal report
/// can arrive twice, so each call id is told once.
/// </summary>
internal sealed class AcpCompaction
{
    private readonly HashSet<string> _told = new(StringComparer.Ordinal);

    /// <summary>Whether this update is the first report that a compaction completed.</summary>
    public bool Completed(JsonElement update)
    {
        var kind = AcpRender.Kind(update);
        if (kind is not ("tool_call" or "tool_call_update")) return false;
        if (!update.TryGetProperty("_meta", out var meta) || meta.ValueKind != JsonValueKind.Object
            || !meta.TryGetProperty("claudeCode", out var code) || code.ValueKind != JsonValueKind.Object
            || Json.Text(code, "toolName") != "compact"
            || Json.Text(update, "status") != "completed")
        {
            return false;
        }

        return _told.Add(Json.Text(update, "toolCallId") ?? "");
    }
}
