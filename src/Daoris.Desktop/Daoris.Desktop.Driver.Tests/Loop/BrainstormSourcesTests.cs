using System.Net;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORMPACKET1: a brainstorm's packet holds the records that answer its question. The search is asked by the question's
/// terms, designs, decisions and the code index rank above session records, the asker may name sources with <c>--source</c>, and
/// each source says why it is in.
/// </summary>
public sealed class BrainstormSourcesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-brainstorm-sources-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z");

    private const string Question =
        "How should the Agents place show the company view, the set-up flow and the workflow designs together, so a person reads one agents page?";

    public BrainstormSourcesTests() => Directory.CreateDirectory(Path.Combine(_home, "checkout", "docs"));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private BrainstormFiles Files => new(_home);

    private string Checkout => Path.Combine(_home, "checkout");

    private sealed class NoService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
    }

    /// <summary>The service's order is the old one: session records first, the designs after them.</summary>
    private static BrainstormSearch Search(string words) =>
        new(
            [
                new BrainstormFound("record", "docs/sessions/2026-09-01-session.md", null, null, "- fix: a commit list", words),
                new BrainstormFound("record", "docs/sessions/2026-09-02-session.md", null, null, "- feat: a commit list", words),
                new BrainstormFound("knowledge", "docs/2026-10-01-company-view-design.md", null, null, "The company view.", words),
                new BrainstormFound("knowledge", "docs/2026-10-02-setup-design.md", null, null, "The set-up flow.", words),
                new BrainstormFound("decision", "docs/decisions/D193.md", null, null, "A development company.", words),
                new BrainstormFound("index", "docs/index/README.md", 4, 9, "Agents place: AgentsView.", words),
            ], BrainstormPackets.Words);

    private async Task<BrainstormPacket> FreezeAsync(params string[] sources)
    {
        var id = Files.WriteRequest(Question, "daoris", [], At, null, sources);
        var asked = new List<string>();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null, new HttpClient(new NoService())), DriverConfig.Empty with { Actors = StaffedRoles.On("stub") }, ProviderRegistry.Built(), _home)
        {
            BrainstormSearches = (words, _, _) =>
            {
                asked.Add(words);
                return Task.FromResult(Search(words));
            },
            BrainstormCheckouts = (_, _) => Task.FromResult<(string?, string?)>((Checkout, "0123456789abcdef0123456789abcdef01234567")),
        };

        await driver.BrainstormsAsync(new Snapshot([], [], []), 0, [], CancellationToken.None);
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);

        Assert.DoesNotContain(Question, asked);
        Assert.All(asked, words => Assert.True(words.Length < Question.Length));
        return BrainstormPackets.Read(Files.PacketBytes(id)!)!;
    }

    [Fact]
    public async Task The_designs_and_decisions_rank_above_session_records_and_each_source_says_why()
    {
        var packet = await FreezeAsync();

        var paths = packet.Sources.Select(each => each.Path).ToList();
        var firstSession = paths.FindIndex(path => path.Contains("/sessions/", StringComparison.Ordinal));
        foreach (var kept in new[] { "docs/2026-10-01-company-view-design.md", "docs/2026-10-02-setup-design.md", "docs/decisions/D193.md", "docs/index/README.md" })
        {
            Assert.InRange(paths.IndexOf(kept), 0, firstSession - 1);
        }

        Assert.All(packet.Sources, each => Assert.False(string.IsNullOrWhiteSpace(each.Reason)));
        Assert.Contains("session record", packet.Sources[firstSession].Reason, StringComparison.Ordinal);
        Assert.Contains("design", packet.Sources.Single(each => each.Path.EndsWith("setup-design.md", StringComparison.Ordinal)).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_source_the_asker_named_is_in_first_whole_and_says_so()
    {
        await File.WriteAllTextAsync(Path.Combine(Checkout, "docs", "workflow-design.md"), "The workflow design, whole.");

        var packet = await FreezeAsync("docs/workflow-design.md", "docs/gone.md");

        var named = packet.Sources[0];
        Assert.Equal(("docs/workflow-design.md", "named", "The workflow design, whole."), (named.Path, named.Tier, named.Text));
        Assert.Contains("--source", named.Reason, StringComparison.Ordinal);
        Assert.Contains(packet.LeftOut, left => left is { Path: "docs/gone.md", Why: "removed" });
        Assert.Contains("in as a source the asker named", BrainstormCommand.Packet("x", packet), StringComparison.Ordinal);
    }

    /// <summary>The request the desk writes with its sources is read back as they were named, in order (the desk's own rules are the service suite's).</summary>
    [Fact]
    public void A_request_with_named_sources_reads_back_each_in_order()
    {
        Files.WriteRequest("Why?", "daoris", [], At, null, ["docs/a.md", "src/B.cs"]);

        Assert.Equal(["docs/a.md", "src/B.cs"], Files.Requests().Single().Sources);
    }
}
