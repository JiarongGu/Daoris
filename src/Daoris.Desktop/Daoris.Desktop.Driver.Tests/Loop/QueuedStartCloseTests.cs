using System.ComponentModel;
using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1f (D181's PARALLEL1de note, D137 §2.4, D104): a run that goes on with the person's words waits for its place to start on
/// the loop's token, so a driver closing while it waits ends the wait. No run of it began, so what the start took is put back: an
/// ended record goes back as it ended with its words waiting, as a failed spawn's does, and nothing is handed on while the driver
/// closes; a park, which back to waiting on the person the ledger would clear of its words, is closed as the close's interrupted
/// take, its words kept, and carried on at the next start. A native run's go-on says the words it took never reached the session.
/// </summary>
/// <remarks>
/// The fast half: no process starts. The driver's continuation runs whole over the stand-in ledger, and every start in the test's
/// flow is made by a stand-in the gate hands it to (<see cref="ProcessStarts.Within"/>), which refuses it as the runtime refuses a
/// program that is not there. The loop closes as the run is told it opened, the last step before its start waits for a place; a
/// start through the shared gate cannot be held queued here, since the gate is every test's, so its wait sees the closed token at
/// once (<see cref="ProcessStarts"/>' own tests hold a queued wait and cancel it).
/// </remarks>
public sealed class QueuedStartCloseTests : IDisposable
{
    private const string Nowhere = "daoris-parallel1f-nowhere";

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-queued-close-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly List<string> _seen = [];

    private readonly QuestView _quest = new("q1", "game", "engine", "Serve it", "", "Taken");

    public QueuedStartCloseTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 An ended record the person wrote to: the move to working took it up, and the driver closed before its run started. It goes
    /// back as it ended, its note as it was and its words waiting on it, and nothing is handed on as a fallback.
    /// </summary>
    [Fact]
    public async Task An_ended_records_run_closed_before_its_start_goes_back_as_it_ended_with_its_words()
    {
        var tree = Tree("s-1");
        _ledger.Driven("s1", "q1", tree, state: "completed", adapter: "acp-stub");
        var said = await _ledger.Client().SayAsync("s1", "Port 8080.");
        Assert.True(said.Kept, said.Message);
        var record = Record("s1", tree, "completed", "It reached done.", reopens: true);

        var (run, fellBack, _) = await ContinueClosingAsync(record);

        Assert.Null(fellBack);
        Assert.NotNull(run);
        Assert.False(run.Opened);
        Assert.Null(run.Ended);
        Assert.Contains("session s1", run.Line, StringComparison.Ordinal);
        Assert.Equal(["working", "completed"], _ledger.MovesOf("s1"));
        var kept = _ledger.Session("s1");
        Assert.Equal("It reached done.", kept["note"]?.GetValue<string>());
        Assert.Null(kept["interrupted"]);
        Assert.Equal("Port 8080.", Assert.Single(kept["said"]!.AsArray())!["text"]!.GetValue<string>());
        Assert.DoesNotContain(Nowhere, _seen);
    }

    /// <summary>
    /// 🔴 A park the person answered: back to waiting on the person the ledger would clear its words (a park asks anew), so the
    /// close records it as the driver's, stopped and interrupted (D104), its words kept on it for the next start to go on with.
    /// Never ended as a fallback, which would carry its words to a new session as the driver closes.
    /// </summary>
    [Fact]
    public async Task A_parks_run_closed_before_its_start_is_the_closes_interrupted_take_with_its_words_kept()
    {
        var tree = Tree("s-1");
        _ledger.Park("s1", "q1", tree);
        var said = await _ledger.Client().SayAsync("s1", "Port 8080.");
        Assert.True(said.Kept, said.Message);
        var record = Record("s1", tree, "awaiting-person", "Which port should it listen on?", reopens: false);

        var (run, fellBack, _) = await ContinueClosingAsync(record);

        Assert.Null(fellBack);
        Assert.NotNull(run);
        Assert.Equal("stopped  session s1 (#q1 → engine): the driver was stopped.", run.Line);
        Assert.Equal(["working", "stopped"], _ledger.MovesOf("s1"));
        var kept = _ledger.Session("s1");
        Assert.True(kept["interrupted"]?.GetValue<bool>());
        Assert.Equal("Port 8080.", Assert.Single(kept["said"]!.AsArray())!["text"]!.GetValue<string>());
        Assert.DoesNotContain(Nowhere, _seen);
    }

    /// <summary>
    /// 🔴 A native run going on with words the person said while it worked (MSG1b): the driver closed while its next run waited for a
    /// place. That run never started, so the words it took are said never to have reached the session, never dropped unsaid.
    /// </summary>
    [Fact]
    public async Task A_go_on_run_closed_before_its_start_says_the_words_it_took_never_reached_the_session()
    {
        var driver = Driver();
        new HarnessConversations(_home).Keep("s1", "stub", "conv-1");
        var held = driver.Context.Processes.OpenInbox("s1");
        Assert.True(held.Hold(new ChatMessage("And log it.", [])));
        using var closed = new CancellationTokenSource();
        await closed.CancelAsync();

        using (Refusing())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DriverContinuations(driver.Context, driver).GoOnAsync(
                held, (_, _) => new ProcessStartInfo(Nowhere), AdapterSet.Built().Resolve("stub"), "s1", Path.Combine(_home, "s1.log"),
                refusesInput: null, drivesBrowser: false, exitCode: 0, used: null, resume: null, said: null, keep: [], left: [],
                closed.Token).WaitAsync(Bound));
        }

        Assert.Empty(_seen);
        var note = Assert.Single(driver.Context.Events.After("s1", 0).Events);
        Assert.Equal(SessionEventKind.Note, note.Kind);
        Assert.Equal(["words.unreached"], NoteAssert.Codes(note.Parts));
        Assert.Contains(UnreachedWhy.English(UnreachedWhy.EndedFirst), note.Text, StringComparison.Ordinal);
    }

    /// <summary>The record's continuation, whose loop closes as its run is told it opened, just before its start.</summary>
    private async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> ContinueClosingAsync(PriorSession record)
    {
        var driver = Driver();
        new HarnessConversations(_home).Keep(record.Session, "acp-stub", "conv-1");
        using var closing = new CancellationTokenSource();
        var start = new Consideration(_quest, StartVerdict.Start, "starting in `engine`.", Path.Combine(_home, "engine"), "default")
        {
            Resumes = record,
        };

        using (Refusing())
        {
            return await new DriverContinuations(driver.Context, driver).ContinueAsync(
                    start, record, new HarnessSelection(null), [], starting: null, onOpened: closing.Cancel, closing.Token, "acp-stub")
                .WaitAsync(Bound);
        }
    }

    private Daoris.Driver.Driver Driver() => new(
        _ledger.Client(),
        DriverConfig.Empty with { Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = [Nowhere] } },
        AdapterSet.Built(),
        _home);

    private string Tree(string name)
    {
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, name);
        Directory.CreateDirectory(tree);
        return tree;
    }

    /// <summary>The record as a look reads it: its state, note and the one word waiting on it, on the protocol door's stub.</summary>
    private static PriorSession Record(string session, string tree, string state, string note, bool reopens) =>
        new(session, tree, state, note, "engine")
        {
            Adapter = "acp-stub",
            Said = [new SaidWordView("w1", "Port 8080.", DateTimeOffset.UtcNow, [], reopens)],
        };

    /// <summary>Every start in the test's flow, refused as a program that is not there; git's too, which reads as no answer.</summary>
    private IDisposable Refusing() => ProcessStarts.Within(info =>
    {
        lock (_seen) _seen.Add(Path.GetFileNameWithoutExtension(info.FileName));
        throw new Win32Exception(2, "The system cannot find the file specified.");
    });
}
