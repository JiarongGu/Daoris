using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// PARKVIEW1 (the park's ask design §2-§3): a parked session's question over the bridge. The session's page reads the question its
/// agent asked through `question_ask` and the driver kept for its park (`SESSION_QUESTION`, a read), and draws it at the
/// conversation's end with its answers as presses; none where none is kept, and none for a record that is not parked (Fable's read,
/// Q7), whatever a file says.
/// </summary>
public sealed class DriverModuleSessionQuestionTests : DriverModuleBridge
{
    private void Asked(string session, string body)
    {
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Sessions, session + QuestionFiles.Suffix), body);
    }

    /// <summary>A loop whose service answers these records, and the module over it.</summary>
    private async Task<DriverModule> UpAsync(params (string Id, string State)[] records)
    {
        var loop = Loop();
        var json = "[" + string.Join(",", records.Select(record =>
            $$"""{"id":"{{record.Id}}","repository":"engine","state":"{{record.State}}","quest":"q1","kind":"driven","created":"2026-10-22T08:00:00Z"}""")) + "]";
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Records(json))));
        return new DriverModule(Bus, loop);
    }

    /// <summary>
    /// The question as its agent asked it: its words, each answer with what it does, the one recommended by number with why, by when, and
    /// when it asked; another session's, none.
    /// </summary>
    [Fact]
    public async Task A_parks_question_is_read_whole()
    {
        Asked("s1", """
            {"id":"a1b2c3d4","session":"s1","question":"Which quarterly target should the tile show?",
             "choices":[{"label":"1,200","does":"Write 1,200, the planning sheet's value."},{"label":"1,350","does":"Write 1,350."}],
             "recommend":1,"why":"The ticket links the planning sheet.","by":"before Friday's release","at":"2026-10-22T09:05:00Z"}
            """);
        var module = await UpAsync(("s1", "awaiting-person"), ("s2", "awaiting-person"));

        var question = (await AnswerAsync(module, "SESSION_QUESTION", new { id = "s1" })).GetProperty("question");
        var none = await AnswerAsync(module, "SESSION_QUESTION", new { id = "s2" });

        Assert.Equal(("a1b2c3d4", "Which quarterly target should the tile show?", 1, "before Friday's release"),
            (question.GetProperty("id").GetString(), question.GetProperty("question").GetString(), question.GetProperty("recommend").GetInt32(),
                question.GetProperty("by").GetString()));
        Assert.Equal([("1,200", "Write 1,200, the planning sheet's value."), ("1,350", "Write 1,350.")],
            question.GetProperty("choices").EnumerateArray().Select(choice => (choice.GetProperty("label").GetString(), choice.GetProperty("does").GetString())));
        Assert.Equal("The ticket links the planning sheet.", question.GetProperty("why").GetString());
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T09:05:00Z"), DateTimeOffset.Parse(question.GetProperty("at").GetString()!));
        Assert.True(!none.TryGetProperty("question", out var absent) || absent.ValueKind == JsonValueKind.Null);
    }

    /// <summary>A question with no answers offered, and no recommendation or deadline, reads as asked; an id that is not this machine's reads none.</summary>
    [Fact]
    public async Task A_free_question_and_a_teammates_id_read_as_they_are()
    {
        Asked("s1", """{"id":"c1","session":"s1","question":"Which environment?","choices":[],"recommend":null,"by":null,"at":"2026-10-22T09:05:00Z"}""");
        var module = await UpAsync(("s1", "awaiting-person"));

        var question = (await AnswerAsync(module, "SESSION_QUESTION", new { id = "s1" })).GetProperty("question");
        var teammate = await AnswerAsync(module, "SESSION_QUESTION", new { id = "laptop/s1" });

        Assert.Empty(question.GetProperty("choices").EnumerateArray());
        Assert.True(!question.TryGetProperty("recommend", out var recommend) || recommend.ValueKind == JsonValueKind.Null);
        Assert.True(!question.TryGetProperty("by", out var by) || by.ValueKind == JsonValueKind.Null);
        Assert.True(!teammate.TryGetProperty("question", out var absent) || absent.ValueKind == JsonValueKind.Null);
    }

    /// <summary>
    /// 🔴 Fable's read, Q7: a record that is not parked has no question, whatever a file says (a run that works, one that ended, one the
    /// service no longer holds, and before the loop is up).
    /// </summary>
    [Theory]
    [InlineData("working")]
    [InlineData("completed")]
    [InlineData(null)]
    public async Task A_record_not_parked_has_no_question(string? state)
    {
        Asked("s1", """{"id":"c1","session":"s1","question":"Which environment?","choices":[],"at":"2026-10-22T09:05:00Z"}""");
        var module = state is null ? await UpAsync() : await UpAsync(("s1", state));

        var answer = await AnswerAsync(module, "SESSION_QUESTION", new { id = "s1" });

        Assert.True(!answer.TryGetProperty("question", out var absent) || absent.ValueKind == JsonValueKind.Null);
        var down = await AnswerAsync(Module(), "SESSION_QUESTION", new { id = "s1" });
        Assert.True(!down.TryGetProperty("question", out var none) || none.ValueKind == JsonValueKind.Null);
    }

    /// <summary>A stand-in service: the records as given, on the one door the route reads.</summary>
    private sealed class Records(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/api/sessions"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
    }
}
