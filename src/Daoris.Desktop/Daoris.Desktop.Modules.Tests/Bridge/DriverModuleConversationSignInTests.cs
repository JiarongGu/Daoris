using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A conversation's sign-in question is answered by any word kept for it, *Signed in* or typed, and its file goes with it.</summary>
public sealed class DriverModuleConversationSignInTests : DriverModuleBridge
{
    private const string Records = """
        [{"id":"c1","repository":"engine","state":"completed","kind":"chat","created":"2026-10-03T08:00:00Z"},
         {"id":"c2","repository":"engine","state":"completed","kind":"chat","created":"2026-10-03T09:00:00Z","from":"c1"}]
        """;

    private string FileOf(string session) => DaorisHome.Entry(Home, HomeEntry.Sessions, session + QuestionFiles.Suffix);

    private void Asked(string session, string id, bool conversation, DateTimeOffset at)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf(session))!);
        File.WriteAllText(FileOf(session),
            $$"""{"id":"{{id}}","session":"{{session}}","question":"Sign in as ops.","choices":[],"owner":"sign-in","site":"https://portal.example.com/settings",{{(conversation ? "\"conversation\":true," : "")}}"at":"{{at:O}}"}""");
    }

    private async Task<DriverModule> UpAsync(Door door)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(door)));
        return new DriverModule(Bus, loop);
    }

    [Theory]
    [InlineData("Signed in", "a1b2c3d4", true, true, false)]
    [InlineData("I signed in on my phone.", null, true, true, false)]
    [InlineData("I signed in on my phone.", null, true, false, true)]
    [InlineData("Signed in", null, false, true, true)]
    public async Task A_word_kept_for_a_conversation_clears_its_sign_in_question(string text, string? question, bool conversation, bool kept, bool stands)
    {
        Asked("c1", "a1b2c3d4", conversation, DateTimeOffset.UtcNow.AddMinutes(-5));
        var module = await UpAsync(new Door { Kept = kept });

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "c1", text, question });

        Assert.Equal(kept, sent.GetProperty("sent").GetBoolean());
        Assert.Equal(stands, File.Exists(FileOf("c1")));
    }

    /// <summary>🔴 A question the conversation asks while the word is being kept is another question, which the word does not answer.</summary>
    [Fact]
    public async Task A_sign_in_question_asked_while_the_word_is_kept_stands()
    {
        Asked("c1", "a1b2c3d4", true, DateTimeOffset.UtcNow.AddMinutes(-5));
        var module = await UpAsync(new Door { OnSay = _ => Asked("c1", "newer-one", true, DateTimeOffset.UtcNow.AddSeconds(1)) });

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "c1", text = "Signed in" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        Assert.Contains("newer-one", File.ReadAllText(FileOf("c1")), StringComparison.Ordinal);
    }

    /// <summary>🔴 A word said to a part that went on is kept by the part that took it, so it answers that part's question, not the first part's.</summary>
    [Fact]
    public async Task A_word_forwarded_to_the_line_head_clears_the_recipients_question_and_not_the_first_parts()
    {
        var asked = DateTimeOffset.UtcNow.AddMinutes(-5);
        Asked("c1", "first-part", true, asked);
        Asked("c2", "recipient", true, asked);
        var module = await UpAsync(new Door { Head = "c2" });

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "c1", text = "Signed in" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        Assert.False(File.Exists(FileOf("c2")));
        Assert.True(File.Exists(FileOf("c1")));
    }

    /// <summary>The records, and a say door keeping each word, refusing it, or naming the line's head that takes it instead.</summary>
    private sealed class Door : HttpMessageHandler
    {
        public bool Kept { get; init; } = true;

        public string? Head { get; init; }

        public Action<string>? OnSay { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/protocol") return Reply(HttpStatusCode.OK, """{"run":"r1","marks":{"settled":1,"opens":2,"head":1}}""");
            if (request.Method == HttpMethod.Get && path == "/api/sessions") return Reply(HttpStatusCode.OK, Records);
            if (request.Method == HttpMethod.Post && path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/say", StringComparison.Ordinal))
            {
                var session = path.Split('/')[3];
                OnSay?.Invoke(session);
                if (Head is { } head && session != head)
                {
                    return Reply(HttpStatusCode.Conflict, $$"""{"error":"Session `{{session}}` went on in `{{head}}`, its line's head: say it to that one.","refusal":"continued","head":"{{head}}"}""");
                }

                return Kept
                    ? Reply(HttpStatusCode.OK, $$$"""
                        {"session":{"id":"{{{session}}}","state":"completed"},"message":"Kept for session `{{{session}}}` to go on with.",
                         "said":{"id":"w1","text":"x","at":"2026-11-19T09:10:00+00:00","files":[],"reopens":true}}
                        """)
                    : Reply(HttpStatusCode.Conflict, $$"""{"error":"Session `{{session}}` stood down.","refusal":"stood-down"}""");
            }

            return Reply(HttpStatusCode.NotFound, "{}");
        }

        private static Task<HttpResponseMessage> Reply(HttpStatusCode status, string body) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
