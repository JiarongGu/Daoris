using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

public sealed partial class DriverModuleConversationTests
{
    /// <summary>
    /// 🔴 A conversation's sign-in question is answered by any word kept for it, *Signed in* or typed, and its file goes with it; a
    /// refused word and a driven park's file leave it standing.
    /// </summary>
    [Theory]
    [InlineData("Signed in", "a1b2c3d4", true, true, false)]
    [InlineData("I signed in on my phone.", null, true, true, false)]
    [InlineData("I signed in on my phone.", null, true, false, true)]
    [InlineData("Signed in", null, false, true, true)]
    public async Task A_word_kept_for_a_conversation_clears_its_sign_in_question(string text, string? question, bool conversation, bool kept, bool stands)
    {
        var file = DaorisHome.Entry(Home, HomeEntry.Sessions, "c1" + QuestionFiles.Suffix);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file,
            $$"""{"id":"a1b2c3d4","session":"c1","question":"Sign in as ops.","choices":[],"owner":"sign-in","site":"https://portal.example.com/settings",{{(conversation ? "\"conversation\":true," : "")}}"at":"2026-11-19T09:05:00Z"}""");
        var ledger = new Ledger
        {
            Records = Records(Chat("c1", "completed")),
            Say = id => kept
                ? (HttpStatusCode.OK, $$$"""
                    {"session":{"id":"{{{id}}}","state":"completed"},"message":"Kept for session `{{{id}}}` to go on with.",
                     "said":{"id":"w1","text":"x","at":"2026-11-19T09:10:00+00:00","files":[],"reopens":true}}
                    """)
                : (HttpStatusCode.Conflict, """{"error":"Session `c1` stood down.","refusal":"stood-down"}"""),
        };
        var (_, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "c1", text, question });

        Assert.Equal(kept, sent.GetProperty("sent").GetBoolean());
        Assert.Equal(stands, File.Exists(file));
    }
}
