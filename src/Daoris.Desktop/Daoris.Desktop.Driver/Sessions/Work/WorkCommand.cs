namespace Daoris.Driver;

/// <summary>What a terminal asked of a work (PAUSE1b, PAUSE1d, D132 §7.2): pause, resume or abandon an ask's, or one quest's.</summary>
/// <param name="Verb"><c>pause</c>, <c>resume</c> or <c>abandon</c>.</param>
public sealed record WorkAsk(WorkScope Scope, string Verb, string Id)
{
    /// <summary>An abandon's reason (PAUSE1d), which each declined quest keeps; null where none was given.</summary>
    public string? Reason { get; init; }

    /// <summary>An abandon's <c>--yes</c>: the second press, which abandons what the list holds now.</summary>
    public bool Yes { get; init; }
}

/// <summary>
/// <c>daoris-driver ask --pause|--resume|--abandon &lt;id&gt;</c> and <c>quest pause|resume|abandon &lt;id&gt;</c> (PAUSE1b, PAUSE1d,
/// D132 §7.2, D50): the terminal's door to <see cref="WorkPausing"/> and <see cref="WorkAbandoning"/>, which the screen's
/// <c>WORK_PAUSE</c>, <c>WORK_RESUME</c> and <c>WORK_ABANDON</c> call too. In the library, so its words are held by a test.
/// </summary>
/// <remarks>
/// <para><b>A session another Daoris process runs</b> is stopped through the request folder SESSUX1g made, which every loop
/// on the home watches, with <c>by: pause</c> or <c>by: abandon</c>; one nothing here runs is ended as the screen ends an orphan.</para>
///
/// <para><b>Without <c>--yes</c>, an abandon is the first press</b>: it prints the list and changes nothing. With it, it judges
/// every piece again and abandons what may go, as <c>trees clean --yes</c> does; <c>--yes</c> without <c>--reason</c> is
/// refused before the service is asked anything.</para>
///
/// <para>Exit codes are the family's: 0 done, listed, or nothing to do · 1 refused, no such ask or quest, or no reason · 2
/// could not: a session of the work this machine runs was not stopped, a step of an abandon failed, or the service did not
/// answer.</para>
/// </remarks>
public static class WorkCommand
{
    public const string Usage =
        "usage: daoris-driver ask --pause|--resume <id>  ·  daoris-driver ask --abandon <id> [--reason \"…\" --yes]  ·  "
        + "daoris-driver quest pause|resume <id>  ·  daoris-driver quest abandon <id> [--reason \"…\" --yes]";

    /// <summary>Whether the words after <c>ask</c> or <c>quest</c> name a pause, a resume or an abandon, rightly or not.</summary>
    public static bool Asks(WorkScope scope, IReadOnlyList<string> args) =>
        args.Count > 0 && (scope == WorkScope.Ask ? args[0] is "--pause" or "--resume" or "--abandon" : args[0] is "pause" or "resume" or "abandon");

    /// <summary>What the words after <c>ask</c> or <c>quest</c> ask, or null with what is wrong with them.</summary>
    public static WorkAsk? Read(WorkScope scope, IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (Asks(scope, args) && args[0].TrimStart('-') == "abandon") return ReadAbandon(scope, args, out problem);
        if (args is [var verb, var id] && Asks(scope, args) && Id(id) is { } named)
        {
            return new WorkAsk(scope, verb.TrimStart('-'), named);
        }

        problem = scope == WorkScope.Ask
            ? "`--pause` and `--resume` take one ask's id."
            : "`pause` and `resume` take one quest's id.";
        return null;
    }

