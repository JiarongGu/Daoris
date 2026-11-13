using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>A session's line, read back a page at a time (the session-context design §3.5): a session that continues another (a
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
        "Read what the earlier parts of this session said and did, when the brief Daoris handed you left something out. This "
        + "session's line is the sessions it continues and itself, each a part named by its id. Read-only: it reads Daoris's record "
        + "on this machine, never over the network, and refuses any other session's record. A page is a part's newest entries "
        + "(or those before a number a page named), oldest first: the person's words, the agent's messages, tool calls, plans and "
        + "Daoris's notes, never the agent's thoughts. An answer says where to read on.")]
    public async Task<string> ReadAsync(
        [Description("The id of a part of this session's line, as an answer lists them. Omit for the part this session continues.")]
        string? part = null,
        [Description("Read the entries before this number, to page back. Omit for the newest.")]
        long? before = null,
        [Description("At most this many entries, 1 to 200; default 50.")] int? limit = null,
        CancellationToken ct = default)
    {
        var reader = lines ?? LineEvents.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        IReadOnlyList<Session> line = record is null ? [] : await LineEvents.LineOfAsync(record, ledger!.FindAsync, ct).ConfigureAwait(false);
        return reader.Read(line, session, part, before, limit).Message;
    }
}
