using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// RPC1b3 (the terminal RPC design §7.3, D171's RPC1b note): the trees presses' routes take what their verbs name and answer the
/// verb's words (<c>said</c>) beside the page's own fields, which keep their names and their values. The routes stay the person's
/// (<c>PipeCensusTests</c>); these hold what a verb that becomes their client would send and print.
/// </summary>
public sealed class DriverModuleTreesWordsTests : DriverModuleBridge
{
    /// <summary>A module whose driver is up, over a stand-in service holding no repository and no session.</summary>
    private async Task<DriverModule> ComeUpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));
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

    [Fact]
    public async Task A_discard_naming_neither_a_session_nor_a_path_is_refused_as_before()
    {
        var refusal = await RefusalAsync(await ComeUpAsync(), "DISCARD_SESSION_TREE", new { force = true });

        Assert.Contains("id", refusal);
    }

    /// <summary><c>trees hand</c>: a landed branch named by the branch (and its repository), where the page names a session.</summary>
    [Fact]
    public async Task A_hand_off_names_a_branch_and_says_what_refused_it_in_the_terminals_words()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "HANDOFF", new { branch = "feature/x", repository = "engine", plugin = "example.lands" });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(TreeHand.NotLanded("feature/x"), answer.GetProperty("message").GetString());
        var (output, error, code) = Said(answer);
        Assert.Empty(output);
        Assert.Equal([$"trees: {TreeHand.NotLanded("feature/x")}"], error);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task A_hand_off_by_session_answers_what_the_page_reads_and_the_words_beside_it()
    {
        var answer = await AnswerAsync(await ComeUpAsync(), "HANDOFF", new { id = "s1a2b3c4" });

        Assert.Equal("s1a2b3c4", answer.GetProperty("session").GetString());
        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(TreeHand.NotLanded("s1a2b3c4"), answer.GetProperty("message").GetString());
        Assert.Equal(1, Said(answer).Code);
    }

    [Fact]
    public async Task A_hand_off_naming_nothing_is_refused()
    {
        var refusal = await RefusalAsync(await ComeUpAsync(), "HANDOFF", new { plugin = "example.lands" });

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

    private sealed class StandInService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
