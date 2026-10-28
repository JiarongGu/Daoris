using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1h (D181 point 3): a terminal's requests are honoured beside each other, one session's claim each, never under one
/// gate across the home. A slow say holds only its own session: another session's request in the same look, or one that
/// arrives while it is slow, is taken and answered at once; the same session's next request keeps its asked-at order behind
/// it. Over the test seam and over the fake change source, so no process starts.
/// </summary>
public sealed class SessionRequestBesideTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-request-beside-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FakeChangeSource _source = new();

    /// <summary>What lets the slow say go on: set at each test's end, so a test that fails never leaves one waiting.</summary>
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Set once the slow say has begun, so a test asks its next request while that one is surely held.</summary>
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<string> _said = [];

    public SessionRequestBesideTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        _release.TrySetResult();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionRequests Requests => new(_home);

    private static SessionRequest Say(string session, string text, DateTimeOffset at) =>
        new(session, SessionMove.Say, at) { Text = text, Key = SessionRequests.NewKey() };

    /// <summary>
    /// A say that takes its time for <c>s1</c>'s first words, until the test releases it, and answers every other at once; the
    /// words are kept in the order the loop said them.
    /// </summary>
    private async Task<WordsHeld> SlowForS1Async(SessionRequest request, CancellationToken ct)
    {
        lock (_said) _said.Add(request.Text!);
        if (request.Session == "s1" && request.Text == "first")
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        return new WordsHeld(true, null, null);
    }

    private string[] Said()
    {
        lock (_said) return [.. _said];
    }

    /// <summary>The seam's loop: it runs both sessions, so a say and a stop for either are its own.</summary>
    private SessionRequestWatch Seam(List<string> stopped) =>
        new(_home, runsHere: _ => true, stop: id => { lock (stopped) stopped.Add(id); return true; }, service: () => null, clock: () => Now.AddSeconds(5))
        {
            Say = SlowForS1Async,
        };

    [Fact]
    public async Task A_slow_say_holds_no_other_sessions_request_in_the_same_look()
    {
        Requests.Write(Say("s1", "first", Now));
        Requests.Write(new SessionRequest("s2", SessionMove.Stop, Now.AddSeconds(1)));
        var stopped = new List<string>();
        await using var watch = Seam(stopped);

        var look = watch.HonourAsync();
        await _entered.Task.WaitAsync(Poll.Patience);
        await Poll.Until(() => { lock (stopped) return stopped.Contains("s2"); }, () => "s2's stop waited behind s1's slow say");

        Assert.False(look.IsCompleted);
        _release.TrySetResult();
        var honoured = await look;
        // Both honoured, and given back in the order they were asked, whichever ended first.
        Assert.Equal(["s1", "s2"], honoured.Select(request => request.Session));
    }

    [Fact]
    public async Task A_request_arriving_while_another_sessions_say_is_slow_is_taken_at_once()
    {
        Requests.Write(Say("s1", "first", Now));
        var stopped = new List<string>();
        await using var watch = Seam(stopped);
        var look = watch.HonourAsync();
        await _entered.Task.WaitAsync(Poll.Patience);

        var later = Say("s2", "also the changelog; 中文 too.", Now.AddSeconds(1));
        Requests.Write(later);
        var next = watch.HonourAsync();
        await Poll.Until(() => next.IsCompleted, () => "the next look waited on s1's slow say");

        Assert.Equal("s2", Assert.Single(await next).Session);
        Assert.True(Requests.AnswerOf(later)!.Sent);
        Assert.False(look.IsCompleted);
        _release.TrySetResult();
        Assert.Equal("s1", Assert.Single(await look).Session);
    }

    /// <summary>
    /// The claim is per session, so its own words wait their turn: a look while its say is slow neither says the next words
    /// beside it nor returns before they are said, and they are said after the first.
    /// </summary>
    [Fact]
    public async Task A_sessions_own_next_request_keeps_its_asked_order_behind_its_slow_say()
    {
        Requests.Write(Say("s1", "first", Now));
        var stopped = new List<string>();
        await using var watch = Seam(stopped);
        var look = watch.HonourAsync();
        await _entered.Task.WaitAsync(Poll.Patience);

        var second = Say("s1", "second", Now.AddSeconds(1));
        Requests.Write(second);
        var next = watch.HonourAsync();
        await Task.Delay(200);

        Assert.Equal(["first"], Said());
        Assert.False(next.IsCompleted);
        _release.TrySetResult();
        await Task.WhenAll(look, next).WaitAsync(Poll.Patience);
        Assert.Equal(["first", "second"], Said());
        Assert.True(Requests.AnswerOf(second)!.Sent);
        Assert.Empty(Requests.Pending(Now.AddSeconds(5)));
    }

    /// <summary>A loop whose registry hears both sessions' words at their doors, as conversations it runs do.</summary>
    private SessionProcesses Hearing()
    {
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
        foreach (var session in new[] { "s1", "s2" }) processes.OpenInbox(session).Attach(interrupt: null);
        return processes;
    }

    /// <summary>
    /// On the folder's watch (link 7): the look that began the slow say has returned, so the folder's next word is heard and its
    /// request answered while the say still waits.
    /// </summary>
    [Fact]
    public async Task On_the_watch_a_slow_say_holds_no_later_request_of_another_session()
    {
        await using var watch = new SessionRequestWatch(_home, Hearing(), () => null, every: TimeSpan.FromHours(1), source: _source)
        {
            Say = SlowForS1Async,
        };
        var handle = _source.On(Requests.Folder)!;
        var first = Say("s1", "first", DateTimeOffset.UtcNow);
        Requests.Write(first);
        handle.Raise(Path.GetFileName(Requests.PathOf(first)), ChangeKind.Renamed);
        await _entered.Task.WaitAsync(Poll.Patience);

        var later = Say("s2", "also the changelog", DateTimeOffset.UtcNow);
        Requests.Write(later);
        handle.Raise(Path.GetFileName(Requests.PathOf(later)), ChangeKind.Renamed);
        await Poll.Until(() => Requests.AnswerOf(later) is not null, () => "s2's words waited behind s1's slow say");

        Assert.Null(Requests.AnswerOf(first));
        _release.TrySetResult();
        await Poll.Until(() => Requests.AnswerOf(first) is not null, () => "s1's words were never answered");
    }

    /// <summary>A watch closing ends the say it began beside and waits for it, so nothing it began outlives it.</summary>
    [Fact]
    public async Task Disposing_the_watch_ends_a_say_begun_beside_and_waits_for_it()
    {
        var ended = false;
        var watch = new SessionRequestWatch(_home, Hearing(), () => null, every: TimeSpan.FromHours(1), source: _source)
        {
            Say = async (_, ct) =>
            {
                try
                {
                    _entered.TrySetResult();
                    await Task.Delay(Timeout.Infinite, ct);
                    return WordsHeld.Failed("never");
                }
                finally
                {
                    Volatile.Write(ref ended, true);
                }
            },
        };
        var first = Say("s1", "first", DateTimeOffset.UtcNow);
        Requests.Write(first);
        _source.On(Requests.Folder)!.Raise(Path.GetFileName(Requests.PathOf(first)), ChangeKind.Renamed);
        await _entered.Task.WaitAsync(Poll.Patience);

        await watch.DisposeAsync().AsTask().WaitAsync(Poll.Patience);

        Assert.True(Volatile.Read(ref ended));
    }
}
