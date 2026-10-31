using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Knowledge;

/// <summary>What the session's hook lines said of one call id.</summary>
public enum HookJoinOutcome
{
    /// <summary>A hook line for the id names no agent: the session's own agent made the call.</summary>
    Root,

    /// <summary>A hook line for the id names a subagent.</summary>
    Child,

    /// <summary>No hook line for the id was read within the budget.</summary>
    NotFound,

    /// <summary>Hook lines for the id disagree.</summary>
    Conflict,

    /// <summary>The hook was sent an agent id it could not read.</summary>
    Unreadable,

    /// <summary>The wait was cancelled before it could say.</summary>
    TimedOut,
}

/// <summary>
/// A Claude Code call's id joined to the hook line naming the agent behind it, in the session's lineage file (FABRIC-C2a, D173's
/// FABRIC-C2a note, point 5): read from the end, bounded in bytes and time, cancellable, and opened with every writer let in.
/// </summary>
/// <remarks>
/// Only a <c>by: hook</c>, <c>event: call</c> line with the exact id joins; the connector's own <c>by: call</c> line carries the
/// same id and is no join. The hook runs before its call (measured 21 ms to 6.5 s ahead, D173's 2026-10-21 note), so the first
/// look finds it; the <see cref="Budget"/> is the ceiling for a line not yet written.
/// </remarks>
public static class HookJoin
{
    /// <summary>How long a call waits for its hook line.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    /// <summary>How many bytes from the file's end are read.</summary>
    public const int MaxBytes = 1 << 20;

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(15);

    /// <summary>What the hook said of <paramref name="toolUse"/> in <paramref name="path"/>; never throws.</summary>
    public static async Task<HookJoinOutcome> FindAsync(
        string? path, string toolUse, TimeSpan budget, CancellationToken cancellationToken, int maxBytes = MaxBytes)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (cancellationToken.IsCancellationRequested) return HookJoinOutcome.TimedOut;
            var outcome = path is null ? null : Scan(path, toolUse, maxBytes);
            if (outcome is { } said) return said;

            var left = budget - clock.Elapsed;
            if (left <= TimeSpan.Zero) return HookJoinOutcome.NotFound;
            try
            {
                await Task.Delay(left < Poll ? left : Poll, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return HookJoinOutcome.TimedOut;
            }
        }
    }

    /// <summary>The outcome of the window's lines for the id, or null where none names it.</summary>
    private static HookJoinOutcome? Scan(string path, string toolUse, int maxBytes)
    {
        byte[] window;
        bool cut;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var floor = Math.Max(0, stream.Length - maxBytes);
            window = new byte[stream.Length - floor];
            stream.Seek(floor, SeekOrigin.Begin);
            stream.ReadExactly(window);
            cut = floor > 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            return null;
        }

        // A window that begins inside the file begins mid-line: that line is not read.
        var start = cut ? Array.IndexOf(window, (byte)'\n') + 1 : 0;
        if (cut && start == 0) return null;

        HookJoinOutcome? found = null;
        foreach (var text in Encoding.UTF8.GetString(window, start, window.Length - start).Split('\n'))
        {
            if (!text.Contains(toolUse, StringComparison.Ordinal) || Said(text, toolUse) is not { } said) continue;
            found = found is null || found == said ? said : HookJoinOutcome.Conflict;
            if (found == HookJoinOutcome.Conflict) break;
        }

        return found;
    }

    /// <summary>What one line says of the id, or null where it is no hook line for it.</summary>
    private static HookJoinOutcome? Said(string text, string toolUse)
    {
        JsonObject? line;
        try
        {
            line = JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (line is null || Text(line, "by") != "hook" || Text(line, "event") != "call" || Text(line, "toolUse") != toolUse) return null;
        if (line["agentUnreadable"] is JsonValue bad && bad.TryGetValue<bool>(out var unread) && unread) return HookJoinOutcome.Unreadable;
        if (line["agent"] is null) return HookJoinOutcome.Root;
        return CallLineage.Id(Text(line, "agent")) is null ? HookJoinOutcome.Unreadable : HookJoinOutcome.Child;
    }

    private static string? Text(JsonObject line, string name) =>
        line[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
