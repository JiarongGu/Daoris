using Daoris.Driver;

namespace Daoris.Driver.Host;

/// <summary>
/// A conversation from a terminal: `daoris-driver chat --repository <name> [--adapter <name>]`.
/// </summary>
/// <remarks>
/// <para><b>Why a headless door at all.</b> The desktop is where a person chats (D49 §3), and the
/// console stream never leaves the machine that produced it (D47 §4) — but "the machine with no
/// screen is just another machine" (D47), and D50 says no capability may be stranded on one. A
/// server running `daoris-driver` can be talked to over ssh with exactly the same lock, the same
/// record and the same transcript; nothing here is a second implementation, because the mechanics are
/// <see cref="ChatRunner"/>'s and this is the reporting half.</para>
///
/// <para>It is also what lets the family rehearsal gate a whole conversation with no model in it:
/// pipe a scripted exchange in, read the record out.</para>
///
/// <para>Exit codes keep the family contract: 0 the conversation ran, 1 it was refused (busy,
/// unknown repository, a non-interactive adapter), 2 the driver could not run at all.</para>
/// </remarks>
internal static class ChatConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        var repository = Flag(args, "--repository");
        if (string.IsNullOrWhiteSpace(repository))
        {
            Console.Error.WriteLine(
                "usage: daoris-driver chat --repository <name> [--adapter <name>] [--profile <name>] [--own-tree]\n"
                + "  Messages are read from stdin, one per line; the session's output goes to stdout.\n"
                + "  A message sent while a turn runs waits for it. Ctrl+C during a turn stops the turn and\n"
                + "  keeps the conversation, handing back what was waiting; from a script, a line holding only\n"
                + "  ETX (U+0003, the character Ctrl+C is) does the same.\n"
                + "  End of input ends the conversation, and the record says how it finished.\n"
                + "  --profile picks which account to run as; omitted takes the workspace's\n"
                + "  default, then the machine's (`daoris agent profile ...`).\n"
                + "  --own-tree opens the conversation in a session worktree of its own (D51), so your\n"
                + "  uncommitted work in the checkout stays yours alone.");
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var adapter = Flag(args, "--adapter") ?? config.Adapter;
        var home = Path.GetDirectoryName(Path.GetFullPath(configPath))!;

        using var service = ServiceClient.FromEnvironment();
        // Marked under the home, so the desktop sharing it never takes this conversation for an orphan.
        var processes = new SessionProcesses(Path.Combine(home, "sessions"));

        // The console IS the point here, so the buffer exists — and every line goes straight out. No
        // ring-buffer replay: a terminal already keeps what scrolled past.
        var output = new SessionOutput();
        output.Lined += (_, line) => Console.WriteLine(line.Text);

        var ended = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        // The build's adapters plus whatever the home's plugins declare (D64): a harness a plugin
        // declared is one a conversation from a terminal can run on, the same as from the desktop.
        var built = AdapterSet.Built();
        var adapters = built.WithPlugins(PluginCatalog.Load(home, built.Names));
        // After the client, so disposed before it: a conversation still open when this door leaves —
        // an exception, say — is ended and recorded through a client that is still there.
        using var runner = new ChatRunner(service, adapters, home, processes, output);

        var start = await runner.StartAsync(
            repository, adapter, config,
            onEnded: (_, state) =>
            {
                ended.TrySetResult(state);
                return Task.CompletedTask;
            },
            // The per-session picker, on the surface a machine with no screen has (D49 §4, D50).
            profile: Flag(args, "--profile"),
            // The per-conversation tree choice (D51) — beside the repository's standing opt-in.
            ownTree: args.Contains("--own-tree")).ConfigureAwait(false);

        if (start.SessionId is null)
        {
            // A refusal is an answer the person acts on — the ledger's own sentence, verbatim.
            Console.Error.WriteLine($"chat: {start.Message}");
            return 1;
        }

        Console.Error.WriteLine($"chat: {start.Message}");

        // Stopping the turn is the terminal's third verb (CONV4a, D50): Ctrl+C while a turn runs, as a
        // person at a prompt expects of any program that is busy. With no turn running it does what it
        // always did, because a person pressing it then means to leave.
        var id = start.SessionId;
        ConsoleCancelEventHandler interrupt = (_, pressed) =>
        {
            if (!runner.Taking(id)) return;
            pressed.Cancel = true;
            _ = StopTurnAsync(runner, id);
        };
        Console.CancelKeyPress += interrupt;

        // Stdin is the person. A closed stdin is them finishing, which ENDS the conversation rather
        // than killing it: the harness gets end-of-input, says whatever it was going to say, and exits
        // on its own — a `completed` record, not a `stopped` one.
        try
        {
            while (await Console.In.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                // A script has no Ctrl+C to press: a line that is only the character it stands for is the
                // same stop, which is also how the family rehearsal holds this door.
                if (line == "\u0003")
                {
                    await StopTurnAsync(runner, id).ConfigureAwait(false);
                    continue;
                }

                if (!runner.Say(id, line))
                {
                    Console.Error.WriteLine("chat: the session is no longer listening.");
                    break;
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= interrupt;
        }

        // Finished through the conversation, which knows its door (CONV3b).
        runner.Finish(start.SessionId);

        var state = await ended.Task.ConfigureAwait(false);
        Console.Error.WriteLine($"chat: session {start.SessionId} is {state}.");
        return 0;
    }

    /// <summary>
    /// Stop the turn and say what the stop did — including what it handed back, since a terminal has no
    /// draft to put it in and the person should see each line they will not have sent.
    /// </summary>
    private static async Task StopTurnAsync(ChatRunner runner, string sessionId)
    {
        try
        {
            var stop = await runner.CancelTurnAsync(sessionId).ConfigureAwait(false);
            Console.Error.WriteLine(
                stop.Cancelled ? "chat: the turn was asked to stop; the conversation goes on."
                : stop.Withdrawn.Count > 0 ? "chat: nothing had reached the agent yet; the conversation goes on."
                : "chat: no turn was running.");
            foreach (var withdrawn in stop.Withdrawn) Console.Error.WriteLine($"chat: not sent: {withdrawn}");
        }
        catch (DriverException refused)
        {
            Console.Error.WriteLine($"chat: {refused.Message}");
        }
    }

    private static string? Flag(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[at + 1]
            : null;
    }
}
