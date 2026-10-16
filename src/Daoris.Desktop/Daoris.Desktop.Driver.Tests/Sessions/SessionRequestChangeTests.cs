using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The requests folder on the change watch (FABRIC-EVENT2, the design §A4.1, row 7): a request is taken when its folder says it
/// moved, not at a second's look; a listing a few seconds apart stays behind it, inside the asking door's ten seconds; and what
/// the loop itself writes there (a take, an answer, a press it read) wakes nothing. On the fake source; the same over a real
/// watcher is <see cref="ChangeWatchProcessTests"/>.
/// </summary>
public sealed class SessionRequestChangeTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-request-change-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FakeChangeSource _source = new();

    public SessionRequestChangeTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionRequests Requests => new(_home);

    private static SessionRequest Say(string session) =>
        new(session, SessionMove.Say, DateTimeOffset.UtcNow) { Text = "also the changelog; 中文 too.", Key = SessionRequests.NewKey() };

    /// <summary>A loop whose registry hears <c>s1</c>'s words at its door, as a conversation it runs does.</summary>
    private (SessionProcesses Processes, DrivenInbox Inbox) Hearing()
    {
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var inbox = processes.OpenInbox("s1");
        inbox.Attach(interrupt: null);
        return (processes, inbox);
    }

    /// <summary>With no listing for an hour, only the folder's word can take the request: it is answered within the debounce.</summary>
    [Fact]
    public async Task A_request_is_taken_when_its_folder_says_it_moved_with_no_listing()
    {
        var (processes, inbox) = Hearing();
        await using var watch = new SessionRequestWatch(_home, processes, () => null, every: TimeSpan.FromHours(1), source: _source);
        var request = Say("s1");
        Requests.Write(request);

        var asked = Stopwatch.StartNew();
        _source.On(Requests.Folder)!.Raise(Path.GetFileName(Requests.PathOf(request)), ChangeKind.Renamed);
        // The answer is written beside the request once the words are held, so it is what is waited for, read once.
        WordsHeld? answer = null;
        await Poll.Until(() => (answer = Requests.AnswerOf(request)) is not null, () => "the words were never answered", TimeSpan.FromSeconds(30));

        Assert.True(answer!.Sent);
        Assert.Equal("also the changelog; 中文 too.", Assert.Single(inbox.State.Queued).Text);
        Assert.True(asked.Elapsed < TimeSpan.FromHours(1), $"{asked.Elapsed}");
    }

    /// <summary>
    /// FABRIC-EVENT2c: a home with no <c>sessions/requests/</c> yet still has its folder watched at the loop's start, so the first
    /// request after an install is taken within the debounce, not at the 3 s listing.
    /// </summary>
    [Fact]
    public async Task A_home_with_no_requests_folder_has_it_made_and_watched_at_the_start()
    {
        Assert.False(Directory.Exists(Requests.Folder));
        var (processes, inbox) = Hearing();
        await using var watch = new SessionRequestWatch(_home, processes, () => null, every: TimeSpan.FromHours(1), source: _source);

        Assert.True(Directory.Exists(Requests.Folder));
        var handle = _source.On(Requests.Folder);
        Assert.NotNull(handle);

        var request = Say("s1");
        Requests.Write(request);
        handle!.Raise(Path.GetFileName(Requests.PathOf(request)), ChangeKind.Renamed);
        await Poll.Until(() => Requests.AnswerOf(request) is not null, () => "the words were never answered", TimeSpan.FromSeconds(30));
        Assert.Single(inbox.State.Queued);
    }

    /// <summary>
    /// With the source silenced, the listing behind it takes the request inside the asking door's ten seconds (SessionsCommand's
    /// <see cref="SessionsWorld.Wait"/>): the watch's default, unchanged by the test.
    /// </summary>
    [Fact]
    public async Task With_the_source_silenced_the_listing_takes_a_request_inside_the_asking_doors_wait()
    {
        var (processes, inbox) = Hearing();
        await using var watch = new SessionRequestWatch(_home, processes, () => null, source: _source);
        var request = Say("s1");
        Requests.Write(request);

        var asked = Stopwatch.StartNew();
        await Poll.Until(() => inbox.State.Queued.Count == 1, () => "the listing never took the words", TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(3), SessionRequestWatch.Listing);
        Assert.True(asked.Elapsed < TimeSpan.FromSeconds(10), $"{asked.Elapsed} waited");
    }

    /// <summary>
    /// What the folder hears: a request or a press arriving under its final name, never a deletion, a temporary, a take's or a
    /// withdrawal's name, or an answer, which are what the loop and the asking door write themselves.
    /// </summary>
    [Theory]
    [InlineData("s1.json", ChangeKind.Renamed, true)]
    [InlineData("s1.json", ChangeKind.Created, true)]
    [InlineData("s1.json", ChangeKind.Changed, true)]
    [InlineData("s1.a1b2c3d4e5f6.json", ChangeKind.Renamed, true)]
    [InlineData("0a1b2c3d4e5f.told", ChangeKind.Renamed, true)]
    [InlineData("s1.json", ChangeKind.Deleted, false)]
    [InlineData("0a1b2c3d4e5f.told", ChangeKind.Deleted, false)]
    [InlineData("s1.a1b2c3d4e5f6.answer.json", ChangeKind.Renamed, false)]
    [InlineData("s1.a1b2c3d4e5f6.answer.json", ChangeKind.Changed, false)]
    [InlineData("s1.json.0a1b2c3d.taking", ChangeKind.Renamed, false)]
    [InlineData("s1.json.0a1b2c3d.withdrawing", ChangeKind.Renamed, false)]
    [InlineData("s1.json.0a1b2c3d4e5f60718293a4b5c6d7e8f9.writing", ChangeKind.Created, false)]
    [InlineData("nested/s1.json", ChangeKind.Created, false)]
    public void The_folder_hears_what_arrives_and_never_what_the_loop_writes(string name, ChangeKind kind, bool heard)
    {
        Assert.Equal(heard, SessionRequestWatch.Hears(name, kind));
    }

    /// <summary>A loop's own take and answer, raised as the watcher would, wake no second look.</summary>
    [Fact]
    public async Task What_the_loop_writes_itself_wakes_no_look()
    {
        var (processes, inbox) = Hearing();
        await using var watch = new SessionRequestWatch(_home, processes, () => null, every: TimeSpan.FromHours(1), source: _source);
        var request = Say("s1");
        Requests.Write(request);
        var handle = _source.On(Requests.Folder)!;
        var name = Path.GetFileName(Requests.PathOf(request));
        handle.Raise(name, ChangeKind.Renamed);
        await Poll.Until(() => inbox.State.Queued.Count == 1, () => "the words were never taken", TimeSpan.FromSeconds(30));
        await Poll.Until(() => watch.Changes!.Judgements == 1 && !watch.Changes.Judging, within: TimeSpan.FromSeconds(30));

        handle.Raise(name + ".0a1b2c3d.taking", ChangeKind.Renamed);
        handle.Raise(name, ChangeKind.Deleted);
        handle.Raise(name.Replace(".json", ".answer.json", StringComparison.Ordinal), ChangeKind.Renamed);
        handle.Raise("0a1b2c3d4e5f.told", ChangeKind.Deleted);
        await Task.Delay(ChangeWatch.Cap + ChangeWatch.Quiet);

        Assert.Equal(1, watch.Changes.Judgements);
    }
}
