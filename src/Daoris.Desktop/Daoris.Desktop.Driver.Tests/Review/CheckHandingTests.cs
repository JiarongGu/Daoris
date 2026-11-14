using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A verdict reaches an unfinished quest's session once under its check's key. A done quest's red waits for the person.
/// </summary>
public sealed class CheckHandingTests : IDisposable
{
    private static Task<string?> Taken(string _, CancellationToken ct) => Task.FromResult<string?>("Taken");
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "check-handing-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Theory]
    [InlineData(CheckAsk.ByAgent, CheckStates.Failed, null)]
    [InlineData(CheckAsk.ByDaoris, CheckStates.Failed, null)]
    [InlineData(CheckAsk.ByAgent, CheckStates.Stopped, null)]
    [InlineData(CheckAsk.ByDaoris, CheckStates.Stopped, null)]
    [InlineData(CheckAsk.ByAgent, CheckStates.Reached, CheckHanding.Quiet)]
    [InlineData(CheckAsk.ByDaoris, CheckStates.Reached, CheckHanding.Quiet)]
    public async Task A_done_quest_is_never_given_a_verdict_word(string by, string state, string? handed)
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(new CheckAsk("a1", "s1", "tests", null, by, At));
        files.WriteState(new CheckState("a1", state, At) { Tip = "abc123", Tail = "boom" });
        var words = new List<string>();

        for (var look = 0; look < 2; look++)
        {
            Assert.Empty(await CheckHanding.HandAsync(files, (_, text, _, _) =>
            {
                words.Add(text);
                return Task.FromResult<string?>("w1");
            }, CancellationToken.None, (_, _) => Task.FromResult<string?>("Done")));
        }

        Assert.Empty(words);
        Assert.Equal(handed, files.ReadState("a1")!.Handed);
        Assert.Equal("abc123", files.ReadState("a1")!.Tip);
    }

    [Fact]
    public async Task A_failed_check_on_a_taken_quest_still_reaches_its_session_once()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(new CheckAsk("a1", "s1", "tests", null, CheckAsk.ByAgent, At));
        files.WriteState(new CheckState("a1", CheckStates.Failed, At) { Exit = 1, Tail = "boom" });
        var words = new List<string>();

        for (var look = 0; look < 2; look++)
        {
            await CheckHanding.HandAsync(files, (session, text, key, _) =>
            {
                Assert.Equal("s1", session);
                Assert.Equal("check/a1", key);
                words.Add(text);
                return Task.FromResult<string?>("w1");
            }, CancellationToken.None, (_, _) => Task.FromResult<string?>("Taken"));
        }

        Assert.Contains("boom", Assert.Single(words));
        Assert.Equal("w1", files.ReadState("a1")!.Handed);
    }

    [Fact]
    public async Task A_finished_agents_check_is_said_once_under_its_key_and_marked_handed()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(new CheckAsk("a1", "s1", "tests", null, CheckAsk.ByAgent, At));
        files.WriteState(new CheckState("a1", CheckStates.Failed, At) { Exit = 1, Tail = "boom" });
        files.WriteAsk(new CheckAsk("a2", "s1", "web", null, CheckAsk.ByAgent, At));
        files.WriteState(new CheckState("a2", CheckStates.Running, At));
        var said = new List<(string Session, string Key, string Text)>();

        var handed = await CheckHanding.HandAsync(files, (session, text, key, _) =>
        {
            said.Add((session, key, text));
            return Task.FromResult<string?>("w1");
        }, CancellationToken.None, Taken);

        Assert.Equal(["a1"], handed);
        var word = Assert.Single(said);
        Assert.Equal(("s1", "check/a1"), (word.Session, word.Key));
        Assert.Contains("boom", word.Text);
        Assert.Equal("w1", files.ReadState("a1")!.Handed);
        Assert.Empty(await CheckHanding.HandAsync(files, (_, _, _, _) => Task.FromResult<string?>("w2"), CancellationToken.None, Taken));
    }

    [Fact]
    public async Task A_green_check_daoris_ran_on_its_own_says_nothing_and_a_refused_hand_is_tried_again()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(new CheckAsk("a1", "s1", "tests", null, CheckAsk.ByDaoris, At));
        files.WriteState(new CheckState("a1", CheckStates.Reached, At) { Exit = 0 });
        files.WriteAsk(new CheckAsk("a2", "s1", "web", null, CheckAsk.ByDaoris, At));
        files.WriteState(new CheckState("a2", CheckStates.Failed, At) { Exit = 1, Tail = "red" });
        var calls = 0;

        var refused = await CheckHanding.HandAsync(files, (_, _, _, _) =>
        {
            calls++;
            return Task.FromResult<string?>(null);
        }, CancellationToken.None, Taken);

        Assert.Empty(refused);
        Assert.Equal(1, calls);
        Assert.Null(files.ReadState("a2")!.Handed);
        Assert.Equal(CheckHanding.Quiet, files.ReadState("a1")!.Handed);

        Assert.Equal(["a2"], await CheckHanding.HandAsync(files, (_, _, _, _) => Task.FromResult<string?>("w9"), CancellationToken.None, Taken));
    }
}