    public static async Task<int> RunAsync(WorkAsk ask, WorkWorld world, TextWriter output, CancellationToken ct = default)
    {
        switch (ask.Verb)
        {
            case "pause":
                var paused = await WorkPausing.PauseAsync(world, ask.Scope, ask.Id, PluginEvents.Terminal, ct).ConfigureAwait(false);
                Write(output, WorkPausing.Lines(paused));
                return paused.Verdict switch
                {
                    PauseVerdict.Unknown => 1,
                    PauseVerdict.Paused when paused.Kept.Any(kept => kept.Unreached) => 2,
                    _ => 0,
                };
            case "abandon":
                return await AbandonAsync(ask, world, output, ct).ConfigureAwait(false);
        }

        var resumed = await WorkPausing.ResumeAsync(world, ask.Scope, ask.Id, PluginEvents.Terminal, ct).ConfigureAwait(false);
        Write(output, WorkPausing.Lines(resumed));
        return resumed.Verdict == ResumeVerdict.Unknown ? 1 : 0;
    }

    /// <summary>The first press without <c>--yes</c>, the second with it (design §7.2).</summary>
    private static async Task<int> AbandonAsync(WorkAsk ask, WorkWorld world, TextWriter output, CancellationToken ct)
    {
        if (!ask.Yes)
        {
            var plan = await WorkAbandoning.PlanAsync(world, ask.Scope, ask.Id, ct).ConfigureAwait(false);
            if (plan is null)
            {
                Write(output, [WorkPausing.Unknown(ask.Scope, ask.Id)]);
                return 1;
            }

            Write(output, WorkAbandoning.Lines(plan));
            return 0;
        }

        // The reason before anything is asked: each declined quest keeps it.
        if (string.IsNullOrWhiteSpace(ask.Reason))
        {
            Write(output, [WorkAbandoning.NeedsReason]);
            return 1;
        }

        var outcome = await WorkAbandoning.AbandonAsync(world, ask.Scope, ask.Id, ask.Reason, pieces: null, PluginEvents.Terminal, ct)
            .ConfigureAwait(false);
        Write(output, WorkAbandoning.Lines(outcome));
        return outcome.Verdict switch
        {
            AbandonVerdict.Unknown or AbandonVerdict.Reason => 1,
            AbandonVerdict.Abandoned when outcome.Failed.Count > 0 => 2,
            _ => 0,
        };
    }

    /// <summary>
    /// <c>ask --abandon &lt;id&gt;</c> or <c>quest abandon &lt;id&gt;</c>, then <c>--reason "…"</c> and <c>--yes</c> in either order,
    /// each at most once.
    /// </summary>
    private static WorkAsk? ReadAbandon(WorkScope scope, IReadOnlyList<string> args, out string? problem)
    {
        problem = scope == WorkScope.Ask
            ? "`--abandon` takes one ask's id, then `--reason \"…\"` and `--yes` to abandon what its list holds."
            : "`abandon` takes one quest's id, then `--reason \"…\"` and `--yes` to abandon what its list holds.";
        if (args.Count < 2 || Id(args[1]) is not { } id) return null;

        string? reason = null;
        var yes = false;
        for (var at = 2; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--reason" when reason is null && at + 1 < args.Count && !string.IsNullOrWhiteSpace(args[at + 1]):
                    reason = args[++at];
                    break;
                case "--yes" when !yes:
                    yes = true;
                    break;
                default:
                    return null;
            }
        }

        problem = null;
        return new WorkAsk(scope, "abandon", id) { Reason = reason, Yes = yes };
    }

    /// <summary>An id as a person writes it, without its <c>#</c>, or null where it is none or a flag.</summary>
    private static string? Id(string word) =>
        !word.StartsWith('-') && word.Trim().TrimStart('#').Trim() is { Length: > 0 } id ? id : null;

    /// <summary>The first line under the binary's name, as its other one-line answers are; the rest as written, indented.</summary>
    private static void Write(TextWriter output, IReadOnlyList<string> lines)
    {
        for (var at = 0; at < lines.Count; at++) output.WriteLine(at == 0 ? $"daoris-driver: {lines[at]}" : lines[at]);
    }
}
