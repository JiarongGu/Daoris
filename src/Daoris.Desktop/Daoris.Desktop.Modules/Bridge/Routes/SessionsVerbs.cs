using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The sessions verbs a terminal sends over the pipe: a person's route asked for its words
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
    /// <summary>The world the sessions verbs run in for a terminal's route: this Daoris's service, home, conversation runner and record writer.</summary>
    public static SessionsWorld WorldOf(DriverContext context)
    {
        var loop = context.Loop;
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var config = DriverConfig.Load(loop.ConfigPath);
        return new SessionsWorld(service, loop.Home, config, DriverContext.UnadoptedOf(loop, config), loop.Log)
        {
            CoolingOf = (adapter, profile, model) => loop.Harnesses.CoolingOf(adapter ?? "", profile, model),
            // `sessions partner`'s read of the lead's tree, the loop's, as the page's press reads it.
            PieceBase = loop.PieceBases,
            // `sessions stop <id> --turn`, the keeping stop, on the turns this Daoris's loop holds.
            StopTurn = (id, _) => ConversationRoutes.KeepingStopAsync(loop, id),
            // A verb's line on a session's record is the loop's, so one writer numbers it.
            Notes = (id, note) => HandOverAskNotes.Say(loop.Events, loop.Output, id, note),
        };
    }

    /// <summary>The verb's lines and exit code, as an answer carries them (<see cref="TreesSaid.ToWire"/>).</summary>
    public static async Task<object> SaidAsync(DriverContext context, SessionsAsk ask, CancellationToken ct)
    {
        var loop = context.Loop;
        var output = new StringWriter { NewLine = "\n" };
        var world = WorldOf(context);

        var code = await SessionsCommand.RunAsync(ask, world, output, ct).ConfigureAwait(false);
        loop.Nudge();
        var lines = output.ToString().Split('\n', StringSplitOptions.None);
        // The writer ends every line with its newline, so the last piece is the empty one after it.
        return new { Said = new TreesSaid([.. lines.Take(lines.Length - 1)], [], code).ToWire() };
    }

    /// <summary>
    /// <c>daoris help</c>'s lines and exit code for a route a terminal sent: <see cref="HelpCommand.RunAsync"/> with this
    /// Daoris's service, home and agents, so a rename, a pin, a delete and a list say what they say alone. A press nudges the loop.
    /// </summary>
    public static async Task<object> HelpSaidAsync(DriverContext context, HelpVerbAsk ask, CancellationToken ct)
    {
        var loop = context.Loop;
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var config = DriverConfig.Load(loop.ConfigPath);
        var world = new SessionsWorld(service, loop.Home, config, DriverContext.UnadoptedOf(loop, config), loop.Log)
        {
            CoolingOf = (adapter, profile, model) => loop.Harnesses.CoolingOf(adapter ?? "", profile, model),
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
            return loop.Harnesses.Adapters.Door(adapter).Can.Resumes;
        }
        catch (DriverException)
        {
            return false;
        }
    }
}
