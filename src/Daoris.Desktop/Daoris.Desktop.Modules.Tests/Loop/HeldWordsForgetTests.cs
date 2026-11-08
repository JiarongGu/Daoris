using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The words held for a record to end are kept by the queue, which owns the held file: a deleted or cleared session's words leave
/// the queue and the file together, so the next word held, which writes the file whole from the queue, does not bring one back.
/// </summary>
public sealed class HeldWordsForgetTests : DriverModuleBridge
{
    private const string Records =
        """
        [{"id":"c1","repository":"engine","state":"working","kind":"chat","created":"2026-10-03T08:00:00Z"},
         {"id":"c2","repository":"engine","state":"working","kind":"chat","created":"2026-10-03T08:00:01Z"}]
        """;

    private async Task<DriverLoop> HoldingAsync()
    {
        var service = new KeyedSayService { Records = Records, Winding = _ => true };
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        Assert.True((await loop.Words.SayAsync("c1", "Port 9090.", [], preface: null, "screen", CancellationToken.None, key: "press/c1"))!.Sent);
        Assert.True((await loop.Words.SayAsync("c2", "Port 8080.", [], preface: null, "screen", CancellationToken.None, key: "press/c2"))!.Sent);
        return loop;
    }

    private string Held() => File.ReadAllText(SessionWords.HeldPath(Home));

    [Fact]
    public async Task A_deleted_sessions_held_word_does_not_come_back_with_the_next_word_held()
    {
        var loop = await HoldingAsync();
        Assert.Contains("\"c1\"", Held());

        // The driver's delete of a session removes what the home keeps of it, its held words among them.
        var removed = new SessionHomeFiles(Home).Remove(["c1"], events: null);

        Assert.Contains(SessionHomeFiles.Held, removed.Went["c1"]);
        Assert.DoesNotContain("\"c1\"", Held());
        Assert.Contains("\"c2\"", Held());

        // The next word held writes the file whole from the queue: nothing for the deleted session is left in it to write.
        Assert.True((await loop.Words.SayAsync("c2", "And 7070.", [], preface: null, "screen", CancellationToken.None, key: "press/c2b"))!.Sent);

        var file = Held();
        Assert.DoesNotContain("\"c1\"", file);
        Assert.Contains("Port 8080.", file);
        Assert.Contains("And 7070.", file);
    }

    [Fact]
    public async Task The_last_held_word_forgotten_removes_the_file_and_a_session_with_none_changes_nothing()
    {
        await HoldingAsync();

        Assert.Empty(HeldWordsFile.Forget(Home, ["c9"]));
        Assert.Equal(["c1", "c2"], HeldWordsFile.Forget(Home, ["c1", "c2"]).Order());

        Assert.False(File.Exists(SessionWords.HeldPath(Home)));
    }

    [Fact]
    public async Task With_no_queue_in_the_process_the_file_is_edited_in_place()
    {
        var home = Path.Combine(Home, "no-queue");
        Directory.CreateDirectory(Path.GetDirectoryName(HeldWordsFile.PathOf(home))!);
        File.WriteAllText(HeldWordsFile.PathOf(home), """{ "held": [ { "session": "c1", "text": "a", "files": [], "door": "screen" }, { "session": "c2", "text": "b", "files": [], "door": "screen" } ] }""");

        Assert.Equal(["c1"], HeldWordsFile.Forget(home, ["c1"]));

        var kept = File.ReadAllText(HeldWordsFile.PathOf(home));
        Assert.DoesNotContain("\"c1\"", kept);
        Assert.Contains("\"c2\"", kept);
    }
}
