namespace Daoris.Driver;

/// <summary>
/// The owner's final exit: where every holder of a run's step has failed (today the role's holder and the vacancy path), Daoris interrupts the
/// owner once and the step then waits in <i>What needs you</i>, held by the person, until the owner looks. Nothing waits on the owner before it
/// unless the risk policy classes the act <c>owner</c>; there is no give-up in its place, none after it, and no second interruption. Each exit
/// is kept once, a spent red's on its telling (<see cref="NodeTold.Decided"/>), a vacancy's on its kept record (<see cref="StepVacancy.Exited"/>).
/// Its row is the person's own, <c>exit</c> on the step's row (<see cref="StepExit"/>), never the steward's.
/// <list type="bullet">
/// <item><b>The interruption</b> is the row's: the desktop's announcements say it once under its key (<c>WaitAnnouncements.ExitKind</c>),
/// a toast in a window in front and the OS balloon behind it, naming the run, the step, the story's short form and the one answer. Its memory
/// is the home's (<see cref="Daoris.Driver.News.ClaimStanding"/>), so a restart says an exit still standing never again.</item>
/// <item><b>The record</b> is a notice of <i>Done and failed</i> under its own key (<see cref="Key"/>), naming the run and the step, which no
/// window says aloud: the whole story (what it is, who held it and for how long, why each could not, and the one answer) is the look's line
/// said once, as the exit is kept (<see cref="Line"/>), and the notice is handed up again at each look while the exit stands
/// (<see cref="News"/>, <see cref="NodeRuntimePass.Standing"/>), so a look a later failure ended loses none, and a write of a key held
/// keeps one; one the owner dismissed is not kept again, whatever restarts.</item>
/// </list>
/// </summary>
public static class FinalExits
{
    /// <summary>One holder in the story: who held the step, since when, and why they could not.</summary>
    public sealed record ExitHolder(string Who, DateTimeOffset? Since, string WhyNot);

    /// <summary>A spent red's exit: its rounds went back to the work and nobody else holds it.</summary>
    public const string Spent = "spent";

    /// <summary>A vacancy's exit: no agent's session answered it and nobody took it from <i>Vacant</i>.</summary>
    public const string Vacant = "vacant";

    /// <summary>An exit of a wait a live session served and never answered within its bound.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>The exit's own key, its notice's and its row's obligation: the step's attempt in its run.</summary>
    public static string Key(string run, string node, int attempt) => $"exit:{run}/{node}#{attempt}";

    /// <summary>The exit's story as the look says it once, as the exit is kept: a news line of its notice (<see cref="News"/>).</summary>
    public static LookLine Line(string run, string node, int attempt, string? workspace, string story) =>
        new(story, LookClassed.Noticed("work", News(run, node, attempt, workspace)));

    /// <summary>The exit's notice: keyed by <see cref="Key"/> in the run's workspace, naming the run and the step.</summary>
    public static LookNews News(string run, string node, int attempt, string? workspace) =>
        new(LookSaid.FinalExit, Key(run, node, attempt), Quest: run, Workspace: workspace, Act: node);

    /// <summary>The notices of the exits standing among <paramref name="steps"/> (<see cref="HeldStep.Exit"/>), handed up at each look.</summary>
    public static IReadOnlyList<LookNews> Standing(IEnumerable<HeldStep> steps) =>
        [.. steps.Where(step => step.Exit is not null).Select(step => new LookNews(LookSaid.FinalExit, step.Exit!.Key, Quest: step.Run, Workspace: step.Workspace, Act: step.Node))];

    /// <summary>The answer a step at the final exit asks of the owner, where its own presses settle it.</summary>
    public const string Answer = "settle it with its own press in its run, or end this work";

    public static string Notice(string what, IReadOnlyList<ExitHolder> held, string answer, DateTimeOffset now) =>
        $"final exit, to you: {what}. "
        + string.Join("; ", held.Select(each => $"{each.Who} held it{For(each.Since, now)} and could not: {each.WhyNot.TrimEnd('.')}"))
        + $". It waits in What needs you until you look; your one answer: {answer}.";

    /// <summary>How long a holder held it, in whole hours or minutes; nothing where the moment is not known.</summary>
    public static string For(DateTimeOffset? since, DateTimeOffset now)
    {
        if (since is not { } from || now <= from) return "";
        var span = now - from;
        return span.TotalHours >= 1 ? $" for {(int)span.TotalHours} hours" : $" for {Math.Max(1, (int)span.TotalMinutes)} minutes";
    }
}

/// <summary>A step's row at the owner's final exit: its key (<see cref="FinalExits.Key"/>) and which holders failed (<see cref="FinalExits.Spent"/>, <see cref="FinalExits.Vacant"/>, <see cref="FinalExits.Unanswered"/>).</summary>
public sealed record StepExit(string Key, string Kind);
