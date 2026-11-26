using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest's class is one of four words, set as a recorded event with its why and who, read at each look: its own,
/// else its ask's, else normal. Urgent carries an end and a workspace holds at most so many at once.
/// </summary>
public sealed class PriorityBookTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 11, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "priority", Guid.NewGuid().ToString("N")[..8]);

    public PriorityBookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static PriorityEvent Event(string target, string id, string @class, string workspace = "games", DateTimeOffset? until = null, string why = "it matters") =>
        new(Now, target, id, workspace, @class, why, "the person") { Until = until };

    private string? Set(PriorityEvent @event, DateTimeOffset? now = null, PriorityRules? rules = null) =>
        PriorityBook.Set(_home, @event, now ?? Now, rules ?? PriorityRules.Default);

    [Fact]
    public void A_quest_reads_normal_until_set_then_its_ask_s_class_and_then_its_own()
    {
        Assert.Equal(new PriorityReading(PriorityClasses.Normal, PriorityReading.Unset, null), PriorityBook.ClassOf(PriorityBook.Read(_home), "q1", "a1", Now));

        Assert.Null(Set(Event(PriorityBook.Ask, "a1", PriorityClasses.High)));
        var fromAsk = PriorityBook.ClassOf(PriorityBook.Read(_home), "q1", "a1", Now);
        Assert.Equal((PriorityClasses.High, PriorityBook.Ask), (fromAsk.Class, fromAsk.Source));

        Assert.Null(Set(Event(PriorityBook.Quest, "q1", PriorityClasses.Low)));
        var own = PriorityBook.ClassOf(PriorityBook.Read(_home), "q1", "a1", Now);
        Assert.Equal((PriorityClasses.Low, PriorityBook.Quest, "it matters", "the person"), (own.Class, own.Source, own.Event!.Why, own.Event.By));
    }

    [Fact]
    public void Urgent_ends_at_its_end_and_the_class_set_before_it_stands_again()
    {
        Assert.Null(Set(Event(PriorityBook.Quest, "q1", PriorityClasses.High)));
        Assert.Null(Set(Event(PriorityBook.Quest, "q1", PriorityClasses.Urgent, until: Now.AddHours(2)) with { At = Now.AddMinutes(1) }));

        Assert.Equal(PriorityClasses.Urgent, PriorityBook.ClassOf(PriorityBook.Read(_home), "q1", null, Now.AddHours(1)).Class);
        Assert.Equal(PriorityClasses.High, PriorityBook.ClassOf(PriorityBook.Read(_home), "q1", null, Now.AddHours(3)).Class);
    }

    [Fact]
    public void Urgent_needs_an_end_after_now_a_why_and_one_of_the_four_words()
    {
        Assert.Contains("an end", Set(Event(PriorityBook.Quest, "q1", PriorityClasses.Urgent)));
        Assert.Contains("end must be after now", Set(Event(PriorityBook.Quest, "q1", PriorityClasses.Urgent, until: Now.AddMinutes(-1))));
        Assert.Contains("why", Set(Event(PriorityBook.Quest, "q1", PriorityClasses.High, why: "  ")));
        Assert.Contains("urgent, high, normal or low", Set(Event(PriorityBook.Quest, "q1", "critical")));
        Assert.Empty(PriorityBook.Read(_home));
    }

    [Fact]
    public void A_workspace_holds_at_most_its_cap_of_urgent_at_once_and_the_refusal_names_them()
    {
        var rules = new PriorityRules(UrgentCap: 2, UrgentLife: TimeSpan.FromHours(24));
        Assert.Null(Set(Event(PriorityBook.Quest, "q1", PriorityClasses.Urgent, until: Now.AddHours(4)), rules: rules));
        Assert.Null(Set(Event(PriorityBook.Ask, "a2", PriorityClasses.Urgent, until: Now.AddHours(4)), rules: rules));

        var refused = Set(Event(PriorityBook.Quest, "q3", PriorityClasses.Urgent, until: Now.AddHours(4)), rules: rules);
        Assert.NotNull(refused);
        Assert.Contains("quest #q1", refused);
        Assert.Contains("ask #a2", refused);

        // Another workspace has its own count, one already urgent is set again, and one that ended frees its place.
        Assert.Null(Set(Event(PriorityBook.Quest, "q4", PriorityClasses.Urgent, workspace: "tools", until: Now.AddHours(4)), rules: rules));
        Assert.Null(Set(Event(PriorityBook.Quest, "q1", PriorityClasses.Urgent, until: Now.AddHours(6)), rules: rules));
        Assert.Null(Set(Event(PriorityBook.Quest, "q3", PriorityClasses.Urgent, until: Now.AddHours(9)), now: Now.AddHours(7), rules: rules));
    }

    [Fact]
    public void Each_change_is_kept_in_the_order_made_with_its_why_who_and_end()
    {
        Assert.Null(Set(Event(PriorityBook.Quest, "q1", PriorityClasses.Urgent, until: Now.AddHours(2), why: "the fix that slows every session")));
        Assert.Null(Set(Event(PriorityBook.Quest, "q1", PriorityClasses.Normal, why: "landed") with { At = Now.AddHours(1), By = "session s-1" }));

        var kept = PriorityBook.Read(_home);
        Assert.Equal([PriorityClasses.Urgent, PriorityClasses.Normal], kept.Select(each => each.Class));
        Assert.Equal(("the fix that slows every session", "the person", Now.AddHours(2)), (kept[0].Why, kept[0].By, kept[0].Until));
        Assert.Equal(("session s-1", (DateTimeOffset?)null), (kept[1].By, kept[1].Until));
        Assert.Equal(PriorityClasses.Normal, PriorityBook.ClassOf(kept, "q1", null, Now.AddHours(1.5)).Class);
    }

    [Fact]
    public void A_run_reads_the_highest_class_of_its_chain_s_quests_so_a_later_step_set_urgent_counts()
    {
        Assert.Null(Set(Event(PriorityBook.Quest, "q2", PriorityClasses.Urgent, until: Now.AddHours(2)) with { Run = "q1" }));
        Assert.Null(Set(Event(PriorityBook.Quest, "q3", PriorityClasses.Low) with { Run = "q1" }));
        Assert.Null(Set(Event(PriorityBook.Ask, "a1", PriorityClasses.High)));

        var run = PriorityBook.OfRun(PriorityBook.Read(_home), "q1", "a1", Now);
        Assert.Equal((PriorityClasses.Urgent, PriorityBook.Quest, "q2"), (run.Class, run.Source, run.Event!.Id));

        // Once the urgent step ends, the run reads its chain's other quests, the low one set on its own.
        Assert.Equal(PriorityClasses.Low, PriorityBook.OfRun(PriorityBook.Read(_home), "q1", "a1", Now.AddHours(3)).Class);
        Assert.Equal(PriorityClasses.High, PriorityBook.OfRun(PriorityBook.Read(_home), "q9", "a1", Now).Class);
    }

    [Fact]
    public void A_later_change_ends_an_earlier_urgent_so_the_cap_still_holds_when_the_later_change_ends()
    {
        var rules = new PriorityRules(UrgentCap: 2, UrgentLife: TimeSpan.FromHours(24));
        Assert.Null(Set(Event(PriorityBook.Quest, "a", PriorityClasses.Urgent, until: Now.AddDays(2)), rules: rules));
        Assert.Null(Set(Event(PriorityBook.Quest, "a", PriorityClasses.Low, until: Now.AddHours(1)) with { At = Now.AddMinutes(1) }, rules: rules));
        Assert.Null(Set(Event(PriorityBook.Quest, "b", PriorityClasses.Urgent, until: Now.AddDays(1)), rules: rules));
        Assert.Null(Set(Event(PriorityBook.Quest, "c", PriorityClasses.Urgent, until: Now.AddDays(1)), rules: rules));

        var later = Now.AddHours(2);
        Assert.Equal(PriorityClasses.Normal, PriorityBook.ClassOf(PriorityBook.Read(_home), "a", null, later).Class);
        Assert.Equal(2, new[] { "a", "b", "c" }.Count(id => PriorityBook.ClassOf(PriorityBook.Read(_home), id, null, later).Class == PriorityClasses.Urgent));
    }

    [Fact]
    public void A_past_end_on_a_class_that_is_not_urgent_is_said_as_a_past_end()
    {
        var refused = Set(Event(PriorityBook.Quest, "q1", PriorityClasses.High, until: Now.AddMinutes(-1)));

        Assert.NotNull(refused);
        Assert.DoesNotContain("urgent", refused);
        Assert.Contains("after now", refused);
    }

    [Fact]
    public void A_file_that_does_not_read_is_no_class_set()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PriorityBook.PathOf(_home))!);
        File.WriteAllText(PriorityBook.PathOf(_home), "{ not json");

        Assert.Empty(PriorityBook.Read(_home));
        Assert.Equal(PriorityClasses.Normal, PriorityBook.ClassOf(PriorityBook.Read(_home), "q1", null, Now).Class);
    }

    [Fact]
    public void The_four_classes_rank_urgent_first_and_low_last()
    {
        Assert.Equal([PriorityClasses.Urgent, PriorityClasses.High, PriorityClasses.Normal, PriorityClasses.Low], PriorityClasses.All);
        Assert.True(PriorityClasses.Rank(PriorityClasses.Urgent) < PriorityClasses.Rank(PriorityClasses.High));
        Assert.True(PriorityClasses.Rank(PriorityClasses.Normal) < PriorityClasses.Rank(PriorityClasses.Low));
        Assert.Equal(PriorityClasses.High, PriorityClasses.Parse(" High "));
        Assert.Null(PriorityClasses.Parse("critical"));
    }
}
