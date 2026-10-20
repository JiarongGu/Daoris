using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORK1c (D188 point 6; the message-urgency design §6, §14's trap 7) for a driven session: the words the person says while it works
/// are kept on its record by its inbox before they are held, so a shell cut off under them leaves them there; the next shell's sweep ends
/// the record nothing runs, and its look goes on with them in their order in the same session, taking them off the record. The real
/// driver over the stand-in ledger, its run standing in for the session's go-on.
/// </summary>
public sealed class HeldWordsRestartTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-held-restart-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public HeldWordsRestartTests() => Directory.CreateDirectory(Tree);

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Tree => DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");

    private SessionProcesses Processes() => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>
    /// 🔴 Trap 7, a driven session: two words held for its turn's end, never attempted, are on its record when its shell is cut off; the
    /// next shell's sweep ends the record as interrupted, and its first look goes on with both, in the order said, as one run of the
    /// same session that takes them off the record.
    /// </summary>
    [Fact]
    public async Task A_driven_sessions_held_words_survive_a_shell_restart_and_go_in_their_order()
    {
        using var ledger = new StandInLedger();
        ledger.Register("engine", Path.Combine(_home, "engine"));
        ledger.Publish("q1", "engine");
        ledger.Driven("s1", "q1", Tree);
        var service = ledger.Client();

        // The first shell: the session's inbox, as its run opens it on a door that takes words at its turn's end.
        var inbox = Processes().OpenInbox("s1");
        inbox.Attach(interrupt: null);
        var door = new SaidDoor(service, "s1");
        var first = await inbox.KeepAsync(door, new ChatMessage("Also log the port.", []) { Key = "press/1" }, _closing.Token);
        var second = await inbox.KeepAsync(
            door, new ChatMessage("No, 9090.", []) { Level = MessageLevel.Ordinary, Key = "press/2" }, _closing.Token);

        Assert.Equal((InboxKeep.Held, InboxKeep.Held), (first.Outcome, second.Outcome));
        Assert.Equal(["w1", "w2"], inbox.State.Queued.Select(word => word.Id));
        var said = ledger.Session("s1")["said"]!.AsArray();
        Assert.Equal(
            [("w1", "urgent", false), ("w2", "ordinary", false)],
            said.Select(word => (word!["id"]!.GetValue<string>(), word["level"]!.GetValue<string>(), word["reopens"]!.GetValue<bool>())));

        // Cut off: nothing closes the inbox, and the next shell's sweep ends the record nothing on this machine runs.
        var swept = await Orphans.EndAsync(service, Processes(), ct: _closing.Token);
        Assert.Equal("s1", Assert.Single(swept).Id);

        var (driver, began) = Driver(service);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.Equal([["Also log the port.", "No, 9090."]], began);
        Assert.Empty(ledger.Session("s1")["said"]!.AsArray());
        Assert.Equal(["stopped", "working", "completed"], ledger.MovesOf("s1"));
    }

    /// <summary>
    /// The real driver over the ledger, its runs standing in for <c>Driver.ContinueAsync</c>, as <c>ReopenTellingTests</c>' is: each goes
    /// on with the words waiting on its record, takes them off it, and ends it completed, the texts it went on with kept in order.
    /// </summary>
    private (Daoris.Driver.Driver Driver, List<IReadOnlyList<string>> Began) Driver(ServiceClient service)
    {
        var began = new List<IReadOnlyList<string>>();
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub", Cap = 3, PollSeconds = 1, Trees = ["engine"] };
        var driver = new Daoris.Driver.Driver(
            service, config, adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = async (start, opened, ct) =>
            {
                var record = start.Resumes ?? throw new InvalidOperationException($"the stand-in only goes on; #{start.Quest.Id} was planned as a start");
                opened();
                await service.AdvanceAsync(record.Session, "working", Continuations.WorkingNoted, ct: ct);
                await service.TakenAsync(record.Session, [.. record.Waiting.Select(word => word.Id).OfType<string>()], ct: CancellationToken.None);
                await service.AdvanceAsync(record.Session, "completed", note: "Went on.", ct: CancellationToken.None);
                lock (began) began.Add([.. record.Waiting.Select(word => word.Text ?? "")]);
                return new StartRun(
                    $"completed  session {record.Session} (#{start.Quest.Id} → {start.Quest.To})", true,
                    new SessionEnded(record.Session, start.Quest.To, "completed", ByPerson: false, Quest: start.Quest.Id));
            },
        };
        return (driver, began);
    }
}
