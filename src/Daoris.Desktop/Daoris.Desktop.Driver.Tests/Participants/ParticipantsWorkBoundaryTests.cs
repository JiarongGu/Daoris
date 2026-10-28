using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xii part 2, the Participants cuts of the three families above it (Review, Work orders and Workflows): the behaviour they
/// keep, written against the surface that exists on both sides of them (what the looks' judges keep for the plan, a loop's reserved and
/// bound starts, a conversation's chat run read at a turn's end and at a spawn, and the helper pass's opening line), so the same cases
/// pass on the base and after it. The seams the cuts add are <c>ParticipantsAbovePortsTests</c>'.
/// </summary>
public sealed class ParticipantsWorkBoundaryTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "participants-work-boundary", Guid.NewGuid().ToString("N")[..8]);
    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    public ParticipantsWorkBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    // ——— What the looks' judges keep for the plan

    private static OpinionAct Owed() => new(null!, "key", "claim", [], "landing", Person: false, null!);

    private static OpinionFact Found(Dictionary<string, OpinionSit> sits, OpinionAct? act = null) => new(sits, null, act);

    private static Dictionary<string, OpinionSit> Sits(params string[] quests) =>
        quests.ToDictionary(quest => quest, quest => new OpinionSit($"sits {quest}", new SittingFact("opinion")), StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void An_opinion_judged_is_kept_by_its_due_in_any_case_and_moves_the_plan_only_when_its_sits_or_its_act_do()
    {
        var facts = new LookFacts();
        Assert.Null(facts.Opinion("due-1"));

        var first = Found(Sits("q1"));
        Assert.True(facts.KeepOpinion("due-1", first));
        Assert.Same(first, facts.Opinion("DUE-1"));

        Assert.False(facts.KeepOpinion("due-1", Found(Sits("Q1"))));
        Assert.True(facts.KeepOpinion("due-1", Found(Sits("q1", "q2"))));
        Assert.True(facts.KeepOpinion("due-1", Found(Sits("q1", "q2"), Owed())));
    }

    [Fact]
    public void A_pass_found_owed_is_taken_once_and_what_else_the_judgment_found_stays()
    {
        var facts = new LookFacts();
        var act = Owed();
        facts.KeepOpinion("due-1", Found(Sits("q1"), act));

        Assert.Same(act, facts.TakeAct("due-1"));
        Assert.Null(facts.TakeAct("due-1"));
        Assert.Null(facts.TakeAct("due-2"));
        var kept = Assert.IsType<OpinionFact>(facts.Opinion("due-1"));
        Assert.Equal(["q1"], kept.Sits.Keys);
        Assert.Null(kept.Act);
    }

    [Fact]
    public void Only_the_opinions_still_owed_are_kept()
    {
        var facts = new LookFacts();
        facts.KeepOpinion("due-1", Found(Sits("q1")));
        facts.KeepOpinion("due-2", Found(Sits("q2")));

        facts.KeepOpinionsOf(["DUE-2"]);

        Assert.Null(facts.Opinion("due-1"));
        Assert.NotNull(facts.Opinion("due-2"));
    }

    // ——— A loop's starts, reserved and bound

    [Fact]
    public void A_start_is_reserved_unless_the_loop_holds_its_quest_its_ask_or_its_tree_and_is_reserved_again_once_released()
    {
        var running = new RunningSessions();
        var first = new StartReservation("q1", null, "engine", @"C:\trees\one");

        Assert.True(running.TryReserve(first));
        Assert.False(running.TryReserve(new StartReservation("Q1", null, "engine", null)));
        Assert.False(running.TryReserve(new StartReservation(null, null, "engine", "c:/trees/ONE/")));
        Assert.True(running.TryReserve(new StartReservation("q2", "a1", "engine", @"C:\trees\two")));
        Assert.False(running.TryReserve(new StartReservation(null, "A1", "ask #a1", null)));
        Assert.True(running.TryReserve(new StartReservation(null, null, "engine", null)));
        Assert.True(running.TryReserve(new StartReservation(null, null, "engine", null)));
        Assert.Equal(4, running.Reserved.Count);

        running.Release(first);
        running.Release(first);

        Assert.DoesNotContain(first, running.Reserved);
        Assert.True(running.TryReserve(new StartReservation("q1", null, "engine", @"C:\trees\one")));
    }

    [Fact]
    public void The_runs_the_starts_bound_are_handed_on_once_in_the_order_they_were_kept()
    {
        var running = new RunningSessions();
        var one = new WorkflowBind(null!, WorkflowBindCame.Bound, "q1", null, null);
        var two = new WorkflowBind(null!, WorkflowBindCame.Failed, "q2", null, "the binding could not be kept");

        running.Bound(one);
        running.Bound(two);

        var taken = running.TakeBound();
        Assert.Equal(2, taken.Count);
        Assert.Same(one, taken[0]);
        Assert.Same(two, taken[1]);
        Assert.Empty(running.TakeBound());
    }

    // ——— A conversation's chat run, read where the service's records say it

    private (ChatRunBinds Binds, Requests Seen, string Tree) Binds(string recordJson)
    {
        var tree = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees), "t1");
        var seen = new Requests(recordJson.Replace("{tree}", JsonEncodedText.Encode(tree).ToString()));
        var service = new ServiceClient("http://stand-in", null, new HttpClient(seen));
        return (new ChatRunBinds(_home, service, new SessionEvents(Sessions)), seen, tree);
    }

    private const string ChatRecord = """[{"id":"c1a2b3c4","repository":"engine","state":"running","kind":"chat","tree":"{tree}"}]""";

    [Fact]
    public async Task A_turn_s_end_binds_the_run_of_a_chat_in_a_tree_this_home_opened_once_from_the_service_s_records()
    {
        var (binds, seen, tree) = Binds(ChatRecord);
        var config = DriverConfig.Parse("""{"sessionChecks":{"engine":"light"}}""");

        await binds.TurnEndedAsync("c1a2b3c4", tree, config);

        Assert.True(WorkflowRunBindings.Read(_home, "chat-c1a2b3c4")!.IsCurrent);
        Assert.Equal(["/api/sessions", "/api/registry"], seen.Paths);

        await binds.TurnEndedAsync("c1a2b3c4", tree, config);

        Assert.Equal(2, seen.Paths.Count);
    }

    [Fact]
    public async Task A_turn_s_end_binds_nothing_for_a_tree_this_home_did_not_open_or_a_record_that_serves_a_quest()
    {
        var (binds, seen, _) = Binds(ChatRecord);
        await binds.TurnEndedAsync("c1a2b3c4", Path.Combine(_home, "elsewhere"), DriverConfig.Empty);
        Assert.Empty(seen.Paths);
        Assert.False(WorkflowRunBindings.Bound(_home, "chat-c1a2b3c4"));

        var (served, servedSeen, tree) = Binds("""[{"id":"c1a2b3c4","repository":"engine","state":"running","kind":"chat","tree":"{tree}","quest":"abcdef012345"}]""");
        await served.TurnEndedAsync("c1a2b3c4", tree, DriverConfig.Empty);
        Assert.Equal(["/api/sessions"], servedSeen.Paths);
        Assert.False(WorkflowRunBindings.Bound(_home, "chat-c1a2b3c4"));
    }

    [Fact]
    public async Task A_spawn_in_a_chat_run_answers_findings_only_where_its_process_names_a_second_opinion()
    {
        var (binds, _, tree) = Binds(ChatRecord);

        Assert.True(await binds.AnswersAsync("c1a2b3c4", tree, DriverConfig.Parse("""{"opinions":{"engine":{"reviewers":["codex-acp"]}}}""")));
        Assert.False(await binds.AnswersAsync("c1a2b3c4", tree, DriverConfig.Empty));
        Assert.False(await binds.AnswersAsync("c1a2b3c4", Path.Combine(_home, "elsewhere"), DriverConfig.Parse("""{"opinions":{"engine":{"reviewers":["codex-acp"]}}}""")));
        Assert.False(await binds.AnswersAsync("zzzzzzzz", tree, DriverConfig.Parse("""{"opinions":{"engine":{"reviewers":["codex-acp"]}}}""")));
    }

    [Fact]
    public async Task A_service_that_does_not_answer_leaves_the_turn_s_end_and_the_spawn_unread()
    {
        var tree = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Trees), "t1");
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Requests(null)));
        var binds = new ChatRunBinds(_home, service, new SessionEvents(Sessions));

        await binds.TurnEndedAsync("c1a2b3c4", tree, DriverConfig.Empty);

        Assert.False(WorkflowRunBindings.Bound(_home, "chat-c1a2b3c4"));
        Assert.False(await binds.AnswersAsync("c1a2b3c4", tree, DriverConfig.Parse("""{"opinions":{"engine":{"reviewers":["codex-acp"]}}}""")));
    }

    private sealed class Requests(string? records) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (records is null) throw new HttpRequestException("the stand-in answers nothing");
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(path == "/api/sessions" ? records : "[]", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    // ——— The helper pass's opening line

    [Fact]
    public void A_helper_s_transcript_opens_by_what_it_reads_where_and_what_held_it_read_only()
    {
        Assert.Equal(
            "— a helper for a session working in `engine`, at `abcdef0`, read-only by its agent's rules and its copy: it reads in a copy of its own, and nothing in it is taken back.",
            HelperPass.Opening("engine", "abcdef0123456", OpinionPosture.RulesAndCopy));
        Assert.Equal(
            "— a helper for a session working in `engine`, at `abcdef0`, read-only by its copy alone: it reads in a copy of its own, and nothing in it is taken back.",
            HelperPass.Opening("engine", "abcdef0123456", OpinionPosture.CopyAlone));
    }

    // ——— The reviewer a helper or a partner is chosen as, and the two words the helper's rule spells

    [Fact]
    public void A_helper_s_rule_says_where_it_was_set_in_the_words_the_commit_rules_spell()
    {
        Assert.Equal(CommitRules.RepositorySource, OpinionSource.Repository);
        Assert.Equal(CommitRules.WorkspaceSource, OpinionSource.Workspace);
        Assert.Equal("repository", OpinionSource.Repository);
        Assert.Equal("workspace", OpinionSource.Workspace);
    }
}
