using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1c4 (D183's note): Ask Daoris's read of a checkout reaches the driver as its chosen settings do, through the session
/// requests every loop watches: a keyed request for the conversation's own session, move <c>read</c>, taken once by the loop whose
/// registry runs that conversation and answered beside it, the answer carrying what was read beside its words.
/// </summary>
/// <remarks>
/// 🔴 A twin: the request is written by the service's connector (<c>CheckoutBox</c>), whose <c>McpCheckoutToolTests</c> hold the same
/// field names, in the order that side writes them, and read the answer's <c>said</c> and <c>text</c>. The registry is the watch's
/// test seam, so no process starts.
/// </remarks>
public sealed class CheckoutRequestTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-checkout-requests-" + Guid.NewGuid().ToString("N")[..8]);

    public CheckoutRequestTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private SessionRequests Requests => new(_home);

    private string AnswerPath => DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.answer.json");

    /// <summary>The request as the connector writes it, field for field in its order.</summary>
    private void Written()
    {
        Directory.CreateDirectory(Requests.Folder);
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.json"), """
            {
              "session": "h1",
              "move": "read",
              "at": "2026-10-21T09:00:00.0000000+00:00",
              "by": "help",
              "key": "0123456789ab",
              "operation": "read",
              "repository": "engine",
              "path": "src/Game.cs"
            }
            """);
    }

    [Fact]
    public void The_connectors_request_reads_as_the_twin_writes_it()
    {
        Written();

        var read = Assert.Single(Requests.Pending(Now.AddSeconds(5)));

        Assert.Equal(("h1", SessionMove.Read, RequestDoor.Help, "0123456789ab", Now), (read.Session, read.Move, read.By, read.Key, read.At));
        Assert.Equal(("read", "engine", "src/Game.cs"), (read.Operation, read.Repository, read.Path));
        Assert.Null(read.Value);
    }

    /// <summary>A request written here reads back the same, its path kept beside the repository: the driver's own writer is the twin's shape.</summary>
    [Fact]
    public void A_request_written_here_reads_back_the_same()
    {
        Requests.Write(new SessionRequest("h1", SessionMove.Read, Now)
        {
            By = RequestDoor.Help, Key = "0123456789ab", Operation = "read", Repository = "engine", Path = "README.md",
        });

        var read = Assert.Single(Requests.Pending(Now));
        using var written = JsonDocument.Parse(File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.json")));

        Assert.Equal(("read", "engine", "README.md"), (read.Operation, read.Repository, read.Path));
        Assert.False(written.RootElement.TryGetProperty("branches", out _));
    }

    /// <summary>
    /// 🔴 The loop running the conversation takes it once and writes the answer beside it, in the twin's fields with what was read as
    /// <c>text</c>; the request is gone, so no other loop acts on it again.
    /// </summary>
    [Fact]
    public async Task The_loop_running_the_conversation_takes_it_once_and_answers_beside_it()
    {
        Written();
        var asked = new List<SessionRequest>();
        await using var watch = new SessionRequestWatch(_home, runsHere: id => id == "h1", stop: _ => true, service: () => null, clock: () => Now)
        {
            Checkout = (request, _) =>
            {
                asked.Add(request);
                return Task.FromResult(new CheckoutAnswer(request.Operation!, true, null, "`engine`'s `src/Game.cs`, 14 bytes, whole:")
                {
                    Text = "class Game {}\n",
                });
            },
        };

        var honoured = await watch.HonourAsync();

        Assert.Equal("0123456789ab", Assert.Single(honoured).Key);
        Assert.Equal("src/Game.cs", Assert.Single(asked).Path);
        using var answer = JsonDocument.Parse(File.ReadAllText(AnswerPath));
        Assert.Equal(["operation", "done", "code", "said", "text"], answer.RootElement.EnumerateObject().Select(field => field.Name));
        Assert.Equal(("read", true, JsonValueKind.Null, "class Game {}\n"),
            (answer.RootElement.GetProperty("operation").GetString(), answer.RootElement.GetProperty("done").GetBoolean(),
                answer.RootElement.GetProperty("code").ValueKind, answer.RootElement.GetProperty("text").GetString()));
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.json")));
        Assert.Empty(await watch.HonourAsync());
    }

    /// <summary>
    /// A loop that does not run the conversation leaves the request, and so does one that reads no checkout; a judgement that fails
    /// is still answered, so the tool is never left to wait out its time, and nothing is said to have been read.
    /// </summary>
    [Fact]
    public async Task Another_loop_leaves_it_and_a_failed_judgement_is_still_answered()
    {
        Written();
        await using (var elsewhere = new SessionRequestWatch(_home, runsHere: _ => false, stop: _ => true, service: () => null, clock: () => Now)
                     {
                         Checkout = (_, _) => throw new InvalidOperationException("never asked"),
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
            Checkout = (_, _) => throw new IOException("the checkout is held"),
        };

        Assert.Single(await failing.HonourAsync());
        using var answer = JsonDocument.Parse(File.ReadAllText(AnswerPath));
        Assert.False(answer.RootElement.GetProperty("done").GetBoolean());
        Assert.Equal(CheckoutAnswer.Failed, answer.RootElement.GetProperty("code").GetString());
        Assert.Contains("the checkout is held", answer.RootElement.GetProperty("said").GetString());
        Assert.Equal(JsonValueKind.Null, answer.RootElement.GetProperty("text").ValueKind);
    }
}
