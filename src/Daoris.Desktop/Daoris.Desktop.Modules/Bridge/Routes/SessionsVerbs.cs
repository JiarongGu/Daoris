using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The sessions verbs a terminal sends over the pipe (RPC1d, the terminal RPC design §7.3): a person's route asked for its words
/// (<c>said: true</c>) runs the verb's own path (<see cref="SessionsCommand.RunAsync"/>) with this Daoris's service, home and
/// conversation runner, and answers the lines it prints and its exit code, so the terminal says what it says alone, case for case.
/// </summary>
/// <remarks>
/// The world has no instance of its own to look for, so the verb never sends the route on. A request-folder wait the verb holds
/// (stop, say, take-back) is the wait it holds beside a window: this Daoris's loop takes the request from the home as it takes a
/// verb's from another process. A conversation a verb opens (<c>start-from</c>, <c>hand-over</c>) opens in this Daoris's window, whose
/// chat runner holds it; the terminal is told so in the runner's own sentence and ends.
/// </remarks>
internal static class SessionsVerbs
{
    /// <summary>The verb's lines and exit code, as an answer carries them (<see cref="TreesSaid.ToWire"/>).</summary>
    public static async Task<object> SaidAsync(DriverContext context, SessionsAsk ask, CancellationToken ct)
    {
        var loop = context.Loop;
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var config = DriverConfig.Load(loop.ConfigPath);
        var output = new StringWriter { NewLine = "\n" };
        var world = new SessionsWorld(service, loop.Home, config, DriverContext.DoorOf(loop, config), loop.Log)
        {
            CoolingOf = (adapter, profile) => loop.Harnesses.CoolingOf(adapter ?? "", profile),
            StartFrom = async (id, token) =>
            {
                var chat = loop.Chat ?? throw DriverArguments.NotReady();
                var started = await chat.StartFromAsync(
                    id, config.Adapter, config, onEnded: (session, state) => DriverLoop.Ended(context.Events, session, state), token).ConfigureAwait(false);
                return SessionsCommand.Started(started, output) ?? 0;
            },
            HandOver = async (id, agent, account, token) =>
            {
                var chat = loop.Chat ?? throw DriverArguments.NotReady();
                var handed = await chat.HandOverAsync(
                    id, agent, account, HandOverWhy.Press, HandOverBy.Person, config,
                    onEnded: (session, state) => DriverLoop.Ended(context.Events, session, state), token).ConfigureAwait(false);
                return SessionsCommand.HandedOver(handed, output) ?? 0;
            },
            // COAGENT1b: `sessions partner`'s read of the lead's tree, the loop's, as the page's press reads it.
            PieceBase = loop.PieceBases,
            // COWORK1a: `sessions stop <id> --turn`, the keeping stop, on the turns this Daoris's loop holds.
            StopTurn = (id, _) => ConversationRoutes.KeepingStopAsync(loop, id),
            // HANDOVERSAID1: a verb's line on a session's record is the loop's, so one writer numbers it.
            Notes = (id, note) => HandOverAskNotes.Say(loop.Events, loop.Output, id, note),
        };

        var code = await SessionsCommand.RunAsync(ask, world, output, ct).ConfigureAwait(false);
        loop.Nudge();
        var lines = output.ToString().Split('\n', StringSplitOptions.None);
        // The writer ends every line with its newline, so the last piece is the empty one after it.
        return new { Said = new TreesSaid([.. lines.Take(lines.Length - 1)], [], code).ToWire() };
    }

    /// <summary>
    /// <c>daoris-driver help</c>'s lines and exit code for a route a terminal sent (RPC1e): <see cref="HelpCommand.RunAsync"/> with this
    /// Daoris's service, home and agents, so a rename, a pin, a delete and a list say what they say alone. A press nudges the loop.
    /// </summary>
    public static async Task<object> HelpSaidAsync(DriverContext context, HelpVerbAsk ask, CancellationToken ct)
    {
        var loop = context.Loop;
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var config = DriverConfig.Load(loop.ConfigPath);
        var world = new SessionsWorld(service, loop.Home, config, DriverContext.DoorOf(loop, config), loop.Log)
        {
            CoolingOf = (adapter, profile) => loop.Harnesses.CoolingOf(adapter ?? "", profile),
        };

        var said = await TerminalSaid.RunAsync(output => HelpCommand.RunAsync(ask, world, adapter => Resumes(loop, adapter), output, ct)).ConfigureAwait(false);
        if (ask.Verb != "list") loop.Nudge();
        return said;
    }

    /// <summary>Whether an adapter's door resumes a conversation by its id, as the panel's history reads it: one this machine no longer has cannot.</summary>
    public static bool Resumes(DriverLoop loop, string adapter)
    {
        try
        {
            return loop.Harnesses.Adapters.Resolve(adapter).Resumes;
        }
        catch (DriverException)
        {
            return false;
        }
    }
}
