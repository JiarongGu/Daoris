using Daoris.Driver;

namespace Daoris.Driver.Host;

/// <summary>
/// A conversation of the running Daoris held from a terminal (RPC1g2 and RPC1g3, D238): <c>daoris chat --repository</c> starts one and
/// <c>--session</c> holds one already open; <c>session start-from</c> and <c>session hand-over</c> hold what they open. This is the
/// console's half: the process's own streams and Ctrl+C, handed to <see cref="HeldVerbs"/>, which holds the conversation. With no
/// Daoris running on the home, nothing runs.
/// </summary>
internal static class HeldChatConsole
{
    private const string Usage =
        "usage: daoris chat --repository <name> [--agent <name>] [--account <name>] [--own-tree]\n"
        + "       daoris chat --session <id>\n"
        + "  Holds a conversation of the running Daoris: --repository starts one, --session holds one that is open. Daoris's\n"
        + "  window asks you to confirm it once; this terminal then says words and stops turns in that conversation until it\n"
        + "  lets go. Messages are read from stdin, one per line; the session's output goes to stdout. A message sent while a\n"
        + "  turn runs waits for it. Ctrl+C during a turn stops the turn and keeps the conversation, handing back what was\n"
        + "  waiting; from a script, a line holding only ETX (U+0003, the character Ctrl+C is) does the same. Ctrl+C at rest\n"
        + "  lets go, and the conversation goes on in Daoris.\n"
        + "  A line `:attach <path>` attaches that file to your next message; Daoris reads it where it is, for this\n"
        + "  conversation.\n"
        + "  End of input finishes the conversation, and the record says how it ended.\n"
        + "  --account picks which account to run as; omitted takes the agent's order (`daoris agent account ...`).\n"
        + "  --own-tree opens the conversation in a session worktree of its own (D51), so your uncommitted work in the\n"
        + "  checkout stays yours alone.\n"
        + "  With no Daoris running on this home it does nothing: open Daoris, then run this again.";

    public static async Task<int> ChatAsync(string[] args)
    {
        var repository = Flag(args, "--repository");
        var session = Flag(args, "--session");
        if (string.IsNullOrWhiteSpace(repository) == string.IsNullOrWhiteSpace(session))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var world = InstanceHost.World("chat");
        return string.IsNullOrWhiteSpace(session)
            ? await HeldVerbs.StartAsync(world, new(repository!, Flag(args, "--agent"), Flag(args, "--account"), args.Contains("--own-tree")), Interrupts)
                .ConfigureAwait(false)
            : await HeldVerbs.HoldSessionAsync(world, session, Interrupts).ConfigureAwait(false);
    }

    public static Task<int> StartFromAsync(string session) => HeldVerbs.StartFromAsync(InstanceHost.World("sessions"), session, Interrupts);

    public static Task<int> HandOverAsync(string session, string agent, string? account) =>
        HeldVerbs.HandOverAsync(InstanceHost.World("sessions"), session, agent, account, Interrupts);

    /// <summary>Ctrl+C reaches the held conversation, which stops its turn or lets go; it ends this process only through that.</summary>
    private static IDisposable Interrupts(Action interrupt)
    {
        ConsoleCancelEventHandler handler = (_, pressed) =>
        {
            pressed.Cancel = true;
            interrupt();
        };
        Console.CancelKeyPress += handler;
        return new Unhooked(handler);
    }

    private static string? Flag(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[at + 1]
            : null;
    }

    private sealed class Unhooked(ConsoleCancelEventHandler handler) : IDisposable
    {
        public void Dispose() => Console.CancelKeyPress -= handler;
    }
}
