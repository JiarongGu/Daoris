using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// QUESTCLOSE1 (D126's note, D50): <c>daoris-driver quest done</c>, the terminal's door to the person's done, beside the quest
/// page's <i>Mark done…</i>. It posts the person's words to the service's own door for it, never to respond's, and says the
/// service's sentence: 0 closed, 1 refused (a closed quest, no such quest), 2 the usage.
/// </summary>
public sealed class QuestDoneCommandTests
{
    public static TheoryData<string[]> Problems => new()
    {
        new[] { "done" },
        new[] { "done", "q1", "q2" },
        new[] { "done", "q1", "--note" },
        new[] { "done", "q1", "--note", "   " },
        new[] { "done", "q1", "--reason", "Built." },
        new[] { "done", "--note", "Built." },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem(string[] args)
    {
        Assert.True(QuestDoneCommand.Asks(args));
        Assert.Null(QuestDoneCommand.Read(args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void It_reads_a_quest_and_the_persons_words()
    {
        Assert.Equal(new QuestDoneAsk("q1", null), QuestDoneCommand.Read(["done", "#q1"], out _));
        Assert.Equal(new QuestDoneAsk("q1", "It is in the shared folder."), QuestDoneCommand.Read(["done", "q1", "--note", " It is in the shared folder. "], out _));
        Assert.False(QuestDoneCommand.Asks(["delete", "q1"]));
    }

    /// <summary>The person's words travel to the done door as written, and its sentence comes back; a refusal is exit 1, in its words.</summary>
    [Fact]
    public async Task It_posts_to_the_persons_done_door_and_says_the_services_words()
    {
        var heard = new List<(string Path, string Body)>();
        var standIn = new StandIn(request =>
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            lock (heard) heard.Add((request.RequestUri!.AbsolutePath, body));
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/quests/q1/done" when request.Method == HttpMethod.Post =>
                    (HttpStatusCode.OK, """{"quest":{"id":"q1","status":"Done"},"message":"Quest `#q1` is now Done: you marked it done."}"""),
                "/api/quests/q2/done" =>
                    (HttpStatusCode.Conflict, """{"error":"Quest `#q2` is Declined — a closed quest does not move; a new ask is a new title."}"""),
                _ => null,
            };
        });
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var output = new StringWriter();
        Assert.Equal(0, await QuestDoneCommand.RunAsync(new QuestDoneAsk("q1", "It is in the shared folder."), service, output));
        Assert.Equal(1, await QuestDoneCommand.RunAsync(new QuestDoneAsk("q2", null), service, output));
        var older = await service.PersonDoneAsync("q3", null);

        Assert.Equal(
            "daoris-driver: Quest `#q1` is now Done: you marked it done.\n"
            + "daoris-driver: Quest `#q2` is Declined — a closed quest does not move; a new ask is a new title.\n",
            output.ToString().ReplaceLineEndings("\n"));
        using var sent = JsonDocument.Parse(heard[0].Body);
        Assert.Equal("It is in the shared folder.", sent.RootElement.GetProperty("note").GetString());
        // No words is no note: the service writes its sentence alone.
        using var none = JsonDocument.Parse(heard[1].Body);
        Assert.False(none.RootElement.TryGetProperty("note", out _));
        Assert.False(older.Ok);
        Assert.Contains("older than this driver", older.Message);
    }

    /// <summary>The headless host's usage names the person's done, as every verb it answers is named.</summary>
    [Fact]
    public void The_usage_names_the_terminals_done()
    {
        Assert.Contains("\n  quest done <id> [--note \"…\"]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.StartsWith("usage: daoris-driver quest done <id> [--note \"…\"]", QuestDoneCommand.Usage);
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
