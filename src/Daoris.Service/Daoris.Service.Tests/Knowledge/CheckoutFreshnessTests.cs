using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The knowledge server's freshness on a watch: a checkout's moving is marked, the next search reads it, and a silenced
/// watch is caught by its deadline, never by a timer of the search's own. The watch here is the silent one a test marks by
/// hand; the machine's real watchers are <see cref="CheckoutWatchTests"/>.
/// </summary>
public sealed class CheckoutFreshnessTests : FreshnessCheckout
{
    [Fact]
    public async Task An_edit_that_was_heard_is_read_at_the_next_search_with_no_time_gone_by()
    {
        var checkout = Checkout();
        var time = new SteppedTime(DateTimeOffset.Parse("2026-10-17T10:00:00Z"));
        using var freshness = CheckoutFreshness.Silent();
        var service = Serve(checkout, freshness, time);
        Assert.False(await Finds(service));

        Write("served/docs/later.md", "# Zeppelins\n\nWritten while the session ran.\n");
        Assert.False(await Finds(service));

        freshness.MarkStale();
        Assert.True(await Finds(service));
    }

    [Fact]
    public async Task A_watched_checkout_is_not_read_again_by_the_minute_only_by_its_deadline()
    {
        var checkout = Checkout();
        var time = new SteppedTime(DateTimeOffset.Parse("2026-10-17T10:00:00Z"));
        using var freshness = CheckoutFreshness.Silent();
        var service = Serve(checkout, freshness, time);
        Assert.False(await Finds(service));

        // The watch heard nothing: a silenced source. The minute that once caught it no longer does.
        Write("served/docs/later.md", "# Zeppelins\n\nWritten while the watch was silent.\n");
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.False(await Finds(service));

        time.Advance(CheckoutFreshness.Deadline - TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(1));
        Assert.False(await Finds(service));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await Finds(service));
    }

    [Fact]
    public async Task A_stale_mark_is_taken_once_so_a_second_search_does_not_read_again()
    {
        var checkout = Checkout();
        var time = new SteppedTime(DateTimeOffset.Parse("2026-10-17T10:00:00Z"));
        using var freshness = CheckoutFreshness.Silent();
        var service = Serve(checkout, freshness, time);
        Assert.False(await Finds(service));

        freshness.MarkStale();
        Assert.False(await Finds(service));
        Assert.False(freshness.IsStale);

        Write("served/docs/later.md", "# Zeppelins\n\nWritten after the read the mark asked for.\n");
        Assert.False(await Finds(service));
    }

    [Fact]
    public void An_overflow_marks_stale_and_a_lost_watcher_marks_stale_and_falls_back_to_the_minute()
    {
        using var freshness = CheckoutFreshness.Silent();
        Assert.True(freshness.Watching);
        Assert.False(freshness.IsStale);

        freshness.Failed(new InternalBufferOverflowException());
        Assert.True(freshness.IsStale);
        Assert.True(freshness.Watching);

        Assert.True(freshness.Take());
        freshness.Failed(new IOException("the handle is gone"));
        Assert.True(freshness.IsStale);
        Assert.False(freshness.Watching);
    }

    [Fact]
    public async Task A_lost_watch_leaves_the_checkout_on_the_minute_as_it_was()
    {
        var checkout = Checkout();
        var time = new SteppedTime(DateTimeOffset.Parse("2026-10-17T10:00:00Z"));
        using var freshness = CheckoutFreshness.Silent();
        var service = Serve(checkout, freshness, time);
        Assert.False(await Finds(service));
        freshness.Failed(new IOException("gone"));
        await service.SearchAsync(new KnowledgeQuery("probe"));

        Write("served/docs/later.md", "# Zeppelins\n\nWritten after the loss.\n");
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.True(await Finds(service));
    }
}
