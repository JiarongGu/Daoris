using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1c2a (D183's note): Ask Daoris's housekeeping tool reaches the driver through the session requests every loop watches
/// (SESSUX1g): a keyed request for the conversation's own session, move <c>housekeep</c>, taken once by the loop whose registry
/// runs that conversation and answered beside it, as a terminal's say is (MSG1e). Any other loop leaves it, and so does a loop
/// that answers no housekeeping.
/// </summary>
/// <remarks>
/// 🔴 A twin: the request is written by the service's connector (<c>HousekeepingBox</c>), whose <c>McpHousekeepingToolTests</c>
/// hold the same field names, in the order that side writes them. The registry is the watch's test seam, so no process starts.
/// </remarks>
public sealed class HousekeepingRequestTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-housekeeping-requests-" + Guid.NewGuid().ToString("N")[..8]);

    public HousekeepingRequestTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private SessionRequests Requests => new(_home);

    /// <summary>The request as the connector writes it, field for field in its order.</summary>
    private void Written()
    {
        Directory.CreateDirectory(Requests.Folder);
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.json"), """
            {
              "session": "h1",
              "move": "housekeep",
              "at": "2026-10-20T09:00:00.0000000+00:00",
              "by": "help",
              "key": "0123456789ab",
              "operation": "clean-up",
              "repository": "engine",
              "branches": ["daoris/s-one", "daoris/s-two"]
            }
            """);
    }

    [Fact]
    public void The_connectors_request_reads_as_the_twin_writes_it()
    {
        Written();

        var read = Assert.Single(Requests.Pending(Now.AddSeconds(5)));

        Assert.Equal(("h1", SessionMove.Housekeep, RequestDoor.Help, "0123456789ab", Now), (read.Session, read.Move, read.By, read.Key, read.At));
        Assert.Equal(("clean-up", "engine"), (read.Operation, read.Repository));
        Assert.Equal(["daoris/s-one", "daoris/s-two"], read.Branches);
    }

    /// <summary>
    /// 🔴 The loop running the conversation takes it once and writes the answer beside it, in the twin's fields; the request is
    /// gone, so no other loop acts on it again.
    /// </summary>
    [Fact]
    public async Task The_loop_running_the_conversation_takes_it_once_and_answers_beside_it()
    {
        Written();
        var asked = new List<SessionRequest>();
        await using var watch = new SessionRequestWatch(_home, runsHere: id => id == "h1", stop: _ => true, service: () => null, clock: () => Now)
        {
            Housekeep = (request, _) =>
            {
                asked.Add(request);
                return Task.FromResult(new HousekeepingAnswer(request.Operation!, true, null, "`daoris/s-one` in `engine` was removed."));
            },
        };

        var honoured = await watch.HonourAsync();

        Assert.Equal("0123456789ab", Assert.Single(honoured).Key);
        Assert.Equal(["daoris/s-one", "daoris/s-two"], Assert.Single(asked).Branches);
        var answerPath = DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.answer.json");
        using var answer = JsonDocument.Parse(File.ReadAllText(answerPath));
        Assert.Equal(["operation", "done", "code", "said"], answer.RootElement.EnumerateObject().Select(field => field.Name));
        Assert.Equal(("clean-up", true, JsonValueKind.Null, "`daoris/s-one` in `engine` was removed."),
            (answer.RootElement.GetProperty("operation").GetString(), answer.RootElement.GetProperty("done").GetBoolean(),
                answer.RootElement.GetProperty("code").ValueKind, answer.RootElement.GetProperty("said").GetString()));
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.json")));
        Assert.Empty(await watch.HonourAsync());
    }

    /// <summary>
    /// A loop that does not run the conversation leaves the request, and so does one that answers no housekeeping; a judgement
    /// that fails is still answered, so the tool is never left to wait out its time.
    /// </summary>
    [Fact]
    public async Task Another_loop_leaves_it_and_a_failed_judgement_is_still_answered()
    {
        Written();
        await using (var elsewhere = new SessionRequestWatch(_home, runsHere: _ => false, stop: _ => true, service: () => null, clock: () => Now)
                     {
                         Housekeep = (_, _) => throw new InvalidOperationException("never asked"),
                     })
        {
            Assert.Empty(await elsewhere.HonourAsync());
        }

        await using (var deaf = new SessionRequestWatch(_home, runsHere: _ => true, stop: _ => true, service: () => null, clock: () => Now))
        {
            Assert.Empty(await deaf.HonourAsync());
        }

        Assert.Single(Requests.Pending(Now));
        await using var failing = new SessionRequestWatch(_home, runsHere: _ => true, stop: _ => true, service: () => null, clock: () => Now)
        {
            Housekeep = (_, _) => throw new DriverException("git could not read `engine`"),
        };

        Assert.Single(await failing.HonourAsync());
        using var answer = JsonDocument.Parse(File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.answer.json")));
        Assert.False(answer.RootElement.GetProperty("done").GetBoolean());
        Assert.Equal(HousekeepingAnswer.Failed, answer.RootElement.GetProperty("code").GetString());
        Assert.Contains("git could not read `engine`", answer.RootElement.GetProperty("said").GetString());
    }
}
