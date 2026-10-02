namespace Daoris.Driver;

/// <summary>What a terminal asked of a work (PAUSE1b, D132 §7.2): pause or resume an ask's, or one quest's.</summary>
/// <param name="Verb"><c>pause</c> or <c>resume</c>.</param>
public sealed record WorkAsk(WorkScope Scope, string Verb, string Id);

/// <summary>
/// <c>daoris-driver ask --pause|--resume &lt;id&gt;</c> and <c>quest pause|resume &lt;id&gt;</c> (PAUSE1b, D132 §7.2, D50): the
/// terminal's door to <see cref="WorkPausing"/>, which the screen's <c>WORK_PAUSE</c> and <c>WORK_RESUME</c> call too. In the
/// library, so its words are held by a test.
/// </summary>
/// <remarks>
/// <para><b>A session another Daoris process runs</b> is stopped through the request folder SESSUX1g made, which every loop
/// on the home watches, with <c>by: pause</c>; one nothing here runs is ended as the screen ends an orphan.</para>
///
/// <para>Exit codes are the family's: 0 done, or nothing to do · 1 refused, no such ask or quest · 2 could not: a session of
/// the work this machine runs was not stopped, or the service did not answer.</para>
/// </remarks>
public static class WorkCommand
{
    public const string Usage = "usage: daoris-driver ask --pause|--resume <id>  ·  daoris-driver quest pause|resume <id>";

    /// <summary>Whether the words after <c>ask</c> or <c>quest</c> name a pause or a resume, rightly or not.</summary>
    public static bool Asks(WorkScope scope, IReadOnlyList<string> args) =>
        args.Count > 0 && (scope == WorkScope.Ask ? args[0] is "--pause" or "--resume" : args[0] is "pause" or "resume");

    /// <summary>What the words after <c>ask</c> or <c>quest</c> ask, or null with what is wrong with them.</summary>
    public static WorkAsk? Read(WorkScope scope, IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is [var verb, var id] && Asks(scope, args) && id.Trim().TrimStart('#').Trim().Length > 0 && !id.StartsWith('-'))
        {
            return new WorkAsk(scope, verb.TrimStart('-'), id.Trim().TrimStart('#'));
        }

        problem = scope == WorkScope.Ask
            ? "`--pause` and `--resume` take one ask's id."
            : "`pause` and `resume` take one quest's id.";
        return null;
    }

    public static async Task<int> RunAsync(WorkAsk ask, WorkWorld world, TextWriter output, CancellationToken ct = default)
    {
        if (ask.Verb == "pause")
        {
            var paused = await WorkPausing.PauseAsync(world, ask.Scope, ask.Id, PluginEvents.Terminal, ct).ConfigureAwait(false);
            Write(output, WorkPausing.Lines(paused));
            return paused.Verdict switch
            {
                PauseVerdict.Unknown => 1,
                PauseVerdict.Paused when paused.Kept.Any(kept => kept.Unreached) => 2,
                _ => 0,
            };
        }

        var resumed = await WorkPausing.ResumeAsync(world, ask.Scope, ask.Id, PluginEvents.Terminal, ct).ConfigureAwait(false);
        Write(output, WorkPausing.Lines(resumed));
        return resumed.Verdict == ResumeVerdict.Unknown ? 1 : 0;
    }

    /// <summary>The first line under the binary's name, as its other one-line answers are; the rest as written, indented.</summary>
    private static void Write(TextWriter output, IReadOnlyList<string> lines)
    {
        for (var at = 0; at < lines.Count; at++) output.WriteLine(at == 0 ? $"daoris-driver: {lines[at]}" : lines[at]);
    }
}
