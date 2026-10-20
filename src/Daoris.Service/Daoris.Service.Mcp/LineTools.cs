using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A session's line, read back a page at a time (SESSCTX1f; the session-context design §3.5): a session that continues another (a
/// hand-over, an account move, a carry-on, a fallback) is handed Daoris's brief of its line, bounded, and what the bound left out is
/// read here from this machine's event files. Only for the session its connector names, and only the parts of that session's line.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the records the line is followed back through.</param>
/// <param name="lines">Where the parts' event files are read, under the home's sessions folder.</param>
[McpServerToolType]
public sealed class LineTools(IntakeScope? intake = null, SessionLedger? ledger = null, LineEvents? lines = null)
{
    [McpServerTool(Name = "line_read")]
    [Description(
        "Read what the earlier parts of this session said and did, when the brief Daoris handed you left something out, or you need a "
        + "word, an answer, a tool call or a plan it did not carry whole. This session's line is the sessions it continues (by a "
        + "hand-over, an account move, a carry-on or a fallback) and itself; each is a part, named by its id. Read-only: it reads "
        + "Daoris's record of those parts on this machine, never over the network, and refuses any other session's record. A page is "
        + "a part's newest entries, or those before a number a page named, oldest first: the person's words, the agent's messages, "
        + "tool calls, plans, turns' ends and Daoris's notes, never the agent's thoughts. An answer is bounded and says where to read on.")]
    public async Task<string> ReadAsync(
        [Description("Optional: the id of a part of this session's line, as an answer lists them. Left out, the part this session "
            + "continues, or this session itself where it continues none.")]
        string? part = null,
        [Description("Optional: read the entries before this number, as an answer names it, to page back. Left out, the part's newest.")]
        long? before = null,
        [Description("Optional: at most this many entries, from 1 to 200. Left out, 50.")] int? limit = null,
        CancellationToken ct = default)
    {
        var reader = lines ?? LineEvents.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        IReadOnlyList<Session> line = record is null ? [] : await LineEvents.LineOfAsync(record, ledger!.FindAsync, ct).ConfigureAwait(false);
        return reader.Read(line, session, part, before, limit).Message;
    }
}
