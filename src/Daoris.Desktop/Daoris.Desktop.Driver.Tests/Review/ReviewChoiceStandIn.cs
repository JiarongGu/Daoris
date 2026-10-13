using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service as the review choice's terminal doors meet it (REVIEWENV1j), standing in: each request answered by the test's own
/// table, or 404 with no JSON where it answers none, and every request kept as it was heard, its method, path and body.
/// </summary>
internal static class ReviewChoiceStandIn
{
    /// <summary>A service answering by <paramref name="answer"/>, and the requests it heard.</summary>
    public static (ServiceClient Service, List<(string Method, string Path, string Body)> Heard) Of(
        Func<string, string, string, (HttpStatusCode Status, string Body)?> answer)
    {
        var heard = new List<(string Method, string Path, string Body)>();
        var handler = new Answering(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            lock (heard) heard.Add((request.Method.Method, path, body));
            return answer(request.Method.Method, path, body);
        });
        return (new ServiceClient("http://stand-in", null, new HttpClient(handler)), heard);
    }

    /// <summary>
    /// A local environment and a deployed one, <c>local</c> the default, for <paramref name="repository"/>, and a workspace rule
    /// declaring <c>preview</c> for <c>shop</c>.
    /// </summary>
    public static DriverConfig Config(string repository = "web-app") => DriverConfig.Empty with
    {
        Reviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase)
        {
            [repository] = new([
                new ReviewEnvironment("local", "local", "README.md", "http://localhost:4200"),
                new ReviewEnvironment("dev", "deployed", "docs/deploying.md"),
            ], Required: true),
            ["notes"] = ReviewRule.None,
        },
        WorkspaceReviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["shop"] = new([new ReviewEnvironment("preview", "deployed", "docs/preview.md")]),
        },
    };

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
