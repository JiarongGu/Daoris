using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service as the review's doors meet it (REVIEWENV1c), standing in: a set-up step done and held for its review, the quest
/// list that answers it, and the review and set-up doors, each post kept as it was heard. Shared by the terminal's verdict and
/// the set-up's posting.
/// </summary>
internal static class ReviewStandIn
{
    public const string Commit = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The set-up step the stand-in answers: done, held for its review, having shown one set-up said by session s7.</summary>
    public static QuestView Step() => new("q2", "ask #a1", "web-app", "Show #q1 in `local` for review", "", "Done")
    {
        Parent = "q1",
        SetUpIn = "local",
        Held = true,
        Hold = EvidenceCodes.Unreviewed,
        SetUps =
        [
            new QuestSetUpView(Commit)
            {
                Look = "http://localhost:4200/reports", Shows = "the new column", Again = "open reports", Session = "s7", Local = true,
                At = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero), Machine = "desk", Sequence = 41,
            },
        ],
    };

    /// <summary>A service that answers the quest as given, keeps each post it hears, and answers the review and set-up doors.</summary>
    public static (ServiceClient Service, List<(string Path, string Body)> Heard) Of(QuestView quest)
    {
        var heard = new List<(string Path, string Body)>();
        var json = QuestJson(quest);
        var handler = new Answering(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            lock (heard) heard.Add((path, body));
            return (request.Method.Method, path) switch
            {
                ("GET", "/api/quests") => (HttpStatusCode.OK, $"[{json}]"),
                ("POST", var review) when review.EndsWith("/review", StringComparison.Ordinal) => (HttpStatusCode.OK,
                    body.Contains("not-yet", StringComparison.Ordinal)
                        ? """{"quest":{"id":"q2"},"message":"Not yet, on set-up step `#q2`'s set-up at `01234567`: your words are kept on the quest."}"""
                        : body.Contains("skipped", StringComparison.Ordinal)
                            ? """{"quest":{"id":"q1"},"message":"Skipped the review of quest `#q1`'s work: its landing waits for no review."}"""
                            : """{"quest":{"id":"q2"},"message":"Reviewed set-up step `#q2` in `local` at `01234567`: what its review held goes on."}"""),
                ("POST", var setUp) when setUp.EndsWith("/set-up", StringComparison.Ordinal) =>
                    (HttpStatusCode.OK, """{"quest":{"id":"q2"},"message":"Posted session `s7`'s set-up on set-up step `#q2`."}"""),
                _ => null,
            };
        });
        return (new ServiceClient("http://stand-in", null, new HttpClient(handler)), heard);
    }

    /// <summary>The quest as the service answers it, the set-up step's review fields included.</summary>
    private static string QuestJson(QuestView quest)
    {
        var setUps = string.Join(",", quest.SetUps.Select(setUp =>
            $$"""{"commit":"{{setUp.Commit}}","look":"{{setUp.Look}}","shows":"{{setUp.Shows}}","again":"{{setUp.Again}}","session":"{{setUp.Session}}","local":true,"at":"2026-10-08T09:00:00+00:00","machine":"{{setUp.Machine}}","sequence":{{setUp.Sequence}}}"""));
        var setUpIn = quest.SetUpIn is { } environment ? $$""","setUpIn":"{{environment}}" """ : "";
        return $$"""{"id":"{{quest.Id}}","from":"{{quest.From}}","to":"{{quest.To}}","title":"{{quest.Title}}","body":"","status":"{{quest.Status}}"{{setUpIn}},"setUps":[{{setUps}}]}""";
    }

    /// <summary>A service standing in: each request answered, or 404 with no JSON where the answer is null.</summary>
    private sealed class Answering(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
