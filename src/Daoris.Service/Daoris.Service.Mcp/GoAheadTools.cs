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
        "Ask the person for a go-ahead on an act outside this repository that is theirs to allow: a write to a system "
        + "(production above all), a release, a push, a sign-in, or a run against a system's data. It is held once per act: "
        + "an act already asked in the same words joins the first, and you are told its answer. Then say in your last message "
        + "that the work waits on it, by its number, commit what you have, and end your turn with the quest still taken.")]
    public async Task<string> AskGoAheadAsync(
        [Description(
            "write (a change to an outside system), release (deploying or publishing), push (sending commits, or opening a "
            + "pull request), sign-in (credentials to a system), or run (something against a system's data).")]
        string kind,
        [Description("Where it lands: the environment or system, such as production.")]
        string on,
        [Description("What it touches, in a few words; the same words join an act already asked.")]
        string act,
        [Description("Why the work needs it, and exactly what you would do.")]
        string why,
        CancellationToken ct = default)
    {
        // Only a session the driver started names itself on its connector, and only its record says which ask it is on.
        if (ledger is null || intake?.Session is not { } session)
        {
            return "This connector speaks for no session the driver started, so there is no ask to hold a go-ahead on: say "
                   + "exactly what you need and why in your last message, and end your turn.";
        }

        return (await ledger.AskGoAheadAsync(session, kind, on, act, why, DateTimeOffset.UtcNow, ct).ConfigureAwait(false)).Message;
    }
}
