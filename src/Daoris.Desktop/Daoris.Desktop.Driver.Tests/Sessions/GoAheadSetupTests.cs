using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTONOMY1c (D184 points 2, 3 and 5): go-aheads follow the repository's access setup. Under <c>named</c>, today's, a session asks one
/// before an act outside its repository or on a production system, and before one the repository or the quest names; under
/// <c>never</c>, and so under full access, it asks none of those. A go-ahead the quest or the person's own words demand is asked in
/// every setup, since the person wrote it there. On the install a session waited 41 minutes for go-aheads.
/// </summary>
public sealed class GoAheadSetupTests
{
    /// <summary>Today's sentence (AUTONOMY1e), which <c>named</c> keeps word for word.</summary>
    private const string Named =
        "a go-ahead for an act outside this repository or on a production system, or for one this repository or the quest says needs it.";

    /// <summary>What <c>never</c> says instead: only the person's own demand is asked.</summary>
    private const string Never =
        "a go-ahead only where the quest or the person's words demand one before an act. This repository's access setup asks no "
        + "other go-ahead: where the work needs an act on a production system or outside this repository, do it without asking "
        + "and name it under **Readings** in your closing note; a push, a publish or a release stays as the boundary below says.";

    private static SessionTarget Target(AskWords? words = null) =>
        new SessionTarget("abc123", "Fix the tile", "The dashboard figure reads zero.", "ask #a1", "dashboards", "C:/somewhere/dashboards",
            "http://stand-in") { Words = words };

    /// <summary>The three driven instructions: claiming, resuming after an answer, and carrying on after a cut-off.</summary>
    private static IEnumerable<(string Which, string Flat)> EveryInstruction(SessionTarget target) =>
        new[]
        {
            ("claiming", target),
            ("resuming", target with { Answered = new QuestView("q2", "dashboards", "metrics", "What does it read?", "Need it.", "Done") }),
            ("carrying on", target with { CutOff = "timed out after 30 minutes and was killed." }),
        }.Select(each => (each.Item1, Flat(TargetPrompt.Compose(each.Item2))));

    private static string Flat(string prompt) => System.Text.RegularExpressions.Regex.Replace(prompt, @"\s+", " ");

    private static SessionTarget Under(SessionTarget target, AccessSetting? repository, AccessSetting? machine = null) =>
        Daoris.Driver.Driver.WithAccess(target, AccessRules.ResolveLevels(repository, null, machine));

    private static AccessSetting Pauses(string pause, string value) =>
        new() { Pauses = new Dictionary<string, string>(StringComparer.Ordinal) { [pause] = value } };

    [Fact]
    public void Named_asks_the_go_aheads_it_asked_before()
    {
        foreach (var (which, flat) in EveryInstruction(Under(Target(), Pauses(AccessRules.GoAheads, AccessRules.Named))))
        {
            Assert.True(flat.Contains(Named, StringComparison.Ordinal), which);
            Assert.False(flat.Contains("asks no other go-ahead", StringComparison.Ordinal), which);
        }
    }

    [Fact]
    public void Never_asks_no_go_ahead_for_an_act_outside_the_repository_or_on_production()
    {
        foreach (var (which, flat) in EveryInstruction(Under(Target(), Pauses(AccessRules.GoAheads, AccessRules.Never))))
        {
            Assert.True(flat.Contains(Never, StringComparison.Ordinal), which);
            Assert.False(flat.Contains(Named, StringComparison.Ordinal), which);
            // The rest of what only the person can give is still asked.
            Assert.True(flat.Contains("a credential, an account or a sign-in;", StringComparison.Ordinal), which);
        }
    }

    [Fact]
    public void Full_access_asks_no_go_ahead_and_today_asks_them()
    {
        Assert.Contains(Never, Flat(TargetPrompt.Compose(Under(Target(), AccessSetting.FullAccess))));
        Assert.Contains(Never, Flat(TargetPrompt.Compose(Under(Target(), null, machine: AccessSetting.FullAccess))));
        Assert.Contains(Named, Flat(TargetPrompt.Compose(Under(Target(), null))));
        // A repository's own choice outranks the machine's full access.
        Assert.Contains(Named, Flat(TargetPrompt.Compose(Under(Target(), Pauses(AccessRules.GoAheads, AccessRules.Named), AccessSetting.FullAccess))));
    }

    /// <summary>
    /// D184 point 5: a go-ahead the ask itself demands is asked whatever the setup. The ask's words reach the session beneath the
    /// quest, verbatim, and under <c>never</c> the instruction still sends it to the person for what those words demand, on the ask.
    /// </summary>
    [Fact]
    public void Under_never_a_go_ahead_the_asks_own_words_demand_is_still_asked()
    {
        var demand = "Fix the tile, and ask me before you write anything to production.";
        var words = new AskWords("a1", [new AskWordView("asked", demand, new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero))]);

        foreach (var (which, flat) in EveryInstruction(Under(Target(words), AccessSetting.FullAccess)))
        {
            Assert.True(flat.Contains(demand, StringComparison.Ordinal), which);
            Assert.True(flat.Contains("a go-ahead only where the quest or the person's words demand one", StringComparison.Ordinal), which);
            Assert.True(flat.Contains("`go_ahead_ask`", StringComparison.Ordinal), which);
        }
    }
}
