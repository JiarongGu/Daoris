using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// RPC1b3 (the terminal RPC design §7.3, D171's RPC1b note): the trees presses' routes take what their verbs name and answer the
/// verb's words (<c>said</c>) beside the page's own fields, which keep their names and their values. The routes stay the person's
/// (<c>PipeCensusTests</c>); these hold what a verb that becomes their client would send and print.
/// </summary>
public sealed class DriverModuleTreesWordsTests : DriverModuleBridge
{
    public DriverModuleTreesWordsTests() : base("tree-lock-route") { }

    /// <summary>A module whose driver is up, over a stand-in service holding no repository and no session.</summary>
    private async Task<DriverModule> ComeUpAsync(string? tree = null, string state = "working")
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandInService(tree, state)))));
        return module;
    }

    private static (string[] Out, string[] Error, int Code) Said(JsonElement answer)
    {
        var said = answer.GetProperty("said");
        return ([.. said.GetProperty("out").EnumerateArray().Select(line => line.GetString()!)],
            [.. said.GetProperty("error").EnumerateArray().Select(line => line.GetString()!)], said.GetProperty("code").GetInt32());
    }

    /// <summary><c>trees remove &lt;path&gt;</c>: the tree named by its path, as the verb names it, and not by a session.</summary>
    [Fact]
    public async Task A_tree_named_by_its_path_is_discarded_by_the_route_as_the_verb_does_and_said_in_its_words()
    {
        var module = await ComeUpAsync();
        var folder = Path.Combine(Home, "not-a-tree");
        Directory.CreateDirectory(folder);

        var answer = await AnswerAsync(module, "DISCARD_SESSION_TREE", new { path = folder });

        Assert.False(answer.GetProperty("done").GetBoolean());
        var message = answer.GetProperty("message").GetString()!;
        var (output, error, code) = Said(answer);
        Assert.Equal([$"trees: {message}"], output);
        Assert.Empty(error);
        Assert.Equal(1, code);
        Assert.True(Directory.Exists(folder), "a refusal removes nothing");
    }

    [Theory]
    [InlineData(false, "working")]
    [InlineData(true, "working")]
    [InlineData(false, "awaiting-person")]
    [InlineData(true, "awaiting-person")]
    public async Task A_forced_discard_by_path_or_session_keeps_a_tree_the_local_ledger_holds(bool byPath, string state)
    {
        var tree = DaorisHome.Entry(Home, HomeEntry.Trees, "aurora", "engine", "s-12345678");
        Directory.CreateDirectory(tree);
        var module = await ComeUpAsync(tree, state);
        var answer = byPath
            ? await AnswerAsync(module, "DISCARD_SESSION_TREE", new { path = tree, force = true })
            : await AnswerAsync(module, "DISCARD_SESSION_TREE", new { id = "s1a2b3c4", force = true });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(SessionBranchDiscard.Held("engine", "daoris/s-12345678"), answer.GetProperty("message").GetString());
        Assert.True(Directory.Exists(tree));
        Assert.Equal(1, Said(answer).Code);
    }

    [Fact]
    public async Task A_discard_naming_neither_a_session_nor_a_path_is_refused_as_before()
    {
        var refusal = await RefusalAsync(await ComeUpAsync(), "DISCARD_SESSION_TREE", new { force = true });

        Assert.Contains("id", refusal);
    }

    /// <summary><c>trees clean --yes --workspace</c>: the press takes one workspace's checkouts alone, refusing one the registry does not name.</summary>
    [Fact]
    public async Task A_clean_up_press_takes_a_workspace_and_refuses_one_the_registry_does_not_name()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "SWEEP", new { workspace = "aurora" });

        Assert.Equal(0, answer.GetProperty("removed").GetInt32());
        var (output, error, code) = Said(answer);
        Assert.Empty(output);
        Assert.Equal(["trees: there is no workspace `aurora` on this machine: no repository is registered here."], error);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task A_clean_up_press_the_page_sends_keeps_its_fields_and_adds_the_words()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "SWEEP", new { only = Array.Empty<string>() });

        Assert.Equal(["results", "landed", "folders", "removed", "said"], answer.EnumerateObject().Select(each => each.Name));
        Assert.Equal(["trees: removed 0 of 0."], Said(answer).Out);
        Assert.Equal(0, Said(answer).Code);
    }

    /// <summary><c>trees sync --yes</c>: fetches, takes the verb's scope, and says what was left apart and what the registry followed.</summary>
    [Fact]
    public async Task A_sync_press_that_takes_no_checkout_says_so_as_the_verb_does()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "TREES_SYNC", new { fetch = true, repository = "game", follow = true });

        var (output, error, code) = Said(answer);
        Assert.Empty(output);
        Assert.Equal([TreesCommand.NothingToSync(new TreesAsk(TreesLook.Sync) { Repository = "game" })], error);
        Assert.Equal(1, code);
        Assert.Empty(answer.GetProperty("apart").EnumerateArray());
        Assert.Empty(answer.GetProperty("followed").EnumerateArray());
    }

    [Fact]
    public async Task A_sync_press_the_page_sends_keeps_its_fields_and_adds_the_words_apart_and_followed()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "TREES_SYNC", new { only = new[] { "engine:main" } });

        Assert.Equal(["lines", "rebases", "deletes", "changed", "apart", "followed", "said"], answer.EnumerateObject().Select(each => each.Name));
        Assert.Equal(0, answer.GetProperty("changed").GetInt32());
    }

    /// <summary><c>trees remove &lt;branch&gt;</c>: the other discard answers the same words, from the driver's one discard.</summary>
    [Fact]
    public async Task A_discarded_session_branch_is_said_in_the_words_of_the_remove_verb()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "DISCARD_SESSION_BRANCH", new { repository = "engine", branch = "daoris/s-x", force = true });

        var message = answer.GetProperty("message").GetString()!;
        Assert.Equal(TreesWords.Remove(new TreeRemoval(answer.GetProperty("done").GetBoolean(), message)).Out, Said(answer).Out);
        Assert.Equal(answer.GetProperty("done").GetBoolean() ? 0 : 1, Said(answer).Code);
    }

    /// <summary><c>trees remove &lt;landing branch&gt; --why</c> (LANDRETIRE1): the route's answer says what the verb says, a refusal as words, exit 1.</summary>
    [Fact]
    public async Task A_withdrawn_landing_branch_is_said_in_the_words_of_the_remove_verb()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "WITHDRAW_LANDING_BRANCH", new { repository = "engine", branch = "quest/row-1", why = "by hand" });

        var message = answer.GetProperty("message").GetString()!;
        var withdrawal = new LandedWithdrawal(answer.GetProperty("done").GetBoolean(), message);
        Assert.Equal(TreesWords.Withdraw(withdrawal).Out, Said(answer).Out);
        Assert.Equal([$"trees: {message}"], Said(answer).Out);
        Assert.Equal(1, Said(answer).Code);
    }

    /// <summary><c>trees remove &lt;named&gt;</c> (RPC1b4): a session or a branch by one name, with the repository only where it must say.</summary>
    [Fact]
    public async Task A_branch_discard_naming_a_branch_or_a_session_says_what_the_verb_says()
    {
        var module = await ComeUpAsync();

        var branch = await AnswerAsync(module, "DISCARD_SESSION_BRANCH", new { named = "s-1a2b3c4d", force = true });
        var (output, _, code) = Said(branch);
        const string unrecorded = "`s-1a2b3c4d` names no session branch this machine recorded — say which repository holds it with `--repository <name>`.";
        Assert.Equal([$"trees: {unrecorded}"], output);
        Assert.Equal(1, code);
        Assert.False(branch.GetProperty("done").GetBoolean());
        Assert.Equal(unrecorded, branch.GetProperty("message").GetString());

        var session = await AnswerAsync(module, "DISCARD_SESSION_BRANCH", new { named = "s1a2b3c4", force = true });
        Assert.Equal(["trees: session `s1a2b3c4` names no working tree on this machine, so it left no session branch here."], Said(session).Out);
        Assert.Equal(1, Said(session).Code);
    }

    [Fact]
    public async Task A_branch_discard_naming_nothing_is_refused_as_before()
    {
        var refusal = await RefusalAsync(await ComeUpAsync(), "DISCARD_SESSION_BRANCH", new { force = true });

        Assert.Contains("repository", refusal);
    }

    /// <summary><c>trees land --entry</c> and <c>--ask</c> (RPC1b4): the verbs' words beside the page's fields, a refusal as words.</summary>
    [Fact]
    public async Task An_entry_naming_no_work_here_is_said_as_the_verb_says_it()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "LAND_ENTRY", new { entry = "e-1", leaveOut = new[] { "s2" } });

        Assert.False(answer.GetProperty("done").GetBoolean());
        var (output, error, code) = Said(answer);
        Assert.Empty(output);
        Assert.Equal(TreesWords.EntryNone(isAsk: false, "e-1").Error, error);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task An_ask_naming_no_work_here_is_said_as_the_verb_says_it_and_keeps_the_pages_fields()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "LAND_ASK", new { ask = "a1b2c3" });

        Assert.Equal(["ask", "done", "repositories", "said", "followed"], answer.EnumerateObject().Select(each => each.Name));
        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(TreesWords.EntryNone(isAsk: true, "a1b2c3").Error, Said(answer).Error);
        Assert.Equal(1, Said(answer).Code);
    }

    /// <summary>RPC1b5: the landing presses answer the lines the registry followed, none where nothing landed or none was asked for.</summary>
    [Fact]
    public async Task The_landing_presses_answer_followed_beside_the_words_and_follow_nothing_where_nothing_landed()
    {
        RegistryFollowing.Moved(Home, "engine", DateTimeOffset.UtcNow);
        var module = await ComeUpAsync();

        var entry = await AnswerAsync(module, "LAND_ENTRY", new { entry = "e-1", follow = true });
        var ask = await AnswerAsync(module, "LAND_ASK", new { ask = "a1b2c3", follow = true });
        var session = await AnswerAsync(module, "LAND_SESSION_TREE", new { id = "s1a2b3c4", follow = true });

        foreach (var answer in new[] { entry, ask, session })
        {
            Assert.Empty(answer.GetProperty("followed").EnumerateArray());
            Assert.Empty(Said(answer).Out);
        }

        Assert.Equal(["ask", "done", "repositories", "said", "followed"], ask.EnumerateObject().Select(each => each.Name));
        Assert.Equal(1, Said(entry).Code);
        Assert.Equal(["engine"], RegistryFollowing.Due(Home));
    }

    /// <summary><c>trees land &lt;session&gt;</c> (RPC1b4): the refusals the verb says as words, where the route threw or landed nothing said.</summary>
    [Fact]
    public async Task A_landing_of_a_session_with_no_tree_is_the_verbs_sentence_exit_1_not_an_error()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "LAND_SESSION_TREE", new { id = "s1a2b3c4" });

        Assert.False(answer.GetProperty("done").GetBoolean());
        var (output, error, code) = Said(answer);
        Assert.Empty(output);
        Assert.Equal(TreesWords.NoTreeToLand("s1a2b3c4").Error, error);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task A_landing_the_review_already_reads_as_landed_is_not_again_exit_1()
    {
        new SessionTrees(Home).Recorded.Record(new LandedBranch(
            "engine", "aurora", "daoris/s-landed00", "main", "abc123", "s1a2b3c4", null, "work", DateTimeOffset.UtcNow));

        var answer = await AnswerAsync(await ComeUpAsync(), "LAND_SESSION_TREE", new { id = "s1a2b3c4" });

        Assert.False(answer.GetProperty("done").GetBoolean());
        var (output, error, code) = Said(answer);
        Assert.Single(output);
        Assert.StartsWith("trees: ", output[0]);
        Assert.Contains("Nothing was landed again: its tree is gone.", output[0]);
        Assert.Empty(error);
        Assert.Equal(1, code);
        Assert.Contains("Nothing was landed again", answer.GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_plan_of_a_session_with_no_tree_is_still_the_refusal_the_page_reads()
    {
        var refusal = await RefusalAsync(await ComeUpAsync(), "LANDING", new { id = "s1a2b3c4" });

        Assert.Contains("no working tree", refusal);
    }

    /// <summary>
    /// TREENAME1: a tree's name given where the doors take a session names the session whose tree it is, at the press, the plan, the
    /// discard and the review, as the terminal's alone path says it; never that the session names no tree here.
    /// </summary>
    [Fact]
    public async Task A_trees_name_given_for_a_session_names_the_session_whose_tree_it_is_at_every_tree_door()
    {
        var module = await ComeUpAsync(DaorisHome.Entry(Home, HomeEntry.Trees, "default", "engine", "s-92ed2c8c"));
        var sentence = $"`s-92ed2c8c` is a tree's name, and this verb takes the session: that tree is session `s1a2b3c4`'s, so say `s1a2b3c4`.";

        var press = await AnswerAsync(module, "LAND_SESSION_TREE", new { id = "s-92ed2c8c" });
        Assert.False(press.GetProperty("done").GetBoolean());
        Assert.Equal([$"trees: {sentence}"], Said(press).Error);
        Assert.Equal([$"trees: {sentence}"], Said(await AnswerAsync(module, "LANDING", new { id = "s-92ed2c8c", said = true })).Error);

        foreach (var (route, payload) in new (string, object)[]
                 {
                     ("LANDING", new { id = "s-92ed2c8c" }), ("DISCARD_SESSION_TREE", new { id = "s-92ed2c8c" }), ("SESSION_DIFF", new { id = "s-92ed2c8c" }),
                 })
        {
            var refusal = await RefusalAsync(module, route, payload);
            Assert.Contains("that tree is session `s1a2b3c4`'s", refusal);
            Assert.DoesNotContain("no working tree", refusal);
        }
    }

    private sealed class StandInService(string? tree = null, string state = "working") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(tree is not null && request.RequestUri!.AbsolutePath == "/api/sessions"
                    ? JsonSerializer.Serialize(new[] { new { id = "s1a2b3c4", repository = "engine", tree, state } }) : "[]",
                    System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
