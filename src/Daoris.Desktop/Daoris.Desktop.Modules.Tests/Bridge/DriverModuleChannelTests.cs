using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SESSONE2c (D167 points 6 to 8; the co-working design §2.4, D50): the page's door onto an ask's channel, the screen twin of
/// <c>daoris-driver ask channel</c>. <c>CHANNEL_READ</c> answers the channel as the person reads it (their own mark, an open
/// question marked), <c>CHANNEL_POST</c> posts their words and answers the service's code where it refused, and <c>CHANNEL_MARK</c>
/// moves their mark. The service is a stand-in reached through the real client; nothing machine-local is on the answer.
/// </summary>
public sealed class DriverModuleChannelTests : DriverModuleBridge
{
    private const string Channel = """
        {"ask":"a1","entry":"person","items":[
         {"kind":"line","at":"2026-10-14T09:00:00Z","code":"published","quest":"q1","name":"Read the figure","from":"ask #a1","to":"db"},
         {"kind":"message","at":"2026-10-14T09:02:00Z","id":"m1","person":false,"session":"s1","repository":"db","agent":"claude-code",
          "entry":"ask:a1/db","question":true,"open":true,"text":"Which column holds the figure?"}],
         "unread":2,"mark":"2026-10-14T08:00:00Z"}
        """;

    private async Task<(DriverModule Module, StandIn Service)> UpAsync(StandIn standIn)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(standIn)));
        return (new DriverModule(Bus, loop), standIn);
    }

    /// <summary>The read is the person's own, unless an entry is named, and moves no mark; each item is the page's shape.</summary>
    [Fact]
    public async Task CHANNEL_READ_answers_the_channel_as_the_person_reads_it()
    {
        var (module, service) = await UpAsync(new StandIn(_ => (HttpStatusCode.OK, Channel)));

        var read = await AnswerAsync(module, "CHANNEL_READ", new { ask = "#a1" });

        Assert.Equal(["GET /api/asks/a1/channel"], service.Requests);
        Assert.Equal(("a1", "person", 2), (read.GetProperty("ask").GetString(), read.GetProperty("entry").GetString(), read.GetProperty("unread").GetInt32()));
        var items = read.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal("published", items[0].GetProperty("code").GetString());
        Assert.Equal("db", items[0].GetProperty("to").GetString());
        Assert.True(items[1].GetProperty("open").GetBoolean());
        Assert.Equal("ask:a1/db", items[1].GetProperty("entry").GetString());
        Assert.Equal("2026-10-14T08:00:00", read.GetProperty("mark").GetString()![..19]);
    }

    /// <summary>A named entry's read and a tail after a moment go to the door as its query.</summary>
    [Fact]
    public async Task CHANNEL_READ_passes_an_entry_and_a_since_on()
    {
        var (module, service) = await UpAsync(new StandIn(_ => (HttpStatusCode.OK, Channel)));

        await AnswerAsync(module, "CHANNEL_READ", new { ask = "a1", entry = "ask:a1/db", since = "2026-10-14T09:01:00Z" });

        Assert.StartsWith("GET /api/asks/a1/channel?entry=ask%3Aa1%2Fdb&since=2026-10-14T09%3A01%3A00", Assert.Single(service.Requests));
    }

    /// <summary>An ask the host does not hold, or a host from before the channel, reads as a channel with nothing on it.</summary>
    [Fact]
    public async Task CHANNEL_READ_of_an_ask_with_no_channel_is_an_empty_one()
    {
        var (module, _) = await UpAsync(new StandIn(_ => null));

        var read = await AnswerAsync(module, "CHANNEL_READ", new { ask = "a9" });

        Assert.Equal(0, read.GetProperty("items").GetArrayLength());
        Assert.Equal(0, read.GetProperty("unread").GetInt32());
    }

    /// <summary>The post carries the person's words, to and re, and answers the message kept.</summary>
    [Fact]
    public async Task CHANNEL_POST_posts_the_persons_words()
    {
        var (module, service) = await UpAsync(new StandIn(_ => (HttpStatusCode.OK,
            """{"message":{"kind":"message","at":"2026-10-14T09:06:00Z","id":"m2","person":true,"to":"db","re":"m1","text":"The v3 one."}}""")));

        var posted = await AnswerAsync(module, "CHANNEL_POST", new { ask = "a1", text = "The v3 one.", to = "db", re = "m1" });

        Assert.Equal(["POST /api/asks/a1/channel"], service.Requests);
        Assert.Contains("\"to\":\"db\"", service.Bodies[0]);
        Assert.True(posted.GetProperty("posted").GetBoolean());
        Assert.Equal("m2", posted.GetProperty("message").GetProperty("id").GetString());
        Assert.True(posted.GetProperty("message").GetProperty("person").GetBoolean());
    }

    /// <summary>A refusal is an answer with its code, which the page words in the reader's language, and a post without words is sent as it is.</summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("too-long")]
    [InlineData("bad-to")]
    [InlineData("bad-re")]
    [InlineData("not-found")]
    public async Task CHANNEL_POST_answers_a_refusal_as_its_code(string code)
    {
        var (module, _) = await UpAsync(new StandIn(_ => (HttpStatusCode.BadRequest, $$"""{"error":"{{code}}","code":"{{code}}"}""")));

        var posted = await AnswerAsync(module, "CHANNEL_POST", new { ask = "a1", text = "hello" });

        Assert.False(posted.GetProperty("posted").GetBoolean());
        Assert.Equal(code, posted.GetProperty("code").GetString());
    }

    /// <summary>The mark moves through the moment named, or the newest, and the answer is the channel as it then reads.</summary>
    [Fact]
    public async Task CHANNEL_MARK_moves_the_persons_mark()
    {
        var (module, service) = await UpAsync(new StandIn(_ => (HttpStatusCode.OK, Channel)));

        var read = await AnswerAsync(module, "CHANNEL_MARK", new { ask = "a1", through = "2026-10-14T09:02:00Z" });

        Assert.Equal(["POST /api/asks/a1/channel/read"], service.Requests);
        Assert.Contains("\"through\":\"2026-10-14T09:02:00", service.Bodies[0]);
        Assert.Equal(2, read.GetProperty("unread").GetInt32());
    }

    /// <summary>Before the driver's service answers, each door says the cold-start sentence.</summary>
    [Fact]
    public async Task The_channel_before_the_service_answers_says_so()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "CHANNEL_READ", new { ask = "a1" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "CHANNEL_POST", new { ask = "a1", text = "hi" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "CHANNEL_MARK", new { ask = "a1" }));
    }

    /// <summary>An ask named by nothing is the driver's refusal, said verbatim.</summary>
    [Fact]
    public async Task A_post_with_no_ask_is_refused_in_the_drivers_words()
    {
        var (module, _) = await UpAsync(new StandIn(_ => (HttpStatusCode.OK, Channel)));

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "CHANNEL_POST", new { ask = " ", text = "hi" }));
    }

    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Requests) Requests.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, """{"error":"none"}""");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
