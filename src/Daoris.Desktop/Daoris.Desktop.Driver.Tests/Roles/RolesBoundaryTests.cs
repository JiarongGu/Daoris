using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xi, Roles' cuts: the seams they add. The one-turn re-ask and the supervisor's quarantine read their owners' choice and
/// snapshot through facets Roles declares, the park's question is read by Roles' own parser, and the supervisor's print reads the question
/// file and the ask's words through the operations Work orders installs. The behaviour itself is the moved classes' own.
/// </summary>
[Collection("agent operation ports")]
public sealed class RolesBoundaryTests
{
    private static readonly ActorEffective Entry = new("claude-code-acp", "sonnet", null, "2");

    private sealed record Standing(ActorEffective? Effective, HarnessSelection? Selection = null) : IActorChoice<Standing>
    {
        public bool Chosen => Effective is not null;
        public IReadOnlyList<IActorTried> Tried => [];
        public Standing WithSelection(HarnessSelection? selection) => this with { Selection = selection };
    }

    private sealed record StandingPassed(ActorEffective Entry, string? Account) : IActorPassed<StandingPassed>
    {
        public static StandingPassed Of(ActorEffective entry, string? account) => new(entry, account);
    }

    [Fact]
    public async Task The_re_ask_passes_the_running_entry_and_asks_the_owner_s_walk_again_over_a_choice_of_any_owner()
    {
        var later = new Standing(Entry with { From = "3" });
        IReadOnlyList<StandingPassed>? asked = null;
        var reask = new OneTurnReask<Standing, StandingPassed>(new Standing(Entry), passed =>
        {
            asked = passed;
            return Task.FromResult(later);
        });

        Assert.Same(later, await reask.NextAsync());
        Assert.Same(later, reask.Current);
        Assert.Equal([new StandingPassed(Entry, null)], asked);
        Assert.Equal(asked, reask.Passed);

        // A choice that names no entry leaves nothing out, and the same account is not offered again.
        var none = new OneTurnReask<Standing, StandingPassed>(new Standing(null), _ => Task.FromResult(later));
        Assert.Null(await none.SameAccountAsync());
        Assert.Empty(none.Passed);
    }

    [Fact]
    public void The_reviewer_s_choice_and_try_answer_the_facets_and_the_reviewer_re_ask_keeps_its_name()
    {
        var tried = new ReviewerTried("codex", "label", ReviewerHeld.Cooling, "wait.") { Entry = Entry };
        var choice = new ReviewerChoice("claude", "fresh", null, "sentence") { Tried = [tried], Effective = Entry };
        IActorChoice<ReviewerChoice> facet = choice;

        Assert.True(facet.Chosen);
        Assert.Equal(Entry, facet.Effective);
        var held = Assert.Single(facet.Tried);
        Assert.Same(tried, held);
        Assert.Equal((Entry, ReviewerHeld.Cooling, "wait."), (held.Entry, held.Held, held.Refusal));
        Assert.Equal(choice, facet.WithSelection(null));

        Assert.IsAssignableFrom<OneTurnReask<ReviewerChoice, ReviewerPassed>>(new OneTurnReask(choice, _ => Task.FromResult(choice)));
    }

    [Fact]
    public void The_planner_s_snapshot_answers_the_supervisor_s_look_and_keeps_the_rest_of_itself()
    {
        var snapshot = new Snapshot([], [], []);
        ISupervisorLook<Snapshot> look = snapshot;
        var held = new HashSet<string> { "s1" };
        var chose = new Dictionary<string, string> { ["s1"] = "Monthly" };

        var supervised = look.WithSupervisor(held, chose);
        Assert.Same(held, supervised.SupervisorHeld);
        Assert.Same(chose, supervised.SupervisorChose);
        Assert.Same(snapshot.LastRun, supervised.LastRun);

        var lastRun = new Dictionary<string, PriorSession>();
        var named = new Dictionary<string, PriorSession>();
        var recorded = look.WithRecords(lastRun, named);
        Assert.Same(lastRun, recorded.LastRun);
        Assert.Same(named, recorded.NamedByReopen);
        Assert.Same(snapshot.SupervisorHeld, recorded.SupervisorHeld);
    }

    [Fact]
    public void The_park_s_question_is_read_by_roles_and_the_file_reader_names_the_same_rule_and_bounds()
    {
        const string text = """{ "id": "k1", "question": "Monthly?", "choices": [{ "label": "Monthly", "does": "bills monthly" }, { "label": "" }], "recommend": 1, "why": "the ticket", "owner": "x" }""";
        var at = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

        var parsed = ParkQuestions.Parsed(text, "s1", () => at);
        Assert.Equal(parsed, QuestionFiles.Parsed(text, "s1", () => at) with { Choices = parsed!.Choices });
        Assert.Equal(("k1", "s1", "Monthly?", 1, "the ticket", "x", at), (parsed.Id, parsed.Session, parsed.Question, parsed.Recommend, parsed.Why, parsed.Owner, parsed.At));
        Assert.Equal([new ParkQuestionChoice("Monthly", "bills monthly")], parsed.Choices);
        Assert.Null(ParkQuestions.Parsed("""{ "id": "k1", "question": "  " }""", "s1", () => at));

        Assert.Equal(
            (1000, 6, 80, 300, 200, 300),
            (QuestionFiles.MaxQuestion, QuestionFiles.MaxChoices, QuestionFiles.MaxLabel, QuestionFiles.MaxDoes, QuestionFiles.MaxBy, QuestionFiles.MaxWhy));
        Assert.Equal(
            (ParkQuestions.MaxQuestion, ParkQuestions.MaxChoices, ParkQuestions.MaxLabel, ParkQuestions.MaxDoes, ParkQuestions.MaxBy, ParkQuestions.MaxWhy),
            (QuestionFiles.MaxQuestion, QuestionFiles.MaxChoices, QuestionFiles.MaxLabel, QuestionFiles.MaxDoes, QuestionFiles.MaxBy, QuestionFiles.MaxWhy));
    }

    [Fact]
    public async Task The_installed_operations_are_work_orders_and_a_missing_holder_is_required_where_reached()
    {
        Assert.IsType<RolesWorkOrdersOperations>(RolesWorkOrders.Current);

        // An ask no sender names reads as unread, and its words beneath the quest are the pure rule's.
        using var service = new ServiceClient("http://stand-in", null, new HttpClient());
        var words = await RolesWorkOrders.Current.AskWordsAsync(service, null, "q1", CancellationToken.None);
        Assert.Equal("unread", words.GoAheads);
        Assert.Equal(AskWordsText.Beneath(null, "q1"), words.Beneath);
        Assert.Null(RolesWorkOrders.Current.QuestionOf(Path.Combine(GitSeed.RepoRoot(), "_fixtures", "roles-no-home"), "not an id"));

        var actual = RolesWorkOrders.Current;
        try
        {
            RolesWorkOrders.Current = null!;
            var missing = Assert.Throws<InvalidOperationException>(() => RolesWorkOrders.Current);
            Assert.Contains("Work orders installs them", missing.Message, StringComparison.Ordinal);
        }
        finally { RolesWorkOrders.Current = actual; }
    }
}
