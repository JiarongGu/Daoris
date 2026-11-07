using System.Net;
using System.Text;

namespace Daoris.Driver.Tests;

/// <summary>
/// An agent outside Daoris joins a channel at the terminal
/// under the person's Confirm, then posts, reads and leaves as the member it is admitted as, at its own doors of the service, never the
/// person's; a join while Daoris is closed asks nothing, since no Confirm can be asked.
/// </summary>
public sealed class ChannelMemberVerbTests
{
    private const string Member = "o1a2b3c4";

    [Fact]
    public void The_member_forms_are_read_apart_from_the_persons_channel_verb()
    {
        Assert.True(ChannelMemberVerb.Asks(["channel", "join", "a1"]));
        Assert.True(ChannelMemberVerb.Asks(["channel", "leave", "--member", Member]));
        Assert.True(ChannelMemberVerb.Asks(["channel", "--member", Member, "hello"]));
        Assert.False(ChannelMemberVerb.Asks(["channel", "a1", "hello"]));

        Assert.Equal(new ChannelMemberAsk(ChannelMemberAsk.Join, Ask: "a1", Agent: "codex", Repository: "checker"),
            ChannelMemberVerb.Read(["channel", "join", "#a1", "--agent", "codex", "--repository", "checker"], out var none));
        Assert.Null(none);
        Assert.Equal(new ChannelMemberAsk(ChannelMemberAsk.Join, Line: "s1a2b3c4"), ChannelMemberVerb.Read(["channel", "join", "--line", "s1a2b3c4"], out _));
        Assert.Equal(new ChannelMemberAsk(ChannelMemberAsk.Post, Member: Member, Text: "the check passes", To: "db", Question: true, Urgent: true),
            ChannelMemberVerb.Read(["channel", "--member", Member, "the", "check", "passes", "--to", "db", "--question", "--urgent"], out _));
        Assert.Equal(new ChannelMemberAsk(ChannelMemberAsk.Read, Member: Member), ChannelMemberVerb.Read(["channel", "--member", Member], out _));
        Assert.Equal(new ChannelMemberAsk(ChannelMemberAsk.Leave, Member: Member), ChannelMemberVerb.Read(["channel", "leave", "--member", Member], out _));

        Assert.Null(ChannelMemberVerb.Read(["channel", "join"], out var nothing));
        Assert.Contains("an ask's id or --line", nothing);
        Assert.Null(ChannelMemberVerb.Read(["channel", "join", "a1", "--line", "s1"], out _));
        Assert.Null(ChannelMemberVerb.Read(["channel", "leave"], out _));
        Assert.Null(ChannelMemberVerb.Read(["channel", "--member"], out _));
        Assert.Null(ChannelMemberVerb.Read(["channel", "--member", Member, "--urgent"], out var level));
        Assert.Contains("go with the words", level);
    }

    /// <summary>The verb holds no lease: its one write is the service's.</summary>
    [Fact]
    public void The_member_verbs_hold_no_lease()
    {
        Assert.False(VerbLeases.Holds(["ask", "channel", "join", "a1"]));
        Assert.False(VerbLeases.Holds(["ask", "channel", "--member", Member, "hello"]));
        Assert.False(VerbLeases.Holds(["ask", "channel", "leave", "--member", Member]));
    }

