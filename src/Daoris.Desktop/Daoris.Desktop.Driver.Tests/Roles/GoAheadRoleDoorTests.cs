using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris ask go-ahead</c> in a role's terminal admits by the role the terminal holds: a go-ahead the door lists for the holder (one a
/// role granted <c>goahead.answer</c> may answer at that place) is answered as the role's decision, one it does not list is refused with
/// why and nothing is kept, and the answer says by whom.
/// </summary>
public sealed class GoAheadRoleDoorTests
{
    private static JsonElement Listed(string kind, string subjectField, string subjectId, string generation = "g-1") =>
        JsonDocument.Parse(
            $$"""{"kind":"{{kind}}","subject":{"session":"s1","{{subjectField}}":"{{subjectId}}"},"repository":"reports","generation":"{{generation}}","acts":["answer"]}""")
            .RootElement.Clone();

    private static IReadOnlyList<JsonElement> Waiting(params JsonElement[] listed) => listed;

    /// <summary>A caller whose role the door lists the go-ahead for is admitted: its answer is the role's decision on that subject, pinned to the listing's generation.</summary>
    [Theory]
    [InlineData("a7", true, "run the put", "run the put", SupervisorDoorActs.Approved)]
    [InlineData("#a7", false, "  not yet  ", "not yet", SupervisorDoorActs.Refused)]
    [InlineData("a7", true, null, "answered at the terminal, with no words", SupervisorDoorActs.Approved)]
    public void A_go_ahead_the_role_may_answer_is_answered_as_the_roles_decision(string ask, bool approved, string? words, string reason, string choice)
    {
        var answer = GoAheadRoleDoor.Of(Waiting(Listed("goahead", "goahead", "a7/2")), ask, 2, approved, words, out var refusal);

        Assert.Null(refusal);
        Assert.Equal("s1", answer!.Session);
        var draft = answer.Draft;
        Assert.Equal(("goahead", "a7/2", "g-1", SupervisorKinds.Answer, choice, null), (draft.Kind, draft.Subject, draft.Generation, draft.Do, draft.Choice, draft.Bound));
        Assert.Equal([new SupervisorReason(reason, GoAheadRoleDoor.Source)], draft.Reasons);
    }

    /// <summary>A caller whose role the door does not list it for (answered since, a sign-in, an act the risk policy classes the owner's, no grant) is refused with why, keeping nothing.</summary>
    [Theory]
    [InlineData("goahead", "goahead", "a7/3", "a7", 2, "has go-ahead 3 waiting for this holder, not 2")]
    [InlineData("goahead", "goahead", "a7/2", "A7", 2, "an ask is named as written, and `#a7` does")]
    [InlineData("question", "question", "a7/2", "a7", 2, "waits for this holder as a question, not a go-ahead")]
    [InlineData("goahead", "goahead", "a8/2", "a7", 2, "is not among what waits for the roles this terminal holds: it is answered already")]
    public void A_go_ahead_no_role_the_holder_holds_may_answer_is_refused_with_its_own_why(string kind, string field, string id, string ask, int number, string why)
    {
        var answer = GoAheadRoleDoor.Of(Waiting(Listed(kind, field, id)), ask, number, approved: true, "go", out var refusal);

        Assert.Null(answer);
        Assert.Contains(why, refusal);
        Assert.Contains("Nothing was kept", refusal);
        Assert.Null(GoAheadRoleDoor.Of([], ask, number, approved: true, "go", out var none));
        Assert.Contains("goahead.answer", none);
    }

    /// <summary>A go-ahead listed with no generation says so, never that it waits for no role; a pending read is not an absent Daoris.</summary>
    [Fact]
    public void A_listed_go_ahead_with_no_generation_and_a_read_still_pending_say_their_own_reasons()
    {
        var bare = JsonDocument.Parse("""{"kind":"goahead","subject":{"session":"s1","goahead":"a7/2"},"repository":"reports","acts":["answer"]}""").RootElement.Clone();

        Assert.Null(GoAheadRoleDoor.Of(Waiting(bare), "a7", 2, approved: true, "go", out var refusal));
        Assert.Contains("no generation", refusal);
        Assert.DoesNotContain("waits for no role", refusal);
        Assert.Contains("not running here", GoAheadRoleDoor.NoAnswer(pending: false, TimeSpan.FromSeconds(5)));
        var pending = GoAheadRoleDoor.NoAnswer(pending: true, TimeSpan.FromSeconds(5));
        Assert.Contains("running here but has not yet read", pending);
        Assert.DoesNotContain("not running here", pending);
    }

    /// <summary>The answer says by whom: the roles the holder holds and its holder.</summary>
    [Fact]
    public void The_answer_says_by_whom_it_was_given()
    {
        Assert.Equal("role `reports-supervisor`, held by this terminal", GoAheadRoleDoor.By(["reports-supervisor"]));
        Assert.Equal("role `a`, `b`, held by this terminal", GoAheadRoleDoor.By(["a", "b"]));
        Assert.Equal("a role held by this terminal", GoAheadRoleDoor.By([]));
    }

    /// <summary>The decide form takes a go-ahead's approved or refused as its choice, as the role's door answers it; any other act of the kind takes none.</summary>
    [Fact]
    public void The_decide_form_takes_a_go_aheads_choice()
    {
        Assert.True(SupervisorDecideForm.Chooses(SupervisorKinds.GoAhead, SupervisorKinds.Answer));
        var form = new SupervisorDecideForm(SupervisorSubject.GoAhead("s1", "a7", 2), "g-1", "", SupervisorKinds.Answer, SupervisorDoorActs.Approved,
            [new SupervisorReason("go", GoAheadRoleDoor.Source)]).Proved(HolderKey.Draw(), HolderKey.Draw());

        var read = SupervisorDecideForm.Read(JsonSerializer.SerializeToElement(form.Payload()));

        Assert.Equal((form.Subject, SupervisorDoorActs.Approved), (read!.Subject, read.Choice));
    }

    /// <summary>A role's terminal runs the go-ahead's answer and no other ask verb: it still never acts as the owner.</summary>
    [Fact]
    public void A_role_terminal_runs_the_go_ahead_answer_alone_of_the_ask_verbs()
    {
        Assert.True(RoleTerminal.Runs(["ask", "go-ahead", "a7", "2", "approve"]));
        Assert.False(RoleTerminal.Runs(["ask", "publish", "a7"]));
        Assert.False(RoleTerminal.Runs(["ask", "delete", "a7"]));
    }
}
