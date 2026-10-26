using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The change watch over the machine's own watchers (FABRIC-EVENT2, the design §A5's open questions): what a watcher holds, a
/// request heard and answered within the debounce, what the loop writes never heard, and a folder replaced at its path found
/// and heard again. A handful of real file operations on a scratch folder, never a load: overflow and loss are the fast tests'
/// (<see cref="ChangeWatchTests"/>), on the fake source.
/// </summary>
/// <remarks>
/// Real watchers, whose events arrive on the machine's own time, a request answered within the debounce, and the handles of the
/// whole process measured, which every class beside it moves: so it runs alone, after the rest (<see cref="Alone"/>, PROCPAR1).
/// </remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class ChangeWatchProcessTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-change-real-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// What each watcher holds, measured: the process's handles and nonpaged memory before, with eight watchers open, and once
    /// they are disposed (canary 10's first numbers). Each holds a few handles, and gives them back.
    /// </summary>
    [Fact]
    public async Task Handles_and_nonpaged_memory_with_watchers_open_are_measured_and_given_back()
    {
        const int Roots = 8;
        var folders = Enumerable.Range(1, Roots).Select(i => Directory.CreateDirectory(Path.Combine(_home, $"root{i}")).FullName).ToList();
        using var self = Process.GetCurrentProcess();
        var before = Measure(self);

        var watch = new ChangeWatch();
        watch.Topic("measured", TimeSpan.FromHours(1), (_, _) => Task.FromResult(0));
        foreach (var folder in folders) watch.Watch("measured", Path.GetFileName(folder), folder, (_, _) => true);
        watch.Start();
        var open = Measure(self);

        await watch.DisposeAsync();
        var after = Measure(self);

        output.WriteLine(
            $"handles {before.Handles} -> {open.Handles} with {Roots} watchers open -> {after.Handles} disposed; "
            + $"nonpaged {before.Nonpaged} -> {open.Nonpaged} -> {after.Nonpaged} bytes; "
            + $"{(open.Handles - before.Handles) / (double)Roots:0.##} handles a watcher");
        Assert.True(open.Handles - before.Handles <= 8 * Roots, $"{open.Handles - before.Handles} handles for {Roots} watchers");
        Assert.True(after.Handles < open.Handles, $"{after.Handles} handles after, {open.Handles} open");
    }

    /// <summary>
    /// A request written as a terminal writes it is heard and answered within the debounce, with no listing for an hour; what
    /// the look then writes (its take, its answer, its machine-log line) wakes no second look.
    /// </summary>
    [Fact]
    public async Task A_request_is_answered_within_the_debounce_and_what_the_look_writes_wakes_nothing()
    {
        var requests = new SessionRequests(_home);
        Directory.CreateDirectory(requests.Folder);
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var inbox = processes.OpenInbox("s1");
        inbox.Attach(interrupt: null);
        using var log = new MachineLog(_home, "driver");
        await using var watch = new SessionRequestWatch(_home, processes, () => null, every: TimeSpan.FromHours(1)) { Log = log };
        var request = new SessionRequest("s1", SessionMove.Say, DateTimeOffset.UtcNow) { Text = "and the tests", Key = SessionRequests.NewKey() };

        var asked = Stopwatch.StartNew();
        requests.Write(request);
        await Poll.Until(() => inbox.State.Queued.Count == 1, () => "the words were never taken", Poll.Patience);
        var answered = asked.Elapsed;
        await Poll.Until(() => !watch.Changes!.Judging, within: Poll.Patience);
        await Task.Delay(ChangeWatch.Cap + ChangeWatch.Quiet);

        output.WriteLine($"a say answered {answered.TotalMilliseconds:0} ms after it was written; {watch.Changes!.Judgements} judgement(s)");
        Assert.True(answered < ChangeWatch.Cap, $"{answered.TotalMilliseconds:0} ms");
        Assert.Equal(1, watch.Changes.Judgements);
        var judged = Lines("change.judged");
        Assert.Equal(ChangeWhy.Hint, Assert.Single(judged).GetProperty("why").GetString());
    }

    /// <summary>
    /// An atomic replace, told as a deletion then a rename, is one judgement; a folder replaced at its path, which no watcher
    /// reports, is found at its reconciliation, reopened, and heard again.
    /// </summary>
    [Fact]
    public async Task A_replace_is_one_judgement_and_a_folder_replaced_at_its_path_is_reopened_and_heard_again()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_home, "watched")).FullName;
        var judged = new List<string>();
        await using var watch = new ChangeWatch();
        watch.Topic("watched", TimeSpan.FromMilliseconds(500), (wake, _) =>
        {
            lock (judged) judged.Add(wake.Why);
            return Task.FromResult(0);
        });
        watch.Watch("watched", "watched", folder, (name, kind) => kind != ChangeKind.Deleted && name.EndsWith(".json", StringComparison.Ordinal));
        watch.Start();
        string[] Judged(string why)
        {
            lock (judged) return [.. judged.Where(seen => seen == why)];
        }

        var file = Path.Combine(folder, "a.json");
        await File.WriteAllTextAsync(file, "{}");
        await Poll.Until(() => Judged(ChangeWhy.Hint).Length == 1, () => "the write was never heard", Poll.Patience);
        await Task.Delay(ChangeWatch.Cap);
        var heard = Judged(ChangeWhy.Hint).Length;

        AtomicFile.WriteText(file, """{ "again": true }""");
        await Poll.Until(() => Judged(ChangeWhy.Hint).Length == heard + 1, () => "the replace was never heard", Poll.Patience);
        await Task.Delay(ChangeWatch.Cap);
        Assert.Equal(heard + 1, Judged(ChangeWhy.Hint).Length);

        var aside = folder + ".aside";
        Directory.Move(folder, aside);
        Directory.CreateDirectory(folder);
        await Poll.Until(() => Judged(ChangeWhy.Reopened).Length > 0, () => "the replaced folder was never reopened", Poll.Patience);

        var before = Judged(ChangeWhy.Hint).Length;
        await File.WriteAllTextAsync(Path.Combine(folder, "b.json"), "{}");
        await Poll.Until(() => Judged(ChangeWhy.Hint).Length > before, () => "the new folder was never heard", Poll.Patience);
        lock (judged) output.WriteLine($"judged, in order: {string.Join(", ", judged)}");
    }

    private static (int Handles, long Nonpaged) Measure(Process self)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        self.Refresh();
        return (self.HandleCount, self.NonpagedSystemMemorySize64);
    }

    private JsonElement[] Lines(string @event) =>
    [
        .. Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl")
            .SelectMany(StubFile.Lines)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == @event)
            .Select(line => line.GetProperty("data")),
    ];
}
