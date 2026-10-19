using System.Diagnostics;
using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPSTUCK1: Ask Daoris's start waits for the machine's reading for its room only so long. On the owner's loaded machine the
/// reading walked git across every tree and repository and did not answer, and the conversation placed for the person's words
/// sat at <c>queued</c>: nothing said, no agent started, the words held for it.
/// </summary>
/// <remarks>No process: the start gate's stand-in answers the agent's start (PROCSTART2), and the service is a stand-in.</remarks>
public sealed class HelpStartReadTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-read-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpStartReadTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly HelpMachine Machine = new() { Adapter = "acp-stub", Helper = "acp-stub", Cap = 1 };

    /// <summary>How long a test waits for a start that should be over well within it; the start waits a fifth of a second.</summary>
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(20);

    /// <summary>A reading of the machine that never answers, as the owner's did.</summary>
    private static Task<HelpMachine?> Never(CancellationToken ct) => new TaskCompletionSource<HelpMachine?>().Task;

    private (ChatRunner Runner, SessionEvents Events, ServiceClient Client, DriverConfig Config, HelpPlaced Placed) Placed(
        StandInService service, string id)
    {
        service.Seed(id, "queued");
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var client = new ServiceClient(service.Url, null);
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            HelpReadBound = TimeSpan.FromMilliseconds(200),
        };
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", "never-started.mjs"] },
        };
        var placed = new HelpPlaced(id, $"Ask Daoris `{id}` opened.")
        {
            Adapter = adapters.Resolve("acp-stub"),
            Selection = new HarnessSelection(Refusal: null),
        };
        return (runner, events, client, config, placed);
    }

    /// <summary>
    /// A room written before serves: the agent is started in it once the reading's bound passes, and its conversation says so
    /// first, by a code the page words. The reading left behind writes the room for the next conversation when it answers.
    /// </summary>
    [Fact]
    public async Task A_reading_that_does_not_answer_in_time_starts_on_the_room_as_last_written_and_says_so()
    {
        HelpRoom.Prepare(_home, Machine);
        await using var service = StandInService.Start(_home);
        var (runner, events, client, config, placed) = Placed(service, "help0001");
        using var _c = client;
        using var _r = runner;
        var late = new TaskCompletionSource<HelpMachine?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ProcessStartInfo? spawned = null;

        using (ProcessStarts.Within(info =>
               {
                   spawned = info;
                   return null;
               }))
        {
            await runner.RunHelpAsync(placed, config, _ => late.Task).WaitAsync(Within);
        }

        // Past the reading: the agent's start was made, in the room.
        Assert.NotNull(spawned);
        Assert.Equal(Path.GetFullPath(HelpRoom.PathOf(_home)), Path.GetFullPath(spawned!.WorkingDirectory));
        var said = Assert.Single(events.Page("help0001").Events, e => e.Kind == SessionEventKind.Note && e.Parts is { Count: > 0 });
        var part = Assert.Single(said.Parts!);
        Assert.Equal("help.room-as-written", part.Code);
        // Whole seconds, a part of one said as one.
        Assert.Equal(1L, Convert.ToInt64(part.Value("seconds"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains("room as last written", said.Text);

        // The reading answers later: the room is written from it, for the next conversation.
        late.SetResult(Machine with { Repositories = [new("written-late", "default") { Checkout = true }] });
        var agents = Path.Combine(HelpRoom.PathOf(_home), "AGENTS.md");
        await Poll.Until(() => File.ReadAllText(agents).Contains("`written-late`", StringComparison.Ordinal), within: Within);
    }

    /// <summary>No room was ever written: nothing can serve, so the start ends failed saying why, by a code, never queued.</summary>
    [Fact]
    public async Task A_reading_that_does_not_answer_in_time_with_no_room_written_ends_failed_saying_why()
    {
        await using var service = StandInService.Start(_home);
        var (runner, _, client, config, placed) = Placed(service, "help0002");
        using var _c = client;
        using var _r = runner;
        var ended = new List<(string, string)>();

        var start = await runner.RunHelpAsync(
            placed, config, Never, onEnded: (session, state) =>
            {
                ended.Add((session, state));
                return Task.CompletedTask;
            }).WaitAsync(Within);

        Assert.Null(start.SessionId);
        Assert.Contains("was not read within", start.Message);
        Assert.Equal("failed", service.State("help0002"));
        Assert.Contains("was not read within", service.Note("help0002"));
        Assert.Equal([("help0002", "failed")], ended);
    }
}
