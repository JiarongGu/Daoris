using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A brief says what its line waits on, from the exchanges the line holds: a helper's
/// ask, an answer kept and not yet handed, a predecessor's ask after a hand-over, each told once and bounded.
/// </summary>
public sealed class BriefWaitsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "brief-waits", Guid.NewGuid().ToString("N")[..8]);

    public BriefWaitsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private SessionEvents Events()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        foreach (var id in new[] { "c0nv0001", "c0nv0002" })
        {
            events.Append(id, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Draw the map.", At = At });
            events.Append(id, new SessionEvent { Kind = SessionEventKind.Message, Text = "Drawn in tiles." });
        }

        return events;
    }

    /// <summary>The line c0nv0001 then c0nv0002, the second begun by a hand-over from the first.</summary>
    private static readonly string Line = new JsonArray(
        new JsonObject { ["id"] = "c0nv0001", ["state"] = "ended", ["repository"] = "Owner" },
        new JsonObject { ["id"] = "c0nv0002", ["state"] = "ended", ["repository"] = "Owner", ["from"] = "c0nv0001", ["fromWhy"] = "press", ["line"] = "c0nv0001" }).ToJsonString();

    private string Ask(string id, string asker, string question)
    {
        var files = new HelperFiles(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(files.PathOf(id, "ask"))!);
        File.WriteAllText(files.PathOf(id, "ask"),
            new JsonObject
            {
                ["id"] = id, ["asker"] = asker, ["question"] = question, ["agent"] = "codex-acp", ["askedBy"] = "agent", ["at"] = Moments.Write(At),
            }.ToJsonString());
        return id;
    }

    private Task<ComposedBrief> Compose(string session)
    {
        using var service = QuestStandIn.Client(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/memory/read", StringComparison.Ordinal) ? (HttpStatusCode.NotFound, "{}") : (HttpStatusCode.OK, Line));
        return ContextBriefs.ComposeAsync(service, Events(), _home, session, HandOverWhy.Press, conversation: true, config: DriverConfig.Empty);
    }

    private static int Count(string text, string part) => text.Split(part).Length - 1;

    /// <summary>🔴 A hand-over while the predecessor's helper reads tells the new part what the line waits on, once.</summary>
    [Fact]
    public async Task A_hand_over_while_a_helper_reads_tells_the_new_part_once()
    {
        Ask("h1", "c0nv0001", "Is the tile size 32?");

        var text = (await Compose("c0nv0002")).Text;

        Assert.Contains("What this line waits on:", text);
        Assert.Contains("`helper:h1`", text);
        Assert.Contains("session `c0nv0001`", text);
        Assert.Contains("kept", text);
        Assert.Equal(1, Count(text, "Is the tile size 32?"));
        Assert.DoesNotContain("Asked and not answered yet", text);
    }

    /// <summary>🔴 An answer kept and not yet handed reads as open; a handed one, or a refused ask, does not.</summary>
    [Fact]
    public async Task An_answer_kept_and_not_yet_handed_is_open_and_a_handed_one_is_not()
    {
        Ask("h1", "c0nv0001", "Kept answer?");
        Ask("h2", "c0nv0001", "Handed answer?");
        Ask("h3", "c0nv0001", "Refused ask?");
        var files = new HelperFiles(_home);
        files.WriteState(new HelperState("h1", HelperStates.Answered, null, "hx", At));
        files.WriteState(new HelperState("h2", HelperStates.Answered, null, "hx", At) { Handed = "w1" });
        files.WriteState(new HelperState("h3", HelperStates.Refused, null, null, At));

        var text = (await Compose("c0nv0002")).Text;

        Assert.Contains("Kept answer?", text);
        Assert.Contains("not yet handed", text);
        Assert.DoesNotContain("Handed answer?", text);
        Assert.DoesNotContain("Refused ask?", text);
    }

    /// <summary>🔴 The list is bounded and counted like the memory: what does not fit is said.</summary>
    [Fact]
    public async Task The_list_is_bounded_and_the_rest_counted()
    {
        for (var at = 0; at < ContextBrief.WaitLimit + 3; at++) Ask($"h{at:D2}", "c0nv0001", $"Question {at:D2}?");

        var text = (await Compose("c0nv0002")).Text;

        Assert.Equal(ContextBrief.WaitLimit, Count(text, "What this line waits on:"));
        Assert.Contains("… and 3 more things this line waits on", text);
    }
}
