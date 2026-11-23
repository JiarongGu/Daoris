using Daoris.Adapters.Machine;
using Daoris.Ports.Git;

namespace Daoris.Adapters.Git.Tests;

/// <summary>A streamed read holds real git back at its asker's pace: a paused asker's git has written no more than the bound past what it read, and a stop ends it.</summary>
public sealed class GitProcessRunsPacedReadTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    // Each line is 101 bytes with its line feed; 30000 of them are 3 MB, far past what a pipe and the bound hold.
    private const int Lines = 30000;
    private const int LineBytes = 101;

    private readonly string _folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "paced-" + Guid.NewGuid().ToString("N"))).FullName;

    private string Progress => Path.Combine(_folder, "progress.txt");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A process of the test still holds it a moment: the build's folder is ignored and cleaned with it.
        }
    }

    private static GitProcessRuns Runs() => new(new MachineProcessHost(new StartPlaces(4, 3), (_, _) => null), new PlainGitPolicy());

    // A git alias, a shell loop that writes a line and then says how many it has written in a file: what git has written, seen from outside.
    private GitAsk Floods()
    {
        var progress = Progress.Replace('\\', '/');
        var loop = "!i=0; while [ $i -lt " + Lines + " ]; do printf 'line %095d\\n' $i; i=$((i+1)); echo $i > '" + progress + "'; done";
        return new GitAsk(_folder, ["-c", "alias.flood=" + loop, "flood"], GitWait.Read, Writes: false, Bound: TimeSpan.FromMinutes(5));
    }

    private int Written() => File.Exists(Progress) && int.TryParse(File.ReadAllText(Progress).Trim(), out var count) ? count : 0;

    private async Task<int> SettledAsync()
    {
        // Until it has not moved for a second and a half: a git held back stands still, one that is not goes on.
        var last = -1;
        var still = 0;
        for (var round = 0; round < 80 && still < 3; round++)
        {
            await Task.Delay(500);
            var now = Written();
            still = now == last ? still + 1 : 0;
            last = now;
        }

        return last;
    }

    [Fact]
    public async Task A_paused_asker_holds_git_back_to_the_bound_past_what_it_read_and_a_resumed_one_gets_the_whole()
    {
        using var gate = new ManualResetEventSlim(false);
        var read = 0L;
        var first = new TaskCompletionSource();
        var paused = true;
        var total = 0L;
        var reading = Task.Run(() => Runs().ReadAsync(Floods(), piece =>
        {
            Interlocked.Add(ref total, piece.Length);
            if (paused)
            {
                read = total;
                first.TrySetResult();
                gate.Wait(Patience);
            }

            return true;
        }, CancellationToken.None));

        await first.Task.WaitAsync(Patience);
        var held = await SettledAsync();

        // What git wrote past what the asker read is a pipe and the bound's pieces, never the answer.
        Assert.InRange(held, 1, Lines - 1);
        Assert.True((long)held * LineBytes - read < 512 * 1024, $"git wrote {held} lines ({held * LineBytes} bytes) with {read} read");

        paused = false;
        gate.Set();
        var exit = await reading.WaitAsync(Patience);

        Assert.Equal(0, exit);
        Assert.Equal((long)Lines * LineBytes, Interlocked.Read(ref total));
        Assert.Equal(Lines, Written());
    }

    [Fact]
    public async Task A_stop_ends_a_git_held_back_and_it_writes_no_more()
    {
        using var gate = new ManualResetEventSlim(false);
        using var cancel = new CancellationTokenSource();
        var first = new TaskCompletionSource();
        var reading = Task.Run(() => Runs().ReadAsync(Floods(), piece =>
        {
            first.TrySetResult();
            gate.Wait(cancel.Token);
            return true;
        }, cancel.Token));

        await first.Task.WaitAsync(Patience);
        var held = await SettledAsync();
        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(Patience));
        await Task.Delay(1500);
        Assert.Equal(held, Written());
        Assert.True(held < Lines);
    }
}
