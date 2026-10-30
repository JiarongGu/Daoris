using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 m-iv (the foundation design §4.5): the look's own news, a wait for an account and a quest's park, told once across restarts by
/// the said-set, which replaces the watch's first-look baseline; only the first run on a home takes what stands as said.
/// </summary>
public sealed class AttentionNewsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "attention-news", Guid.NewGuid().ToString("N")[..8]);

    public AttentionNewsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private News Desk() => new(_home, () => StoreSettled.Yes);

    private static AttentionEvent Wait(string until) => new(AttentionKind.Waiting, "", "engine, tools", Note: $"every account is cooling until {until}");

    private static AttentionEvent Park(string quest, string session) => new(AttentionKind.QuestParked, session, "engine") { Quest = quest };

    /// <summary>The first run on a home takes what stands as said and tells none; every later look, a restart's included, tells what it has not claimed.</summary>
    [Fact]
    public void The_first_run_takes_what_stands_as_said_and_a_restart_tells_the_new()
    {
        Assert.Empty(AttentionNews.Told(Desk(), [Wait("10:00"), Park("q1", "s3")], Now));

        // Restarted: the same standing news is told to no one; a new park is told once.
        Assert.Empty(AttentionNews.Told(Desk(), [Wait("10:00"), Park("q1", "s3")], Now.AddMinutes(1)));
        var told = AttentionNews.Told(Desk(), [Wait("10:00"), Park("q1", "s3"), Park("q2", "s7")], Now.AddMinutes(2));

        Assert.Equal(["q2"], told.Select(item => item.Quest));
        Assert.Empty(AttentionNews.Told(Desk(), [Park("q2", "s7")], Now.AddMinutes(3)));
    }

    /// <summary>A wait is one piece of news for each cool-off's end: the same wait is told once, and a wait for another end is told as its own.</summary>
    [Fact]
    public void A_wait_is_told_once_for_each_cool_off()
    {
        Assert.Empty(AttentionNews.Told(Desk(), [], Now));

        Assert.Single(AttentionNews.Told(Desk(), [Wait("10:00")], Now.AddMinutes(1)));
        Assert.Empty(AttentionNews.Told(Desk(), [Wait("10:00")], Now.AddMinutes(2)));
        Assert.Single(AttentionNews.Told(Desk(), [Wait("11:30")], Now.AddMinutes(3)));
    }

    /// <summary>A quest tried again that parks again on another session is a new park.</summary>
    [Fact]
    public void A_quest_that_parks_again_on_another_session_is_told_again()
    {
        Assert.Empty(AttentionNews.Told(Desk(), [], Now));

        Assert.Single(AttentionNews.Told(Desk(), [Park("q1", "s3")], Now.AddMinutes(1)));
        Assert.Single(AttentionNews.Told(Desk(), [Park("q1", "s6")], Now.AddMinutes(2)));
    }
}
