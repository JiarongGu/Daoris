using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A read that counts the index waits for its reading only while it holds nothing; otherwise the reading runs behind it, one at a time.</summary>
public sealed class ReadingBehindTests
{
    [Fact]
    public async Task An_index_that_holds_nothing_is_read_before_the_count()
    {
        var reads = 0;
        var behind = new ReadingBehind(_ => Task.FromResult(false), async _ =>
        {
            await Task.Delay(20);
            reads++;
        });

        await behind.ReadyAsync(CancellationToken.None);
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task An_index_that_holds_a_reading_answers_at_once_and_reads_once_behind_however_often_it_is_asked()
    {
        var reads = 0;
        var release = new TaskCompletionSource();
        var behind = new ReadingBehind(_ => Task.FromResult(true), async _ =>
        {
            Interlocked.Increment(ref reads);
            await release.Task;
        });

        var asked = Task.WhenAll(Enumerable.Range(0, 5).Select(_ => behind.ReadyAsync(CancellationToken.None)));
        Assert.Same(asked, await Task.WhenAny(asked, Task.Delay(Poll.Patience)));
        await UntilAsync(() => Task.FromResult(Volatile.Read(ref reads) >= 1));
        await behind.ReadyAsync(CancellationToken.None);
        Assert.Equal(1, Volatile.Read(ref reads));
        release.SetResult();

        // Once the reading ended, the next read starts the next one.
        await UntilAsync(async () =>
        {
            await behind.ReadyAsync(CancellationToken.None);
            return Volatile.Read(ref reads) == 2;
        });
    }

    private static async Task UntilAsync(Func<Task<bool>> holds)
    {
        var deadline = DateTime.UtcNow + Poll.Patience;
        while (!await holds())
        {
            Assert.True(DateTime.UtcNow < deadline, "never held");
            await Task.Delay(10);
        }
    }
}
