using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1c3 (D183's note): Ask Daoris's chosen settings reach the driver as its housekeeping does, through the session requests
/// every loop watches: a keyed request for the conversation's own session, move <c>set</c>, taken once by the loop whose registry runs
/// that conversation and answered beside it. Any other loop leaves it, and so does a loop that answers no setting.
/// </summary>
/// <remarks>
/// 🔴 A twin: the request is written by the service's connector (<c>SettingsBox</c>), whose <c>McpChosenSettingsToolTests</c> hold
/// the same field names, in the order that side writes them. The registry is the watch's test seam, so no process starts.
/// </remarks>
public sealed class SettingRequestTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-setting-requests-" + Guid.NewGuid().ToString("N")[..8]);

    public SettingRequestTests() => Directory.CreateDirectory(_home);

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
              "move": "set",
              "at": "2026-10-20T09:00:00.0000000+00:00",
              "by": "help",
              "key": "0123456789ab",
              "operation": "language",
              "value": "zh",
              "workspace": "aurora"
            }
            """);
    }

    [Fact]
    public void The_connectors_request_reads_as_the_twin_writes_it()
    {
        Written();

        var read = Assert.Single(Requests.Pending(Now.AddSeconds(5)));

        Assert.Equal(("h1", SessionMove.Set, RequestDoor.Help, "0123456789ab", Now), (read.Session, read.Move, read.By, read.Key, read.At));
        Assert.Equal(("language", "zh", null, "aurora"), (read.Operation, read.Value, read.Repository, read.Workspace));
        Assert.Empty(read.Branches);
    }

    /// <summary>
    /// 🔴 The loop running the conversation takes it once and writes the answer beside it, in the twin's fields; the request is gone,
    /// so no other loop acts on it again.
    /// </summary>
    [Fact]
    public async Task The_loop_running_the_conversation_takes_it_once_and_answers_beside_it()
    {
        Written();
        var asked = new List<SessionRequest>();
        await using var watch = new SessionRequestWatch(_home, runsHere: id => id == "h1", stop: _ => true, service: () => null, clock: () => Now)
        {
            Set = (request, _) =>
            {
                asked.Add(request);
                return Task.FromResult(new SettingAnswer(request.Operation!, true, null, "Set: sessions in workspace `aurora` write in Chinese."));
            },
        };

        var honoured = await watch.HonourAsync();

        Assert.Equal("0123456789ab", Assert.Single(honoured).Key);
        Assert.Equal("aurora", Assert.Single(asked).Workspace);
        using var answer = JsonDocument.Parse(File.ReadAllText(AnswerPath));
        Assert.Equal(["operation", "done", "code", "said"], answer.RootElement.EnumerateObject().Select(field => field.Name));
        Assert.Equal(("language", true, JsonValueKind.Null),
            (answer.RootElement.GetProperty("operation").GetString(), answer.RootElement.GetProperty("done").GetBoolean(),
                answer.RootElement.GetProperty("code").ValueKind));
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Requests, "h1.0123456789ab.json")));
        Assert.Empty(await watch.HonourAsync());
    }

    /// <summary>
    /// A loop that does not run the conversation leaves the request, and so does one that answers no setting; a judgement that fails
    /// is still answered, so the tool is never left to wait out its time, and never told nothing changed where it cannot know.
    /// </summary>
    [Fact]
    public async Task Another_loop_leaves_it_and_a_failed_judgement_is_still_answered()
    {
        Written();
        await using (var elsewhere = new SessionRequestWatch(_home, runsHere: _ => false, stop: _ => true, service: () => null, clock: () => Now)
                     {
                         Set = (_, _) => throw new InvalidOperationException("never asked"),
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
            Set = (_, _) => throw new IOException("the driver's file is held"),
        };

        Assert.Single(await failing.HonourAsync());
        using var answer = JsonDocument.Parse(File.ReadAllText(AnswerPath));
        Assert.False(answer.RootElement.GetProperty("done").GetBoolean());
        Assert.Equal(SettingAnswer.Failed, answer.RootElement.GetProperty("code").GetString());
        Assert.Contains("the driver's file is held", answer.RootElement.GetProperty("said").GetString());
    }

    /// <summary>
    /// The outcome record (the modes design §4.1): each request a loop answers is a line in the machine log, the setting, its value,
    /// where it was set, and whether it was changed with the refusal's code, never the answer's words.
    /// </summary>
    [Fact]
    public async Task Each_answer_is_kept_in_the_machine_log()
    {
        Written();
        using var log = new MachineLog(_home, "driver", () => Now);
        await using var watch = new SessionRequestWatch(_home, runsHere: _ => true, stop: _ => true, service: () => null, clock: () => Now)
        {
            Set = (request, _) => Task.FromResult(new SettingAnswer(request.Operation!, true, null, "Set.")),
            Log = log,
        };

        await watch.HonourAsync();
        log.Dispose();

        var line = File.ReadAllLines(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-20.driver.jsonl"))
            .Single(each => each.Contains("\"help.setting\"", StringComparison.Ordinal));
        using var read = JsonDocument.Parse(line);
        var data = read.RootElement.GetProperty("data");
        Assert.Equal(("h1", "language", "zh", "aurora", true),
            (data.GetProperty("session").GetString(), data.GetProperty("setting").GetString(), data.GetProperty("value").GetString(),
                data.GetProperty("workspace").GetString(), data.GetProperty("done").GetBoolean()));
        Assert.DoesNotContain("Set.", line);
    }
}
