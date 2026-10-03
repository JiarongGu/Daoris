using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>A session asks the person for a go-ahead, once per act, on the ask its work is for (KNOWUSE1a, D135 §2).</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "go_ahead_ask")]
    [Description(
        "Ask the person for a go-ahead on an act outside this repository that is theirs to allow: a write to a system "
        + "(production above all), a release, a push, a sign-in, or a run against a system's data. It is held on the ask "
        + "your quest was asked by, once per act: an act already asked joins the first, and you are told its answer, "
        + "approved, refused or still waiting, so the person is never asked the same thing twice. Name an act already "
        + "asked in the words it was asked in, and it joins. Then say in your last message that the work waits on it, by "
        + "its number, commit what you have, and end your turn with the quest still taken.")]
    public async Task<string> AskGoAheadAsync(
        [Description(
            "The kind of act: write (a change to a system outside this repository, its configuration, data or entries), "
            + "release (deploying or publishing), push (sending commits, or opening a pull request), sign-in (credentials to "
            + "a system), or run (running something against a system's data).")]
        string kind,
        [Description("Where it lands: the environment or the system, such as production, development, or the service's name.")]
        string on,
        [Description("What it touches, in a few words: the configuration, the menu entries, the report. The same words join an act already asked.")]
        string act,
        [Description("Why the work needs it, and exactly what you would do. The person decides on this.")]
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
