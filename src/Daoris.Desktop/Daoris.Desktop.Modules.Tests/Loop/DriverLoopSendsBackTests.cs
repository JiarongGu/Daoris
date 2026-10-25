using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// CHATFLOW1e (the chat-flow design §6.1 (b)): the shell's loop hands its chat runner to every look as the seam a chat run's findings reach
/// their conversation through, <c>SendsBack</c>, as it hands <c>HandsOver</c>; the headless host hands none, and the findings wait. The runner
/// it hands answers for what it holds and only that: a conversation nothing here holds is <see cref="SendBack.NotHere"/>, never reserved,
/// and a record that is no conversation never goes on.
/// </summary>
/// <remarks>Its own scratch home under the repository's <c>_fixtures/</c>; nothing here sets the process's environment or starts a process.</remarks>
public sealed class DriverLoopSendsBackTests : IDisposable
{
    private readonly string _home = Path.Combine(RepositoryRoot(), "_fixtures", "sends-back", Guid.NewGuid().ToString("N")[..8]);

    public DriverLoopSendsBackTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 The loop's watch is built with the shell's chat runner as <c>SendsBack</c>, beside <c>HandsOver</c>, and the watch hands it to every
    /// look's driver: held on the loop's own source, since nothing short of a running shell builds the watch.
    /// </summary>
    [Fact]
    public void The_loop_hands_its_chat_runner_to_every_look_as_the_send_back_seam()
    {
        var loop = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "Loop", "DriverLoop.cs"));
        var watch = loop[loop.IndexOf("_watch = new DriverWatch(", StringComparison.Ordinal)..];
        watch = watch[..watch.IndexOf("};", StringComparison.Ordinal)];
        Assert.Contains("SendsBack = chat,", watch, StringComparison.Ordinal);
        Assert.Contains("using var chat = new ChatRunner(", loop[..loop.IndexOf("_watch = new DriverWatch(", StringComparison.Ordinal)], StringComparison.Ordinal);

        var looks = File.ReadAllText(SourceFiles.Driver("DriverWatch.cs"));
        Assert.Contains("HandsOver = HandsOver, SendsBack = SendsBack,", looks, StringComparison.Ordinal);
    }

    /// <summary>
    /// The runner it hands is the seam (<see cref="IChatSendBacks"/>): for a conversation it does not hold it answers not here, reserving and
    /// handing nothing, and no turn state; for a record that is no conversation it never goes on, and asks no hand.
    /// </summary>
    [Fact]
    public async Task The_shells_runner_answers_for_what_it_holds_and_only_that()
    {
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(new Records()));
        using var runner = new ChatRunner(
            client, AdapterSet.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.SessionPids)),
            events: new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        IChatSendBacks seam = runner;
        var hands = 0;
        var ask = new ChatSendBackAsk("c1", 0, ["c1"])
        {
            Hand = (_, _) =>
            {
                hands++;
                return Task.FromResult<SaidWordView?>(null);
            },
            Answered = _ => Task.CompletedTask,
        };

        Assert.Equal(SendBack.NotHere, (await seam.SendBackAsync(ask)).Outcome);
        Assert.Null(seam.TurnEnded("c1"));
        Assert.Equal(SendBack.CannotGoOn, (await seam.GoOnWithFindingsAsync(ask with { Session = "d1" })).Outcome);
        Assert.Equal(0, hands);
    }

    /// <summary>The service's records: one driven record, ended.</summary>
    private sealed class Records : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """[{"id":"d1","quest":"q1","repository":"engine","state":"completed","kind":"driven","said":[]}]""", Encoding.UTF8, "application/json"),
            });
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("the tests run from inside a checkout");
    }

    private static string SourceRoot() => Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");
}
