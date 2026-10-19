using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SYNCMERGE1c3 (D178 points 2, 7, 8): <c>daoris-driver sync forward &lt;quest&gt; &lt;machine&gt; &lt;sequence&gt;</c>, the terminal's door to
/// the person's bring-forward of one side of two machines' work on a quest, beside <c>sync dismiss</c>. Exit 0 brought forward, 1 refused
/// or lost again, 2 the usage. Runs no process: the fast half (MOD8).
/// </summary>
public sealed class SyncForwardCommandTests
{
    public static TheoryData<string[]> Problems => new()
    {
        new[] { "forward" },
        new[] { "forward", "q1" },
        new[] { "forward", "q1", "desk" },
        new[] { "forward", "q1", "desk", "x" },
        new[] { "forward", "q1", "desk", "-3" },
        new[] { "forward", "q1", "--workspace", "4" },
        new[] { "forward", "q1", "desk", "4", "5" },
        new[] { "forward", "-q", "desk", "4" },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem(string[] args)
    {
        Assert.True(SyncForwardCommand.Asks(args));
        Assert.Null(SyncForwardCommand.Read(args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void It_reads_a_quest_a_machine_and_a_sequence()
    {
        Assert.Equal(new SyncForwardAsk("q1", "desk", 41), SyncForwardCommand.Read(["forward", "#q1", "desk", "41"], out _));
        Assert.False(SyncForwardCommand.Asks(["dismiss", "q1"]));
    }

    /// <summary>The door gets the machine and sequence; 200 is 0, a 409 refusal is 1 in the service's words, 404 is 1.</summary>
    [Fact]
    public async Task It_posts_to_the_forward_door_and_says_the_services_words()
    {
        var heard = new List<(string Path, string Body)>();
        var standIn = new StandIn(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            if (request.Method == HttpMethod.Post) lock (heard) heard.Add((path, body));
            return (request.Method.Method, path) switch
            {
                ("POST", "/api/quests/q1/conflicts/forward") => (HttpStatusCode.OK, """{"message":"Brought forward desk's work on `#q1`."}"""),
                ("POST", "/api/quests/q2/conflicts/forward") => (HttpStatusCode.Conflict, """{"error":"Quest `#q2` moved again. Nothing moved."}"""),
                ("POST", "/api/quests/q3/conflicts/forward") => (HttpStatusCode.NotFound, """{"error":"No quest `#q3` here."}"""),
                _ => null,
            };
        });
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var output = new StringWriter();
        Assert.Equal(0, await SyncForwardCommand.RunAsync(new SyncForwardAsk("q1", "desk", 41), service, output));
        Assert.Equal(1, await SyncForwardCommand.RunAsync(new SyncForwardAsk("q2", "desk", 41), service, output));
        Assert.Equal(1, await SyncForwardCommand.RunAsync(new SyncForwardAsk("q3", "desk", 41), service, output));

        var said = output.ToString().ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("sync: Brought forward desk's work on `#q1`.", said[0]);
        Assert.StartsWith("sync: Quest `#q2` moved again", said[1], StringComparison.Ordinal);
        Assert.Contains("No quest", said[2], StringComparison.Ordinal);
        using var sent = JsonDocument.Parse(heard[0].Body);
        Assert.Equal(("desk", 41L), (sent.RootElement.GetProperty("machine").GetString(), sent.RootElement.GetProperty("sequence").GetInt64()));
    }

    [Fact]
    public void The_usage_names_the_verb()
    {
        Assert.Contains("sync forward <quest> <machine> <sequence>", SyncForwardCommand.Usage, StringComparison.Ordinal);
        Assert.Contains("sync forward <quest> <machine> <sequence>", DriverCommand.Usage, StringComparison.Ordinal);
    }

    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