    /// <summary>A join with no running Daoris sends nothing and says why: no pipe serves, and no Confirm can be asked.</summary>
    [Fact]
    public async Task A_join_while_Daoris_is_closed_asks_nothing_and_exits_2()
    {
        var standIn = new StandIn(_ => null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        var exit = await ChannelMemberVerb.RunAsync(new ChannelMemberAsk(ChannelMemberAsk.Join, Ask: "a1"), service, output, instance: null);

        Assert.Equal(2, exit);
        Assert.Contains("Daoris is not running", output.ToString());
        Assert.Empty(standIn.Requests);
    }

    /// <summary>On the person's Confirm the shell presses the admission and the terminal hears the member's id with its two verbs.</summary>
    [Fact]
    public async Task The_confirmed_join_prints_the_members_id_and_its_verbs()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath == "/api/channel/members"
            ? (HttpStatusCode.OK, $$"""{"id":"{{Member}}","origin":"ask:a1","entry":"ask:a1/outside:{{Member}}","agent":"codex","repository":"checker","joined":"2026-11-06T09:00:00Z"}""")
            : null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        var exit = await ChannelMemberVerb.JoinAsync(new ChannelMemberAsk(ChannelMemberAsk.Join, Ask: "a1", Agent: "codex", Repository: "checker"), service, output);

        Assert.Equal(0, exit);
        var text = output.ToString();
        Assert.Contains($"You joined ask #a1's channel as member {Member}", text);
        Assert.Contains($"ask channel --member {Member} \"…\"", text);
        Assert.Contains($"ask channel leave --member {Member}", text);
        Assert.Contains("never the person's", text);
        Assert.Contains("\"ask\":\"a1\"", standIn.Bodies.Single(), StringComparison.Ordinal);
        Assert.Contains("\"agent\":\"codex\"", standIn.Bodies.Single(), StringComparison.Ordinal);
    }

    /// <summary>The member posts under its own id and reads at its own door, each line naming who said it; neither carries the person's key.</summary>
    [Fact]
    public async Task The_member_posts_and_reads_at_its_own_doors()
    {
        var standIn = new StandIn(request => request.Method == HttpMethod.Post
            ? (HttpStatusCode.OK, $$"""{"message":{"kind":"message","at":"2026-11-06T09:01:00Z","id":"m2","person":false,"session":"{{Member}}","agent":"codex","outside":true,"urgent":true,"text":"the check passes"} }""")
            : (HttpStatusCode.OK, $$"""
                {"member":"{{Member}}","origin":"ask:a1","ask":"a1","unread":2,"items":[
                  {"kind":"message","at":"2026-11-06T09:00:00Z","id":"m1","person":true,"text":"please check the export"},
                  {"kind":"message","at":"2026-11-06T09:01:00Z","id":"m2","person":false,"session":"{{Member}}","repository":"checker","agent":"codex","outside":true,"text":"the check passes"}]}
                """));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        Assert.Equal(0, await ChannelMemberVerb.RunAsync(new ChannelMemberAsk(ChannelMemberAsk.Post, Member: Member, Text: "the check passes", Urgent: true), service, output, instance: null));
        Assert.Equal(0, await ChannelMemberVerb.RunAsync(new ChannelMemberAsk(ChannelMemberAsk.Read, Member: Member), service, output, instance: null));

        var text = output.ToString();
        Assert.Contains($"Posted as member {Member}, for everyone, urgent", text);
        Assert.Contains("2 unread", text);
        Assert.Contains("You → everyone: please check the export", text.Replace("The person", "You"));
        Assert.Contains("checker · codex (outside Daoris) → everyone: the check passes", text);
        Assert.Equal([$"POST /api/channel/members/{Member}/messages", $"GET /api/channel/members/{Member}/messages"], standIn.Requests);
        Assert.Contains("\"urgent\":true", standIn.Bodies[0], StringComparison.Ordinal);
    }

    /// <summary>A refusal is said in a sentence with exit 1: a member that left, or one past its bounds.</summary>
    [Fact]
    public async Task A_member_refused_hears_why_and_exits_1()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath.EndsWith("/leave", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, $$"""{"id":"{{Member}}","origin":"ask:a1","entry":"e","agent":"codex","joined":"2026-11-06T09:00:00Z","left":"2026-11-06T10:00:00Z","why":"left"}""")
            : (HttpStatusCode.NotFound, """{"error":"No such channel or member here: not-member.","code":"not-member"}"""));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        Assert.Equal(0, await ChannelMemberVerb.RunAsync(new ChannelMemberAsk(ChannelMemberAsk.Leave, Member: Member), service, output, instance: null));
        Assert.Equal(1, await ChannelMemberVerb.RunAsync(new ChannelMemberAsk(ChannelMemberAsk.Post, Member: Member, Text: "still here?"), service, output, instance: null));

        var text = output.ToString();
        Assert.Contains($"Member {Member} left the channel", text);
        Assert.Contains("is no member of a channel here", text);
        Assert.Contains("a new Confirm", text);
    }

    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, """{"error":"none"}""");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
