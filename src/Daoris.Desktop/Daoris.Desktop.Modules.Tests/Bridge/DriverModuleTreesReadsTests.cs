using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// RPC1b2 and RPCOPEN1 (the terminal RPC design §7.3): the trees reads' routes answer the verb's words (<c>said</c>) beside or in place
/// of the page's fields where the verb asks for them (<c>said: true</c>), and the page, which asks for none, is answered as it always
/// was. The routes stay open reads (<c>PipeCensusTests</c>); these hold what a verb that is their client would send and print.
/// </summary>
public sealed class DriverModuleTreesReadsTests : DriverModuleBridge
{
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

    [Fact]
    public async Task The_clean_ups_list_says_its_words_where_asked_and_the_page_is_answered_as_before()
    {
        var module = await ComeUpAsync();

        var page = await AnswerAsync(module, "SWEEP_PLAN", new { });
        var verb = await AnswerAsync(module, "SWEEP_PLAN", new { workspace = (string?)null, said = true });
        var named = await AnswerAsync(module, "SWEEP_PLAN", new { workspace = "aurora", said = true });

        Assert.Equal(["branches", "landed"], page.EnumerateObject().Select(each => each.Name));
        Assert.Equal(["branches", "landed", "said"], verb.EnumerateObject().Select(each => each.Name));
        Assert.Equal([TreesCommand.NothingToClean(new TreesAsk(TreesLook.Clean))], Said(verb).Out);
        Assert.Empty(Said(verb).Error);
        Assert.Equal(0, Said(verb).Code);
        Assert.Equal([TreesCommand.NothingToClean(new TreesAsk(TreesLook.Clean) { Workspace = "aurora" })], Said(named).Out);
    }

    [Fact]
    public async Task The_syncs_list_says_its_words_where_asked_and_the_page_is_answered_as_before()
    {
        var module = await ComeUpAsync();

        var page = await AnswerAsync(module, "TREES_SYNC_PLAN", new { });
        var verb = await AnswerAsync(module, "TREES_SYNC_PLAN", new { repository = (string?)null, workspace = (string?)null, all = false, said = true });
        var all = await AnswerAsync(module, "TREES_SYNC_PLAN", new { all = true, said = true });

        Assert.Equal(["lines", "rebases", "deletes", "looked", "apart"], page.EnumerateObject().Select(each => each.Name));
        Assert.Equal(["lines", "rebases", "deletes", "looked", "apart", "said"], verb.EnumerateObject().Select(each => each.Name));
        Assert.Equal([TreesCommand.NoneHeld(new TreesAsk(TreesLook.Sync))], Said(verb).Out);
        Assert.Empty(Said(verb).Error);
        Assert.Equal(0, Said(verb).Code);
        Assert.Equal([TreesCommand.NoneHeld(new TreesAsk(TreesLook.Sync) { All = true })], Said(all).Out);
    }

    [Fact]
    public async Task A_plan_of_a_session_with_no_tree_is_the_verbs_sentence_where_asked_and_the_pages_refusal_where_not()
    {
        var module = await ComeUpAsync();

        var verb = await AnswerAsync(module, "LANDING", new { id = "s1a2b3c4", branch = (string?)null, said = true });

        Assert.Equal(["session", "said"], verb.EnumerateObject().Select(each => each.Name));
        Assert.Equal(TreesWords.NoTreeToLand("s1a2b3c4").Error, Said(verb).Error);
        Assert.Equal(1, Said(verb).Code);
        Assert.Contains("no working tree", await RefusalAsync(module, "LANDING", new { id = "s1a2b3c4" }));
    }

    [Fact]
    public async Task A_plan_of_a_landing_the_review_reads_as_landed_says_why_and_lands_nothing_again()
    {
        var landed = new LandedBranch("engine", "aurora", "daoris/s-landed00", "main", "abc123", "s1a2b3c4", null, "work", DateTimeOffset.UtcNow);
        new SessionTrees(Home).Recorded.Record(landed);

        var answer = await AnswerAsync(await ComeUpAsync(), "LANDING", new { id = "s1a2b3c4", said = true });

        var (output, error, code) = Said(answer);
        Assert.Single(output);
        Assert.StartsWith("trees: ", output[0]);
        Assert.Empty(error);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task A_hand_off_plan_of_a_branch_no_landing_made_is_the_verbs_refusal_where_asked_and_the_pages_shape_where_not()
    {
        var module = await ComeUpAsync();

        var verb = await AnswerAsync(module, "HANDOFF_PLAN", new { branch = "feature/x", repository = "engine", plugin = "example.lands", said = true });
        var page = await AnswerAsync(module, "HANDOFF_PLAN", new { branch = "feature/x" });

        Assert.Equal([$"trees: {TreeHand.NotLanded("feature/x")}"], Said(verb).Error);
        Assert.Equal(1, Said(verb).Code);
        Assert.Equal(["session", "branch"], page.EnumerateObject().Select(each => each.Name));
    }

    [Fact]
    public async Task An_entrys_and_an_asks_plan_say_there_is_no_work_where_asked_as_the_verb_does()
    {
        var module = await ComeUpAsync();

        var entry = await AnswerAsync(module, "ENTRY_LANDING", new { entry = "e-1", said = true });
        var ask = await AnswerAsync(module, "ASK_LANDING", new { ask = "a1b2c3", said = true });

        Assert.Equal(["said"], entry.EnumerateObject().Select(each => each.Name));
        Assert.Equal(TreesWords.EntryNone(isAsk: false, "e-1").Error, Said(entry).Error);
        Assert.Equal(TreesWords.EntryNone(isAsk: true, "a1b2c3").Error, Said(ask).Error);
        Assert.Equal(1, Said(entry).Code);
        Assert.Equal(1, Said(ask).Code);
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
