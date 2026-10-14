using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ENTRY1c: an ask proposal's Apply is the ask composer's own door, the local host's <c>POST /api/asks</c>, with the review choice
/// and the person's words as the composer sends them. A named environment the ask's workspace does not declare is refused before
/// anything is sent, as <c>daoris-driver ask --review</c> refuses it (<see cref="AskReviewCommand.UndeclaredAsync"/>): the
/// proposal's plan holds no repositories per workspace, so the door judges it. A service stood in, and nothing starts a process:
/// the fast half (MOD8), beside <see cref="HelpDoorsTests"/>, whose update runs a script.
/// </summary>
public sealed class HelpAskDoorTests : DriverModuleBridge
{
    /// <summary>Workspace <c>work</c>'s own rule declaring <c>dev</c>, as <c>daoris driver review --workspace work dev …</c> writes it.</summary>
    private void DeclareDev() => (DriverConfig.Empty with
    {
        WorkspaceReviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["work"] = new([new ReviewEnvironment("dev", "deployed", "docs/deploying.md")]),
        },
    }).Save(DriverConfigPath);

    [Fact]
    public async Task An_ask_goes_through_the_composers_door_with_the_review_choice_and_its_words()
    {
        DeclareDev();
        var host = new StandInHost();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(host));
        var doors = Module().HelpDoors(service);

        var chosen = await doors.AskAsync("work", "add the compare setting", "dev", "it touches the page", CancellationToken.None);
        var none = await doors.AskAsync("work", "fix the stall", null, null, CancellationToken.None);

        Assert.Equal((true, "Asked as `#a1b2c3` in `work`."), (chosen.Ok, chosen.Message));
        Assert.True(none.Ok);
        var posts = host.Heard.Where(each => each.Method == "POST").ToList();
        Assert.Equal(["/api/asks", "/api/asks"], posts.Select(each => each.Path));
        using var withChoice = JsonDocument.Parse(posts[0].Body);
        Assert.Equal(("dev", "it touches the page"),
            (withChoice.RootElement.GetProperty("review").GetString(), withChoice.RootElement.GetProperty("reviewWords").GetString()));
        using var withNone = JsonDocument.Parse(posts[1].Body);
        Assert.False(withNone.RootElement.TryGetProperty("review", out _));
        Assert.False(withNone.RootElement.TryGetProperty("reviewWords", out _));
    }

    [Fact]
    public async Task A_named_environment_the_workspace_does_not_declare_is_refused_before_anything_is_sent()
    {
        DeclareDev();
        var host = new StandInHost();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(host));
        var doors = Module().HelpDoors(service);

        var refused = await doors.AskAsync("work", "add the compare setting", "preview", null, CancellationToken.None);

        Assert.False(refused.Ok);
        Assert.Equal("No review environment `preview` is declared in workspace `work`: its rules declare `dev`. Nothing was kept.", refused.Message);
        Assert.DoesNotContain(host.Heard, each => each.Method == "POST");

        // `on` and `off` read no rule, so they are sent in a workspace that declares none.
        Assert.True((await doors.AskAsync("default", "fix the stall", "off", "a readme change", CancellationToken.None)).Ok);
        Assert.Single(host.Heard, each => each.Method == "POST");
    }

    /// <summary>The local host's registry and ask door, standing in: every request kept as heard, its method, path and body.</summary>
    private sealed class StandInHost : HttpMessageHandler
    {
        public List<(string Method, string Path, string Body)> Heard { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            lock (Heard) Heard.Add((request.Method.Method, path, body));
            var answer = (request.Method.Method, path) switch
            {
                ("GET", "/api/registry") => """[{"repository":"engine","workspace":"work"}]""",
                ("POST", "/api/asks") => JsonSerializer.Serialize(new
                {
                    ask = new { id = "a1b2c3" },
                    message = $"Asked as `#a1b2c3` in `{JsonDocument.Parse(body).RootElement.GetProperty("workspace").GetString()}`.",
                }),
                _ => null,
            };
            return new HttpResponseMessage(answer is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(answer ?? "", Encoding.UTF8, "application/json"),
            };
        }
    }
}
