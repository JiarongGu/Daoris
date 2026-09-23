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
                + "  End of input ends the conversation, and the record says how it finished.\n"
                + "  --profile picks which credential profile to run as; omitted takes the workspace's\n"
                + "  default, then the machine's (`daoris harness profile ...`).\n"
                + "  --own-tree opens the conversation in a session worktree of its own (D51), so your\n"
                + "  uncommitted work in the checkout stays yours alone.");
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var adapter = Flag(args, "--adapter") ?? config.Adapter;
        var home = Path.GetDirectoryName(Path.GetFullPath(configPath))!;

        using var service = ServiceClient.FromEnvironment();
        var processes = new SessionProcesses();

        // The console IS the point here, so the buffer exists — and every line goes straight out. No
        // ring-buffer replay: a terminal already keeps what scrolled past.
        var output = new SessionOutput();
        output.Lined += (_, line) => Console.WriteLine(line.Text);

        var ended = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        // The build's adapters plus whatever the home's plugins declare (D64): a harness a plugin
        // declared is one a conversation from a terminal can run on, the same as from the desktop.
        var built = AdapterSet.Built();
        var adapters = built.WithPlugins(PluginCatalog.Load(home, built.Names));
        var runner = new ChatRunner(service, adapters, home, processes, output);

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

        // Stdin is the person. A closed stdin is them finishing, which ENDS the conversation rather
        // than killing it: the harness gets end-of-input, says whatever it was going to say, and exits
        // on its own — a `completed` record, not a `stopped` one.
        while (await Console.In.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (!runner.Say(start.SessionId, line))
            {
                Console.Error.WriteLine("chat: the session is no longer listening.");
                break;
            }
        }

        processes.CloseInput(start.SessionId);

        var state = await ended.Task.ConfigureAwait(false);
        Console.Error.WriteLine($"chat: session {start.SessionId} is {state}.");
        return 0;
    }

    private static string? Flag(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[at + 1]
            : null;
    }
}
