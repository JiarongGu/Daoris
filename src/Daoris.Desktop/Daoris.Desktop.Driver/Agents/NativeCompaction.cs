namespace Daoris.Driver;

/// <summary>
/// The native door's compaction frame read once: Claude Code's stream-json writes a <c>system</c> frame of subtype <c>compact_boundary</c>
/// whose <c>compact_metadata</c> names the <c>trigger</c> (<c>auto</c> or <c>manual</c>) and the <c>pre_tokens</c> the context held.
/// </summary>
public static class NativeCompaction
{
    /// <summary>The compaction the frame says; null where the frame is no compact boundary.</summary>
    public static WireReading.Compacted? Read(NativeFrame frame)
    {
        if (!frame.IsObject || frame.Type != "system" || frame.Subtype != "compact_boundary") return null;
        if (!frame.Root.TryGetProperty("compact_metadata", out var meta) || meta.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return new WireReading.Compacted(null, null);
        }

        return new WireReading.Compacted(Json.NonEmpty(meta, "trigger"), Json.Long(meta, "pre_tokens"));
    }
}
