using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KNOWCENTRAL2f (the memory coordinator design §6, at presentation): a hand-over's brief shows the memory its line read as the service
/// answers it when the brief is composed, an entry ended since its read shown ended; a service with no such door leaves the brief as it was.
/// </summary>
public sealed class BriefMemoryTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "brief-memory", Guid.NewGuid().ToString("N")[..8]);

    public BriefMemoryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private const string Ended = "`Owner:12`, read at revision 1, has ended: The map moved to vectors. It said: The map is drawn in tiles.";

    private SessionEvents Events()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("c0nv0001", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Draw the map.", At = At });
        events.Append("c0nv0001", new SessionEvent { Kind = SessionEventKind.Message, Text = "Drawn in tiles." });
        return events;
    }

    private static readonly string Records =
        new JsonArray(new JsonObject { ["id"] = "c0nv0001", ["state"] = "ended", ["repository"] = "Owner" }).ToJsonString();

    /// <summary>🔴 The brief asks the service what the line read, by the part moved from, and shows each entry as answered: an ended one ended.</summary>
    [Fact]
    public async Task A_hand_over_s_brief_shows_an_entry_its_line_read_as_the_service_answers_it_now()
    {
        var asked = new List<string>();
        var memory = new JsonObject
        {
            ["shown"] = new JsonArray(new JsonObject { ["reference"] = "Owner:12", ["standing"] = "ended", ["line"] = Ended }),
        }.ToJsonString();
        using var service = QuestStandIn.Client(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/memory/read", StringComparison.Ordinal)) return (HttpStatusCode.OK, Records);
            asked.Add($"{request.Method} {request.RequestUri.AbsolutePath}");
            return (HttpStatusCode.OK, memory);
        });

        var brief = await ContextBriefs.ComposeAsync(service, Events(), _home, "c0nv0001", HandOverWhy.Press, conversation: true, config: DriverConfig.Empty);

        Assert.Equal(["POST /api/sessions/c0nv0001/memory/read"], asked);
        Assert.Contains("Memory this line read, as it stands now: " + Ended, brief.Text);
    }

    /// <summary>🔴 A service with no such door, or an answer that does not read, leaves the brief without memory and composes it still.</summary>
    [Fact]
    public async Task A_service_with_no_such_door_leaves_the_brief_without_memory()
    {
        using var none = QuestStandIn.Client(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/memory/read", StringComparison.Ordinal) ? (HttpStatusCode.NotFound, "{}") : (HttpStatusCode.OK, Records));
        using var unread = QuestStandIn.Client(_ => (HttpStatusCode.OK, "[]"));

        foreach (var service in new[] { none, unread })
        {
            var brief = await ContextBriefs.ComposeAsync(service, Events(), _home, "c0nv0001", HandOverWhy.Press, conversation: true, config: DriverConfig.Empty);
            Assert.Contains("Draw the map.", brief.Text);
            Assert.DoesNotContain("Memory this line read", brief.Text);
        }
    }
}
