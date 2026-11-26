using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// *Hold waiting* on a quest: a recorded event with its why and who, read at each look beside the classes in the
/// same file: a quest's own newest word, else its ask's. A lift is an event too, so the history stays.
/// </summary>
public sealed class HoldBookTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 11, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "hold", Guid.NewGuid().ToString("N")[..8]);

    public HoldBookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static HoldEvent Hold(string target, string id, bool held = true, int minutes = 0, string? run = null) =>
        new(Now.AddMinutes(minutes), target, id, "games", held, "make room", "the person") { Run = run };

    private bool Held(string run, string? ask = null) => HoldBook.IsHeld(HoldBook.Read(_home), run, ask);

    [Fact]
    public void A_quest_is_held_from_its_hold_to_its_lift_and_nothing_else_is()
    {
        Assert.False(Held("q1"));

        Assert.Null(HoldBook.Set(_home, Hold(PriorityBook.Quest, "q1")));
        Assert.True(Held("q1"));
        Assert.False(Held("q2"));

        Assert.Null(HoldBook.Set(_home, Hold(PriorityBook.Quest, "q1", held: false, minutes: 5)));
        Assert.False(Held("q1"));
        Assert.Equal(2, HoldBook.Read(_home).Count);
    }

    [Fact]
    public void A_hold_on_an_ask_holds_its_quests_until_a_quest_s_own_lift_stands_over_it()
    {
        Assert.Null(HoldBook.Set(_home, Hold(PriorityBook.Ask, "a1")));
        Assert.True(Held("q1", "a1"));

        Assert.Null(HoldBook.Set(_home, Hold(PriorityBook.Quest, "q1", held: false, minutes: 1)));
        Assert.False(Held("q1", "a1"));
        Assert.True(Held("q2", "a1"));
    }

    [Fact]
    public void A_later_quest_of_a_chain_held_holds_its_run()
    {
        Assert.Null(HoldBook.Set(_home, Hold(PriorityBook.Quest, "q2", run: "q1")));

        Assert.True(Held("q1"));
    }

    [Fact]
    public void A_hold_needs_its_why_and_a_target()
    {
        Assert.NotNull(HoldBook.Set(_home, Hold(PriorityBook.Quest, "q1") with { Why = " " }));
        Assert.NotNull(HoldBook.Set(_home, Hold("repository", "q1")));
        Assert.Empty(HoldBook.Read(_home));
    }

    [Fact]
    public void A_class_set_after_a_hold_keeps_the_hold_and_a_hold_after_a_class_keeps_the_class()
    {
        Assert.Null(HoldBook.Set(_home, Hold(PriorityBook.Quest, "q1")));
        Assert.Null(PriorityBook.Set(_home, new PriorityEvent(Now, PriorityBook.Quest, "q1", "games", PriorityClasses.High, "it matters", "the person"), Now, PriorityRules.Default));
        Assert.Null(HoldBook.Set(_home, Hold(PriorityBook.Quest, "q2")));

        Assert.True(Held("q1"));
        Assert.True(Held("q2"));
        Assert.Equal(PriorityClasses.High, PriorityBook.ClassOf(PriorityBook.Read(_home), "q1", null, Now).Class);
    }
}
