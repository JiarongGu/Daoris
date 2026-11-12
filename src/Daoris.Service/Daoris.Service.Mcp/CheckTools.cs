using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A working session's one tool to ask for a check: the build or test it would run itself, run by Daoris instead under the machine's
/// admission. The ask is a file the driver's broker takes; the session ends its turn, and the verdict is its next turn.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="checks">Where an ask is written, under the home.</param>
[McpServerToolType]
public sealed class CheckTools(IntakeScope? intake = null, SessionLedger? ledger = null, CheckBox? checks = null)
{
    [McpServerTool(Name = "check_run")]
    [Description(
        "Have Daoris run a build or test in your tree instead of you: it runs one at a time on this machine. Say what you asked, "
        + "then end your turn; the verdict, with a failure's log tail, is your next turn, and the wait costs no tokens. Commit first.")]
    public async Task<string> RunCheckAsync(
        [Description("A declared gate by name; with no command, every heavy gate.")]
        string? gate = null,
        [Description("Or a command of your own, as words (a filtered test run).")]
        string[]? command = null,
        CancellationToken ct = default)
    {
        var box = checks ?? CheckBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return box.Ask(record, session, gate, command, DateTimeOffset.UtcNow).Message;
    }
}
