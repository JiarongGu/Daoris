using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The room's <see cref="HelpRoomGrowth"/>: which place a request calls for, in order, and who is asked what;
/// the two presses that open one, named in <see cref="HelpRoomVerbs"/>; and what each mode does with them, said in
/// <see cref="HelpRoomMayDo"/> beside the one rule that neither is a family of acts a person grants.
/// </summary>
public sealed class HelpRoomGrowthTests
{
    private static HelpMachine Full => HelpRoomFixture.Machine with { Mode = HelpModeNames.Full };

    private static string Growth => Flat(new HelpRoomGrowth().Render(HelpRoomFixture.Machine));

    /// <summary>The text as one line of words, so a sentence the room wraps is found whole.</summary>
    private static string Flat(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

    /// <summary>The seven answers are one table, each by the words that tell it apart, in the order the first that holds wins.</summary>
    [Fact]
    public void The_table_answers_in_the_order_the_design_gives_and_names_the_press_of_each_place()
    {
        string[] answers =
        [
            "| A registered department owns the work",
            "| The work is a new part of a department's code",
            "| The same, but the part needs instructions, gates or a claim group of its own",
            "| The work is its own deliverable",
            "| The work is files that want no history or branches",
            "| A system reached only through a plugin's console",
            "| The request only names a thing, asks for a plan or an answer, or is a throwaway",
        ];
        var at = -1;
        foreach (var answer in answers)
        {
            var next = Growth.IndexOf(answer, StringComparison.Ordinal);
            Assert.True(next > at, $"`{answer}` is missing or out of order");
            at = next;
        }

        Assert.Contains("| That department: an ask, `daoris ask --workspace <name> \"…\"`", Growth);
        Assert.Contains("| A room in that department: `daoris place <repository> --room <path> --purpose \"…\" --apply`", Growth);
        Assert.Contains("| A new repository: `daoris repository create <name> --purpose \"…\"`", Growth);
        Assert.Contains("| A folder department: `daoris repository create <name> --kind folder --purpose \"…\"`", Growth);
        Assert.Contains("| Not this: a system's adapter is the plugin's, which the person installs", Growth);
        Assert.Contains("| Nothing is created: scratch is a repository's `_fixtures` or a gitignored folder, and a plan is words", Growth);
    }

    /// <summary>The earlier row wins, the person's own words win over the table, and what is asked of the person is theirs alone.</summary>
    [Fact]
    public void The_earlier_row_wins_the_persons_words_go_ahead_and_they_are_asked_only_what_is_theirs()
    {
        Assert.Contains("Where two rows could hold, the earlier wins: an existing department over a room, a room over a new repository.", Growth);
        Assert.Contains("The person's explicit words win over the table (\"make me a new repository for this\"), with the press's refusals still applying.", Growth);
        Assert.Contains("Ask the person only what is theirs: a name where the request gives none that fits, a workspace where the work spans two,", Growth);
        Assert.Contains("a folder where the workspace has no repository to infer one from.", Growth);
    }

    /// <summary>It reads the registry and the records, never guesses, and says in its turn which places it looked at and why none owns the work.</summary>
    [Fact]
    public void The_agent_reads_the_registry_and_says_which_places_it_looked_at_and_why_none_owns_the_work()
    {
        Assert.Contains("read the registry's departments (each one's `domain`: what it `owns` and `accepts`, in every workspace) and search the knowledge", Growth);
        Assert.Contains("say in your answer which places you looked at and why none owns the work", Growth);
    }

    /// <summary>Any agent opens a place through the one press, its tier deciding; the intake never does.</summary>
    [Fact]
    public void Any_agent_opens_one_through_the_same_press_its_tier_deciding_and_the_intake_never_does()
    {
        Assert.Contains("Any agent opens a place through these presses, you, a lead or a driven session, its tier deciding as for every press.", Growth);
        Assert.Contains("The intake never creates one: where no department owns an ask it says so.", Growth);
    }

    /// <summary>The press ends before any content, and the work follows as quests to the place's own session.</summary>
    [Fact]
    public void The_press_ends_before_content_and_the_work_follows_as_quests_to_the_places_own_session()
    {
        Assert.Contains("stops before any content: the folder, an empty history with no remote, the registration, the drive and the quests", Growth);
        Assert.Contains("`--work \"…\"` carries the person's request as the quest that follows the set-up", Growth);
        Assert.Contains("You write no file of the new department's, no `daoris.json`, no brief, no code", Growth);
        Assert.Contains("A room is never made by a press: `--room` asks the repository's own session for one quest", Growth);
    }

    /// <summary>A refusal is the route's own words, answered at once and never shown on a card; the agent says it and works inside what exists.</summary>
    [Fact]
    public void A_refusal_is_the_routes_own_words_answered_at_once_and_never_shown()
    {
        Assert.Contains("A press the route would refuse (a name registered already, a folder that exists and is not empty, a workspace that does not exist,", Growth);
        Assert.Contains("no git identity, a room's repository not adopted or held) is answered to you at once in the route's own words and never shown", Growth);
        Assert.Contains("say them, never reword them into a promise", Growth);
    }

    /// <summary>The section is one unit under <i>What you may do</i>, once, and both presses are verbs the room names.</summary>
    [Fact]
    public void The_table_sits_once_under_what_you_may_do_beside_the_verbs()
    {
        var room = Flat(HelpRoom.Render(HelpRoomFixture.Machine));

        Assert.Equal(1, Count(room, "### Opening a department or a room"));
        Assert.True(room.IndexOf("### The verbs you press", StringComparison.Ordinal) < room.IndexOf("### Opening a department or a room", StringComparison.Ordinal));
        Assert.True(room.IndexOf("## What you may do", StringComparison.Ordinal) < room.IndexOf("### Opening a department or a room", StringComparison.Ordinal));
        Assert.True(room.IndexOf("### Opening a department or a room", StringComparison.Ordinal) < room.IndexOf("## The doors", StringComparison.Ordinal));
    }

    /// <summary>The two presses are the verbs the room names, each with the flags its route takes and where its card is.</summary>
    [Fact]
    public void The_two_presses_are_verbs_the_room_names()
    {
        var verbs = Flat(new HelpRoomVerbs().Render(HelpRoomFixture.Machine));

        Assert.Contains("- A department is opened by pressing: `daoris repository create <name> --purpose \"…\" [--kind git|folder] [--workspace <name>] [--under <folder>] [--line <branch>] [--work \"…\"] [--plan]`", verbs);
        Assert.Contains("`--plan` shows what it would make and writes nothing, and asks no card.", verbs);
        Assert.Contains("- A room is asked of a repository by pressing: `daoris place <repository> --room <path> --purpose \"…\" --apply`, a part of the place's one set-up door", verbs);
    }

    /// <summary>In restricted mode each press waits on a card, and no grant of a family of acts changes it.</summary>
    [Fact]
    public void Restricted_mode_waits_on_a_card_whatever_families_are_granted()
    {
        foreach (var granted in new IReadOnlyList<string>[] { [], [.. ActFamilies.Built] })
        {
            var room = Flat(HelpRoom.Render(HelpRoomFixture.Machine with { Acts = granted }));

            Assert.Contains("Opening a department (`daoris repository create`) or asking for a room (`daoris place … --room`) waits on the person's card naming you, and runs on their yes.", room);
            Assert.Contains("It is in no family of acts the person can grant, so a grant never runs it unasked.", room);
        }
    }

    /// <summary>In full access the press runs at once as the person's own, said once and kept in the press record.</summary>
    [Fact]
    public void Full_access_runs_each_press_at_once_as_the_persons_own_said_once_and_logged()
    {
        var room = Flat(HelpRoom.Render(Full));

        Assert.Contains("Opening a department (`daoris repository create`) or asking for a room (`daoris place … --room`) runs at once as the person's own, said once on this conversation's console,", room);
        Assert.Contains("logged and kept in the press record the person reads in Notifications.", room);
        Assert.DoesNotContain("It is in no family of acts", room);
    }

    /// <summary>Neither route is a family's: a granted family never covers a creation, a room's quest or the plan that reads one.</summary>
    [Theory]
    [InlineData("REGISTRY_CREATE")]
    [InlineData("REGISTRY_CREATE_PLAN")]
    [InlineData("PLACE_APPLY")]
    [InlineData("PLACE_PLAN")]
    public void No_family_of_acts_covers_a_creation_or_a_rooms_press(string route)
    {
        Assert.DoesNotContain(route, ActFamilies.Built.SelectMany(ActFamilies.RoutesOf));
        Assert.False(ActFamilies.Covers(ActFamilies.Built, route));
    }

    private static int Count(string text, string word)
    {
        var count = 0;
        for (var at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + word.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
