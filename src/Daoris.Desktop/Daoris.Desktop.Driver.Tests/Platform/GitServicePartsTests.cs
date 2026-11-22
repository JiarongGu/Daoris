using System.Text;
using Daoris.Driver;
using Daoris.Ports.Git;
using Daoris.Ports.Processes;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 A long answer (a log walk, a tree's listing) comes as numbered parts on its ask's id, each read as it arrives, the last marked with
/// git's exit; the read runs at the asker's pace and ends, waited out, once the asker cancels or stops reading.
/// </summary>
public sealed class GitServicePartsTests
{
    private static readonly TimeSpan Soon = Poll.Patience;

    private static GitService Service(int partChars = 64, int ahead = 2) =>
        new(new GitScheduler(4, 2, 2) { Said = (_, _) => { }, Tally = new GitAskTally() }) { PartChars = partChars, PartsAhead = ahead };

    private static GitRunAsk Ask(string repository) =>
        GitRunAsk.Plain(Path.GetFullPath(Path.Combine("_parts", repository)), GitWait.Read, "log") with { Endable = true };

    private static string Lines(int from, int count) =>
        string.Concat(Enumerable.Range(from, count).Select(at => $"commit {at:D6} subject\n"));

    [Fact]
    public async Task A_walks_parts_are_numbered_cut_at_line_ends_carry_the_asks_id_and_the_last_says_gits_exit()
    {
        var service = Service();
        var written = new StringBuilder();
        IReadOnlyList<GitServiceAsk>? openWhileRunning = null;
        var parts = new List<GitPart>();

        await foreach (var part in service.PartsAsync("_parts/a", "log", (take, token) => service.PlaceAsync(Ask("a"), Soon, _ =>
        {
            openWhileRunning = service.Open;
            for (var at = 0; at < 40; at += 4)
            {
                // Pieces cut mid-line, as a pipe hands them.
                var text = Lines(at, 4);
                var cut = text.Length / 3;
                written.Append(text);
                if (!take(text.AsMemory(0, cut)) || !take(text.AsMemory(cut))) return Task.FromResult(0);
            }

            return Task.FromResult(0);
        }, token), CancellationToken.None))
        {
            parts.Add(part);
        }

        Assert.True(parts.Count > 3);
        Assert.Equal(Enumerable.Range(1, parts.Count), parts.Select(part => part.Number));
        Assert.Single(parts, part => part.Last);
        Assert.True(parts[^1].Last);
        Assert.Equal(0, parts[^1].Exit);
        Assert.All(parts[..^1], part => Assert.EndsWith("\n", part.Text, StringComparison.Ordinal));
        Assert.All(parts[..^1], part => Assert.Null(part.Exit));
        Assert.Equal(written.ToString(), string.Concat(parts.Select(part => part.Text)));
        var open = Assert.Single(openWhileRunning!);
        Assert.All(parts, part => Assert.Equal(open.Id, part.Ask));
        Assert.Equal("log", open.Kind);
        // While its git is placed, the ask says the repository it is placed in and its deadline, not the asker's folder and none.
        Assert.Equal(Ask("a").Repository, open.Repository);
        Assert.Equal(Soon + GitService.BackgroundAllowance, open.Deadline);
        Assert.Empty(service.Open);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_walk_whose_git_outlived_its_kill_says_so_when_its_asker_cancels_or_stops_reading(bool cancels)
    {
        var service = Service();
        using var cancel = new CancellationTokenSource();

        var unended = await Assert.ThrowsAsync<ProcessUnendedException>(async () =>
        {
            await foreach (var part in service.PartsAsync("_parts/f", "log", async (take, token) =>
            {
                for (var at = 0; take(Lines(at * 4, 4).AsMemory()); at++) await Task.Yield();
                throw new ProcessUnendedException(41, "kill refused", token);
            }, cancel.Token))
            {
                if (!cancels) break;
                cancel.Cancel();
            }
        }).WaitAsync(Soon);

        Assert.Equal(41, unended.Pid);
        Assert.Empty(service.Open);
    }

    [Fact]
    public async Task A_walk_cancelled_mid_way_stops_gits_read_and_the_read_has_ended_when_the_parts_end()
    {
        var service = Service();
        var ended = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        var read = new List<GitPart>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var part in service.PartsAsync("_parts/b", "log", (take, token) => service.PlaceAsync(Ask("b"), Soon, async ran =>
            {
                try
                {
                    for (var at = 0; ; at++)
                    {
                        ran.ThrowIfCancellationRequested();
                        if (!take(Lines(at * 4, 4).AsMemory())) break;
                        await Task.Yield();
                    }

                    ended.SetResult(0);
                    return 0;
                }
                catch (OperationCanceledException)
                {
                    ended.SetResult(-1);
                    throw;
                }
            }, token), cancel.Token))
            {
                read.Add(part);
                if (read.Count == 2) cancel.Cancel();
            }
        }).WaitAsync(Soon);

        Assert.Equal(2, read.Count);
        Assert.True(ended.Task.IsCompleted, "the read is waited out before the parts end, so it takes nothing more");
        Assert.Empty(service.Open);
    }

    [Fact]
    public async Task A_walk_whose_asker_stops_reading_ends_its_read_as_a_cancel_does()
    {
        var service = Service();
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await foreach (var part in service.PartsAsync("_parts/c", "ls-tree", async (take, token) =>
        {
            try
            {
                for (var at = 0; take(Lines(at * 4, 4).AsMemory()); at++) await Task.Yield();
                return 0;
            }
            finally
            {
                ended.SetResult();
            }
        }, CancellationToken.None))
        {
            Assert.Equal(1, part.Number);
            break;
        }

        Assert.True(ended.Task.IsCompleted);
        Assert.Empty(service.Open);
    }

    [Fact]
    public async Task A_walk_runs_at_its_askers_pace_never_more_than_its_parts_ahead_of_what_was_read()
    {
        var service = Service(partChars: 64, ahead: 2);
        var taken = 0;
        var parts = service.PartsAsync("_parts/d", "log", async (take, token) =>
        {
            // Each piece is past a part's 64 characters, so it makes one part or two.
            for (var at = 0; ; at++)
            {
                Interlocked.Increment(ref taken);
                if (!take(Lines(at * 4, 4).AsMemory())) return 0;
                await Task.Yield();
            }
        }, CancellationToken.None).GetAsyncEnumerator();

        try
        {
            Assert.True(await parts.MoveNextAsync().AsTask().WaitAsync(Soon));
            await Poll.Until(() => Volatile.Read(ref taken) >= 3);
            // One read, two waiting, and the one whose part waits for room.
            Assert.InRange(Volatile.Read(ref taken), 3, 1 + 2 + 1);
            Assert.True(await parts.MoveNextAsync().AsTask().WaitAsync(Soon));
            Assert.Equal(2, parts.Current.Number);
        }
        finally
        {
            await parts.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_read_git_never_started_ends_in_one_last_part_saying_so()
    {
        var service = Service();

        var parts = new List<GitPart>();
        await foreach (var part in service.PartsAsync("_parts/e", "log", (_, _) => Task.FromResult(-1), CancellationToken.None)) parts.Add(part);

        var last = Assert.Single(parts);
        Assert.True(last.Last);
        Assert.Equal(-1, last.Exit);
        Assert.Equal("", last.Text);
    }

    [Fact]
    public async Task Only_a_log_walk_or_a_tree_listing_is_read_in_parts_through_the_flows_git()
    {
        using var git = FlowGit.Use(new ScriptedGit().On(ask => ask.Arguments.Contains("log") ? new GitAnswer(0, Lines(0, 30), "") : null));
        var service = Service();

        var text = new StringBuilder();
        await foreach (var part in service.PartsAsync(AppContext.BaseDirectory, ["log", "--format=%H %s"], CancellationToken.None)) text.Append(part.Text);

        Assert.Equal(Lines(0, 30), text.ToString());
        Assert.Throws<ArgumentException>(() => service.PartsAsync(AppContext.BaseDirectory, ["status", "--porcelain"], CancellationToken.None));
    }
}
