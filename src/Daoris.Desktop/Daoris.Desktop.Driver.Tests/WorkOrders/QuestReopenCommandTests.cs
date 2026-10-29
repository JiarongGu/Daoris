using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1c3 (the fabric events design §E5; D173's CLOSE1c note; D156, D50): <c>daoris-driver quest reopen</c> and <c>quest
/// go-on</c>, the terminal's doors to the person's reopen of a sealed submission and their go-on with the step it held, beside the
/// quest page's <i>Reopen…</i> and <i>Go on with it</i>. The reopen names the quest's current submission, read just before; each says
/// the service's sentence: 0 moved, 1 refused, 2 the usage. Runs no process: the fast half (MOD8).
/// </summary>
public sealed class QuestReopenCommandTests
{
    public static TheoryData<string[]> Problems => new()
    {
        new[] { "reopen" },
        new[] { "reopen", "q1", "q2" },
        new[] { "reopen", "q1", "--note" },
        new[] { "reopen", "q1", "--note", "   " },
        new[] { "reopen", "--note", "It seams." },
        new[] { "go-on" },
        new[] { "go-on", "q1", "--note", "Go." },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem(string[] args)
    {
        Assert.True(QuestReopenCommand.Asks(args));
        Assert.Null(QuestReopenCommand.Read(args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void It_reads_a_quest_which_door_and_the_persons_words()
    {
        Assert.Equal(new QuestReopenAsk("q1", false, null), QuestReopenCommand.Read(["reopen", "#q1"], out _));
        Assert.Equal(new QuestReopenAsk("q1", false, "It seams."), QuestReopenCommand.Read(["reopen", "q1", "--note", " It seams. "], out _));
        Assert.Equal(new QuestReopenAsk("q2", true, null), QuestReopenCommand.Read(["go-on", "#q2"], out _));
        Assert.False(QuestReopenCommand.Asks(["done", "q1"]));
    }

    /// <summary>
    /// The reopen names the submission the quest answers now, with the person's words; a quest with none says so and sends nothing; the
    /// go-on posts to its own door; a refusal is exit 1 in the service's words, and a host older than the doors is said to be.
    /// </summary>
    [Fact]
    public async Task It_posts_to_the_persons_doors_and_says_the_services_words()
    {
        var heard = new List<(string Path, string Body)>();
        var standIn = new StandIn(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            if (request.Method == HttpMethod.Post) lock (heard) heard.Add((path, body));
            return (request.Method.Method, path) switch
            {
                ("GET", "/api/quests") => (HttpStatusCode.OK,
                    """
                    [{"id":"q1","from":"game","to":"engine","title":"t","body":"","status":"Taken","closure":"gph-1","submission":{"machine":"desk","sequence":41,"at":"2026-10-18T09:00:00+00:00"}},
                     {"id":"q3","from":"game","to":"engine","title":"t","body":"","status":"Taken"}]
                    """),
                ("POST", "/api/quests/q1/reopen") => (HttpStatusCode.OK,
                    """{"quest":{"id":"q1","status":"Taken"},"message":"Reopened quest `#q1`'s submission: its step `#q2` is withdrawn."}"""),
                ("POST", "/api/quests/q2/continue") => (HttpStatusCode.Conflict,
                    """{"error":"Quest `#q2` grew from `#q1`, whose work has no unheld submission here yet: it can go on once that work is ready again. Nothing moved."}"""),
                _ => null,
            };
        });
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var output = new StringWriter();
        Assert.Equal(0, await QuestReopenCommand.RunAsync(new QuestReopenAsk("q1", false, "It seams."), service, output));
        Assert.Equal(1, await QuestReopenCommand.RunAsync(new QuestReopenAsk("q2", true, null), service, output));
        Assert.Equal(1, await QuestReopenCommand.RunAsync(new QuestReopenAsk("q3", false, null), service, output));
        var older = await service.ContinueQuestAsync("q4");

        var said = output.ToString().ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("daoris-driver: Reopened quest `#q1`'s submission: its step `#q2` is withdrawn.", said[0]);
        Assert.StartsWith("daoris-driver: Quest `#q2` grew from `#q1`", said[1], StringComparison.Ordinal);
        Assert.Contains("#q3", said[2], StringComparison.Ordinal);
        Assert.Contains("no submission", said[2], StringComparison.Ordinal);
        // A quest with no submission sends nothing; the older host's door is asked and answers no JSON.
        Assert.Equal(["/api/quests/q1/reopen", "/api/quests/q2/continue", "/api/quests/q4/continue"], heard.Select(h => h.Path));
        using var sent = JsonDocument.Parse(heard[0].Body);
        var submission = sent.RootElement.GetProperty("submission");
        Assert.Equal(("desk", 41L), (submission.GetProperty("machine").GetString(), submission.GetProperty("sequence").GetInt64()));
        Assert.Equal("It seams.", sent.RootElement.GetProperty("note").GetString());
        Assert.False(older.Ok);
        Assert.Contains("older than this driver", older.Message, StringComparison.Ordinal);
    }

    /// <summary>The headless host's usage names both, as every verb it answers is named.</summary>
    [Fact]
    public void The_usage_names_the_terminals_reopen_and_go_on()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  quest reopen <id> [--note \"…\"]\n", usage);
        Assert.Contains("\n  quest go-on <id>\n", usage);
        Assert.StartsWith("usage: daoris-driver quest reopen <id> [--note \"…\"]", QuestReopenCommand.Usage);
    }

    /// <summary>A service standing in: each request answered, or 404 with no JSON where the answer is null.</summary>
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
