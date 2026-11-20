using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A working session's one tool to settle a run's step Daoris handed it: a step cast to the role's agent is the run's working agent's,
/// done in its own session and settled here, with its outcome and evidence, at the settle door as its agent. The settle is a file the driver
/// takes; the session ends its turn, and what it came to is its next turn.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="box">Where the settle is written, under the home's sessions folder.</param>
[McpServerToolType]
public sealed class StepSettleTools(IntakeScope? intake = null, SessionLedger? ledger = null, StepSettleBox? box = null)
{
    [McpServerTool(Name = "step_settle")]
    [Description(
        "Settle a run's step Daoris handed you, once done: its outcome, your words, the evidence it names. Cannot? Ask up your line. Then end your turn.")]
    public async Task<string> SettleAsync(
        [Description("The run the step's words named.")] string run,
        [Description("The step's id.")] string node,
        [Description("A decision's outcome; a review's accepted or changes; an approval's approved or rejected: required for each. None for a task.")] string? outcome = null,
        [Description("What you did, or why.")] string? words = null,
        [Description("The evidence by name.")] Dictionary<string, string>? evidence = null,
        [Description("The epoch the words named.")] int? epoch = null,
        CancellationToken ct = default)
    {
        var settles = box ?? StepSettleBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return settles.Ask(record, session, run, node, outcome, words, evidence, epoch, DateTimeOffset.UtcNow).Message;
    }
}
