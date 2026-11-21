namespace Daoris.Driver;

/// <summary>
/// The owner's final exit: where every holder of a run's step has failed (today the role's holder and the vacancy path), Daoris sends the
/// owner one notice carrying the whole story (what it is, who held it and for how long, why each could not, and the one answer the owner
/// gives), and the step then waits in <i>What needs you</i>, held by the person, until the owner looks. Nothing waits on the owner before it
/// unless the risk policy classes the act <c>owner</c>; there is no give-up in its place, none after it, and no second notice: each exit is
/// kept once, a spent red's on its telling (<see cref="NodeTold.Decided"/>), a vacancy's on its kept record (<see cref="StepVacancy.Exited"/>).
/// </summary>
public static class FinalExits
{
    /// <summary>One holder in the story: who held the step, since when, and why they could not.</summary>
    public sealed record ExitHolder(string Who, DateTimeOffset? Since, string WhyNot);

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
