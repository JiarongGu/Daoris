using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>A session asks the person for a go-ahead, once per act, on the ask its work is for.</summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
[McpServerToolType]
public sealed class GoAheadTools(IntakeScope? intake = null, SessionLedger? ledger = null)
{
    [McpServerTool(Name = "go_ahead_ask")]
    [Description(
        "Ask the person's go-ahead on an act outside this repository that is theirs to allow (a write to production above all). "
        + "An act asked in the same words joins the first. Then say the work waits on it, by its number, commit, and end your turn.")]
    public async Task<string> AskGoAheadAsync(
        [Description("write, release, push (commits or a pull request), sign-in, or run (against a system's data).")]
        string kind,
        [Description("Where it lands: the environment or system, such as production.")]
        string on,
        [Description("What it touches, in a few words; the same words join an act already asked.")]
        string act,
        [Description("Why the work needs it, and exactly what you would do.")]
        string why,
        CancellationToken ct = default)
    {
        // A channel of the one door: the same request `question_ask` makes for an act, held once per act on the ask.
        var request = new RoleRequest(intake?.Session, why) { Kind = kind ?? "", Touches = act, On = on };
        return await QuestionTools.Requests(intake, ledger, null, null).AskAsync(request, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
    }
}
