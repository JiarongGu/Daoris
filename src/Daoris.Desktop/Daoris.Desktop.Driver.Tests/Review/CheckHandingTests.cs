using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A verdict reaches the session that asked, once, under its check's key, whatever its state and whatever its quest's.</summary>
public sealed class CheckHandingTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "check-handing-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Theory]
    [InlineData(CheckStates.Reached)]
    [InlineData(CheckStates.Failed)]
    [InlineData(CheckStates.Stopped)]
    public async Task A_finished_check_is_said_once_under_its_key_and_marked_handed(string state)
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(new CheckAsk("a1", "s1", "tests", null, At));
        files.WriteState(new CheckState("a1", state, At) { Exit = 1, Tail = "boom" });
        files.WriteAsk(new CheckAsk("a2", "s1", "web", null, At));
        files.WriteState(new CheckState("a2", CheckStates.Running, At));
        var said = new List<(string Session, string Key, string Text)>();

        var handed = await CheckHanding.HandAsync(files, (session, text, key, _) =>
        {
            said.Add((session, key, text));
            return Task.FromResult<string?>("w1");
        }, CancellationToken.None);

        Assert.Equal(["a1"], handed);
        var word = Assert.Single(said);
        Assert.Equal(("s1", "check/a1"), (word.Session, word.Key));
        Assert.Equal("w1", files.ReadState("a1")!.Handed);
        Assert.Empty(await CheckHanding.HandAsync(files, (_, _, _, _) => Task.FromResult<string?>("w2"), CancellationToken.None));
    }

    [Fact]
    public async Task A_refused_hand_is_tried_again_at_the_next_look()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(new CheckAsk("a1", "s1", "tests", null, At));
        files.WriteState(new CheckState("a1", CheckStates.Failed, At) { Exit = 1, Tail = "red" });

        var refused = await CheckHanding.HandAsync(files, (_, _, _, _) => Task.FromResult<string?>(null), CancellationToken.None);

        Assert.Empty(refused);
        Assert.Null(files.ReadState("a1")!.Handed);
        Assert.Equal(["a1"], await CheckHanding.HandAsync(files, (_, _, _, _) => Task.FromResult<string?>("w9"), CancellationToken.None));
    }

    [Fact]
    public void An_ask_Daoris_wrote_for_a_hand_back_is_swept_with_its_state_and_log_and_no_ask_of_a_session_is()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(new CheckAsk("a1", "s1", "tests", null, At));
        Directory.CreateDirectory(Path.GetDirectoryName(files.PathOf("old", "ask.json"))!);
        File.WriteAllText(files.PathOf("old", "ask.json"), """{"id":"old","session":"s1","gate":"comments","by":"daoris","at":"2026-11-12T10:00:00Z"}""");
        files.WriteState(new CheckState("old", CheckStates.Queued, At));

        Assert.Equal(["a1"], files.Asks().Select(ask => ask.Id));

        Assert.False(File.Exists(files.PathOf("old", "ask.json")));
        Assert.False(File.Exists(files.PathOf("old", "state.json")));
        Assert.True(File.Exists(files.PathOf("a1", "ask.json")));
    }
}
