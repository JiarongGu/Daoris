using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// PARKVIEW1 (the park's ask design §2-§3): a parked session's question over the bridge. The session's page reads the question its
/// agent asked through `question_ask` and the driver kept for its park (`SESSION_QUESTION`, a read), and draws it at the
/// conversation's end with its answers as presses; none where none is kept.
/// </summary>
public sealed class DriverModuleSessionQuestionTests : DriverModuleBridge
{
    private void Asked(string session, string body)
    {
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Sessions, session + QuestionFiles.Suffix), body);
    }

    /// <summary>
    /// The question as its agent asked it: its words, each answer with what it does, the one recommended by number and by when, and
    /// when it asked; another session's, none.
    /// </summary>
    [Fact]
    public async Task A_parks_question_is_read_whole()
    {
        Asked("s1", """
            {"id":"a1b2c3d4","session":"s1","question":"Which quarterly target should the tile show?",
             "choices":[{"label":"1,200","does":"Write 1,200, the planning sheet's value."},{"label":"1,350","does":"Write 1,350."}],
             "recommend":1,"by":"before Friday's release","at":"2026-10-22T09:05:00Z"}
            """);

        var question = (await AnswerAsync(Module(), "SESSION_QUESTION", new { id = "s1" })).GetProperty("question");
        var none = await AnswerAsync(Module(), "SESSION_QUESTION", new { id = "s2" });

        Assert.Equal(("a1b2c3d4", "Which quarterly target should the tile show?", 1, "before Friday's release"),
            (question.GetProperty("id").GetString(), question.GetProperty("question").GetString(), question.GetProperty("recommend").GetInt32(),
                question.GetProperty("by").GetString()));
        Assert.Equal([("1,200", "Write 1,200, the planning sheet's value."), ("1,350", "Write 1,350.")],
            question.GetProperty("choices").EnumerateArray().Select(choice => (choice.GetProperty("label").GetString(), choice.GetProperty("does").GetString())));
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T09:05:00Z"), DateTimeOffset.Parse(question.GetProperty("at").GetString()!));
        Assert.True(!none.TryGetProperty("question", out var absent) || absent.ValueKind == JsonValueKind.Null);
    }

    /// <summary>A question with no answers offered, and no recommendation or deadline, reads as asked; an id that is not this machine's reads none.</summary>
    [Fact]
    public async Task A_free_question_and_a_teammates_id_read_as_they_are()
    {
        Asked("s1", """{"id":"c1","session":"s1","question":"Which environment?","choices":[],"recommend":null,"by":null,"at":"2026-10-22T09:05:00Z"}""");

        var question = (await AnswerAsync(Module(), "SESSION_QUESTION", new { id = "s1" })).GetProperty("question");
        var teammate = await AnswerAsync(Module(), "SESSION_QUESTION", new { id = "laptop/s1" });

        Assert.Empty(question.GetProperty("choices").EnumerateArray());
        Assert.True(!question.TryGetProperty("recommend", out var recommend) || recommend.ValueKind == JsonValueKind.Null);
        Assert.True(!question.TryGetProperty("by", out var by) || by.ValueKind == JsonValueKind.Null);
        Assert.True(!teammate.TryGetProperty("question", out var absent) || absent.ValueKind == JsonValueKind.Null);
    }
}
