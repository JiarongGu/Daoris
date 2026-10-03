using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GIT1b (D147 §4.2): the reads' memory, least recently used first out and bounded by bytes. What it is keyed by, and what
/// is never kept, is <c>GitReadsAskTests</c>'; this holds the bound and the order.
/// </summary>
public sealed class GitReadCacheTests
{
    [Fact]
    public void An_answer_kept_is_handed_back_by_its_key_and_by_nothing_else()
    {
        var cache = new GitReadCache(1_000);
        var answer = new object();

        cache.Keep("a", answer, 100);

        Assert.True(cache.TryGet<object>("a", out var kept));
        Assert.Same(answer, kept);
        Assert.False(cache.TryGet<object>("b", out _));
        // Kept as one type, it is not handed back as another.
        Assert.False(cache.TryGet<string>("a", out _));
        Assert.Equal((1, 100L), (cache.Count, cache.Spent));
    }

    /// <summary>Past the bound, the answer asked for least recently goes first, and asking for one makes it recent.</summary>
    [Fact]
    public void Past_the_bound_the_least_recently_used_answer_goes_first()
    {
        var cache = new GitReadCache(300);
        cache.Keep("a", "A", 100);
        cache.Keep("b", "B", 100);
        cache.Keep("c", "C", 100);
        Assert.True(cache.TryGet<string>("a", out _));

        cache.Keep("d", "D", 100);

        Assert.False(cache.TryGet<string>("b", out _));
        Assert.True(cache.TryGet<string>("a", out _));
        Assert.True(cache.TryGet<string>("c", out _));
        Assert.True(cache.TryGet<string>("d", out _));
        Assert.Equal(300, cache.Spent);
    }

    /// <summary>One answer larger than the whole bound is not kept, and nothing is pushed out for it.</summary>
    [Fact]
    public void An_answer_larger_than_the_bound_is_not_kept()
    {
        var cache = new GitReadCache(300);
        cache.Keep("a", "A", 100);

        cache.Keep("huge", "H", 301);

        Assert.False(cache.TryGet<string>("huge", out _));
        Assert.True(cache.TryGet<string>("a", out _));
        Assert.Equal(100, cache.Spent);
    }

    [Fact]
    public void Keeping_a_key_again_replaces_its_answer_and_its_weight()
    {
        var cache = new GitReadCache(1_000);
        cache.Keep("a", "first", 100);

        cache.Keep("a", "second", 250);

        Assert.True(cache.TryGet<string>("a", out var kept));
        Assert.Equal("second", kept);
        Assert.Equal((1, 250L), (cache.Count, cache.Spent));
    }

    [Fact]
    public async Task Many_readers_at_once_keep_the_count_and_the_weight_whole()
    {
        var cache = new GitReadCache(10_000);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                cache.Keep($"{worker}:{i % 50}", "x", 10);
                cache.TryGet<string>($"{(worker + 1) % 8}:{i % 50}", out _);
            }
        })));

        Assert.Equal(400, cache.Count);
        Assert.Equal(4_000, cache.Spent);
    }

    [Fact]
    public void A_bound_below_nothing_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GitReadCache(-1));
        Assert.Equal(GitReadCache.DefaultBound, new GitReadCache().Bound);
    }
}
