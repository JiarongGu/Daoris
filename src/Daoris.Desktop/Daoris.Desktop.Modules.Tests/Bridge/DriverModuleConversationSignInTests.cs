using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A conversation's sign-in question is answered by any word kept for it, *Signed in* or typed, and its file goes with it.</summary>
public sealed class DriverModuleConversationSignInTests : DriverModuleBridge
{
    private const string Chat = """[{"id":"c1","repository":"engine","state":"completed","kind":"chat","created":"2026-10-03T08:00:00Z"}]""";

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
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Door(kept))));
        var module = new DriverModule(Bus, loop);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "c1", text, question });

        Assert.Equal(kept, sent.GetProperty("sent").GetBoolean());
        Assert.Equal(stands, File.Exists(file));
    }

    /// <summary>The conversation's record, and its say door keeping each word or refusing it.</summary>
    private sealed class Door(bool kept) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/sessions") return Reply(HttpStatusCode.OK, Chat);
            if (request.Method == HttpMethod.Post && path == "/api/sessions/c1/say")
            {
                return kept
                    ? Reply(HttpStatusCode.OK, """
                        {"session":{"id":"c1","state":"completed"},"message":"Kept for session `c1` to go on with.",
                         "said":{"id":"w1","text":"x","at":"2026-11-19T09:10:00+00:00","files":[],"reopens":true}}
                        """)
                    : Reply(HttpStatusCode.Conflict, """{"error":"Session `c1` stood down.","refusal":"stood-down"}""");
            }

            return Reply(HttpStatusCode.NotFound, "{}");
        }

        private static Task<HttpResponseMessage> Reply(HttpStatusCode status, string body) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
