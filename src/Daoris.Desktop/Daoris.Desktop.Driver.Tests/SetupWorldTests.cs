using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAYOUT7: the doors the set-up press reads and asks through, over an in-process stand-in for the service: the index's
/// entries for one repository, and one ask with its receiver named, as the person's.
/// </summary>
public sealed class SetupWorldTests
{
    [Fact]
    public async Task The_indexs_entries_for_a_repository_are_read_by_their_paths()
    {
        var service = new StandIn("""[{"path":"docs/DECISIONS.md","title":"D1"},{"path":"CHANGELOG.md"},{"title":"no path"}]""");
        using var client = service.Client();

        var paths = await new SetupWorld(client, "unused-home").EntriesAsync("reports & co", CancellationToken.None);

        Assert.Equal(["docs/DECISIONS.md", "CHANGELOG.md"], paths);
        Assert.Equal("/api/entries?repository=reports%20%26%20co", service.Asked.Single().PathAndQuery);
    }

    /// <summary>One ask to one repository, at its workspace, with no files and no links: published at once, so no intake runs.</summary>
    [Fact]
    public async Task A_press_asks_once_with_its_receiver_named()
    {
        var service = new StandIn("""{"message":"Asked as `#a1b2c3` in `work`.","ask":{"id":"a1b2c3"},"quest":{"id":"q1"}}""");
        using var client = service.Client();

        var answer = await new SetupWorld(client, "unused-home").PublishAsync("work", "Set up this repository for every agent (2026-10-01)\n\nbody", "reports", CancellationToken.None);

        Assert.True(answer.Ok);
        Assert.Equal(("a1b2c3", "q1"), (answer.AskId, answer.QuestId));
        var (asked, body) = (service.Asked.Single(), service.Bodies.Single());
        Assert.Equal("/api/asks", asked.AbsolutePath);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal("work", sent.RootElement.GetProperty("workspace").GetString());
        Assert.Equal("reports", sent.RootElement.GetProperty("to").GetString());
        Assert.StartsWith("Set up this repository for every agent (2026-10-01)\n", sent.RootElement.GetProperty("sentence").GetString());
        Assert.Equal(0, sent.RootElement.GetProperty("attachments").GetArrayLength());
        Assert.Equal(0, sent.RootElement.GetProperty("links").GetArrayLength());
    }

    private sealed class StandIn(string answer) : HttpMessageHandler
    {
        public List<Uri> Asked { get; } = [];

        public List<string> Bodies { get; } = [];

        public ServiceClient Client() => new("http://setup.test", null, new HttpClient(this, disposeHandler: false));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked.Add(request.RequestUri!);
            if (request.Content is not null) Bodies.Add(await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }
}
