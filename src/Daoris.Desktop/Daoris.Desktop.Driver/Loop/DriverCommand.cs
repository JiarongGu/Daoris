namespace Daoris.Driver;

/// <summary>How a loop runs, once one is asked for.</summary>
public enum LoopMode
{
    /// <summary>Tick until stopped, a poll apart.</summary>
    Watch,

    /// <summary>One tick, then exit.</summary>
    Once,

    /// <summary>Tick until nothing starts, then exit — the deterministic mode a gate drives.</summary>
    UntilIdle,
}

/// <summary>A loop, asked for by name.</summary>
/// <param name="Mode">How it runs.</param>
/// <param name="Share">Run beside a live driver on the same home, on purpose, rather than be refused (D104).</param>
public sealed record LoopRequest(LoopMode Mode, bool Share)
{
    /// <summary>
    /// <c>--quiet-end &lt;minutes&gt;</c> (SESSWATCH1a, D200, the session-watch design §2): a session of this run unheard that long, busy or
    /// not, is ended and carried on, for a run nobody watches that must return. Only with <c>--once</c> or <c>--until-idle</c>; null waits.
    /// </summary>
    public int? QuietEnd { get; init; }
}

/// <summary>
/// The loop a person asks for by its verb (D104), and the tree's usage: a bare invocation, or a word no command answers, is the usage
/// and starts nothing. In the library rather than the host, so that is held by a test, as the plugin kit's command is.
/// </summary>
[CommandGroup("drive", Whole = true)]
public static class DriverCommand
{
    /// <summary>
    /// What a bare <c>daoris</c> and <c>daoris help</c> print: the hand-off to the CLI, then each group of the tree with its sub-verbs, a
    /// group's lines beginning with its name (<see cref="UsageOf"/>). Every group has a line, held by a test against the tree.
    /// </summary>
    public const string Usage =
        DriveUsageIntro.Text + "\n" + DriveUsageAsk.Text + "\n" + DriveUsageRecords.Text + "\n" + DriveUsagePlace.Text + "\n"
        + DriveUsageSession.Text + "\n" + DriveUsageMachine.Text;

    /// <summary>
    /// One group's lines of <see cref="Usage"/> (<c>daoris help &lt;group&gt;</c>): each line that begins with its name, and the lines
    /// that describe them; null for a group with none.
    /// </summary>
    public static string? UsageOf(string group)
    {
        List<string> lines = [];
        var taking = false;
        foreach (var line in Usage.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal))
            {
                var first = line[2..].Split(' ', 2)[0];
                taking = first == group;
            }
            else if (!line.StartsWith("      ", StringComparison.Ordinal))
            {
                taking = false;
            }

            if (taking) lines.Add(line);
        }

        return lines.Count == 0 ? null : $"usage: daoris {group} …\n\n{string.Join("\n", lines)}";
    }

    /// <summary>Whether the words ask for the usage itself — an answer, exit 0, not a mistake.</summary>
    public static bool AskedForHelp(IReadOnlyList<string> args) =>
        args is ["help" or "--help" or "-h"];

    /// <summary>
    /// The loop the words ask for, or null for the usage — with what was not understood, where
    /// something was said. A bare invocation is null with nothing to say.
    /// </summary>
    public static LoopRequest? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Count == 0 || AskedForHelp(args)) return null;

        // One spelling (ONEWAY1ai-i): a mode flag before the verb is the tree's refusal, naming `drive`.
        if (args[0] != "drive")
        {
            problem = $"no verb `{args[0]}` — the loop is `drive`.";
            return null;
        }

        var flags = args.Skip(1);

        bool once = false, untilIdle = false, share = false;
        int? quietEnd = null;
        using var words = flags.GetEnumerator();
        while (words.MoveNext())
        {
            var flag = words.Current;
            switch (flag)
            {
                case "--once": once = true; break;
                case "--until-idle": untilIdle = true; break;
                case "--share": share = true; break;
                // SESSWATCH1a: a whole number of minutes, at least one.
                case "--quiet-end":
                    if (!words.MoveNext() || !int.TryParse(words.Current, System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out var minutes) || minutes < 1)
                    {
                        problem = "`--quiet-end` takes a whole number of minutes, at least 1 — e.g. `--quiet-end 30`.";
                        return null;
                    }

                    quietEnd = minutes;
                    break;
                default:
                    problem = $"`drive` takes --once, --until-idle, --quiet-end and --share, not `{flag}`.";
                    return null;
            }
        }

        if (once && untilIdle)
        {
            problem = "`--once` and `--until-idle` are two modes — name one.";
            return null;
        }

        if (quietEnd is not null && !once && !untilIdle)
        {
            problem = "`--quiet-end` is for a run that must return, `--once` or `--until-idle`; the watch waits for the person.";
            return null;
        }

        return new LoopRequest(once ? LoopMode.Once : untilIdle ? LoopMode.UntilIdle : LoopMode.Watch, share) { QuietEnd = quietEnd };
    }

    /// <summary>
    /// How the host ends a run a cancellation ended (DEV3b, D115's DEV3a note): a close only where the person's own Ctrl+C
    /// asked for one, <i>driver: stopped.</i>, exit 0. A request the service never answered within the client's own timeout
    /// is a failure that says so, exit 2, as a service it could not reach is; and a cancellation nothing asked for is one too.
    /// </summary>
    /// <remarks>
    /// 🔴 The host read every cancellation as Ctrl+C, so a look whose own request timed out printed <i>driver: stopped.</i>
    /// and exited 0, and a script or a gate read a stalled service as a person's close.
    /// </remarks>
    /// <param name="closed">Whether the host's own close was asked for: its Ctrl+C, and nothing else.</param>
    public static LoopEnded Cancelled(OperationCanceledException error, bool closed) =>
        closed ? new LoopEnded(0, "driver: stopped.", Failed: false)
        // The client's own timeout: HttpClient cancels with a TimeoutException inside, and its message names the timeout.
        : error.InnerException is TimeoutException ? new LoopEnded(2, $"driver: the service did not answer in time — {error.Message}", Failed: true)
        : new LoopEnded(2, $"driver: a request was cancelled though nothing closed the run — {error.Message}", Failed: true);
}

/// <summary>How the host ends a run (DEV3b): its exit code, and the line it prints, to the error stream where it failed.</summary>
/// <param name="Exit"><c>0</c> for the person's close, <c>2</c> for a failure.</param>
/// <param name="Said">The line the host prints.</param>
/// <param name="Failed">Whether the run failed rather than closed: its line goes to the error stream and its log as a failure.</param>
public sealed record LoopEnded(int Exit, string Said, bool Failed);
