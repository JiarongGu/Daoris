using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4): the page's doors onto an operation bringing sessions together. <c>TOGETHER</c> answers
/// it for the head and for each member, with no tree on the wire; <c>TOGETHER_LET_GO</c> ends it before the landing, and
/// <c>TOGETHER_ASK_AGAIN</c> is refused where no check failed. A refusal is data, never thrown.
/// </summary>
public sealed class DriverModuleTogetherTests : DriverModuleBridge
{
    private async Task<DriverModule> UpAsync()
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new NoService())));
        return new DriverModule(Bus, loop);
    }

    private TogetherOperation Opened()
    {
        var trees = Path.Combine(Home, "trees-here");
        var (opened, refused) = new TogetherOperations(Home).Open("engine",
            new TogetherHead("s-head", "s-head", Path.Combine(trees, "head"), "daoris/head", "a1"),
            [new TogetherMember("s-beside", Path.Combine(trees, "beside"), "daoris/beside", "b0", "b1")], DateTimeOffset.UtcNow);
        Assert.Null(refused);
        return opened!;
    }

    [Fact]
    public async Task TOGETHER_answers_the_operation_for_its_head_and_its_member_with_no_tree_on_the_wire()
    {
        var module = await UpAsync();
        var operation = Opened();

        var head = (await AnswerAsync(module, "TOGETHER", new { session = "s-head" })).GetProperty("operation");
        var member = (await AnswerAsync(module, "TOGETHER", new { session = "s-beside" })).GetProperty("operation");
        var none = await AnswerAsync(module, "TOGETHER", new { session = "s-other" });

        Assert.Equal((operation.Id, "asked", "head"), (head.GetProperty("id").GetString(), head.GetProperty("phase").GetString(), head.GetProperty("role").GetString()));
        Assert.Equal(("member", "s-head"), (member.GetProperty("role").GetString(), member.GetProperty("head").GetString()));
        Assert.Equal(("s-beside", "b1"), (head.GetProperty("members")[0].GetProperty("session").GetString(), head.GetProperty("members")[0].GetProperty("tip").GetString()));
        Assert.DoesNotContain("trees-here", head.GetRawText());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, none.GetProperty("operation").ValueKind);
    }

    [Fact]
    public async Task TOGETHER_LET_GO_ends_it_before_the_landing_and_ASK_AGAIN_is_refused_where_no_check_failed()
    {
        var module = await UpAsync();
        var operation = Opened();

        var asked = await AnswerAsync(module, "TOGETHER_ASK_AGAIN", new { session = "s-head" });
        var member = await AnswerAsync(module, "TOGETHER_LET_GO", new { session = "s-beside" });
        var let = await AnswerAsync(module, "TOGETHER_LET_GO", new { session = "s-head" });

        Assert.False(asked.GetProperty("done").GetBoolean());
        Assert.Contains("has not failed", asked.GetProperty("refused").GetString());
        Assert.False(member.GetProperty("done").GetBoolean());
        Assert.True(let.GetProperty("done").GetBoolean());
        Assert.Equal(TogetherPhase.LetGo, new TogetherOperations(Home).Of(operation.Id)!.Phase);
    }

    private sealed class NoService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":"none"}""", Encoding.UTF8, "application/json") });
    }
}
