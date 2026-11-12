using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The verdict reaches the session as Daoris's word under the check's own key: an agent's check always, a check Daoris ran itself only
/// where it is not green, so a pass the session never asked about reopens nothing.
/// </summary>
public sealed class CheckHandingTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "check-handing-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
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
        }, CancellationToken.None);

        Assert.Equal(["a1"], handed);
        var word = Assert.Single(said);
        Assert.Equal(("s1", "check/a1"), (word.Session, word.Key));
        Assert.Contains("boom", word.Text);
        Assert.Equal("w1", files.ReadState("a1")!.Handed);
        Assert.Empty(await CheckHanding.HandAsync(files, (_, _, _, _) => Task.FromResult<string?>("w2"), CancellationToken.None));
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
        }, CancellationToken.None);

        Assert.Empty(refused);
        Assert.Equal(1, calls);
        Assert.Null(files.ReadState("a2")!.Handed);
        Assert.Equal(CheckHanding.Quiet, files.ReadState("a1")!.Handed);

        Assert.Equal(["a2"], await CheckHanding.HandAsync(files, (_, _, _, _) => Task.FromResult<string?>("w9"), CancellationToken.None));
    }
}
