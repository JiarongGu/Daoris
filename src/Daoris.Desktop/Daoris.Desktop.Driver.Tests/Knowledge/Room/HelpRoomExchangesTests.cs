using System.Text.Json.Nodes;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The room's <see cref="HelpRoomExchanges"/>: Ask Daoris reads what each line waits on as a fact, from the
/// exchanges' one read, so it says what waits on what as listed and proposes no act for an answer another agent owes.
/// </summary>
public sealed class HelpRoomExchangesTests
{
    /// <summary>A line's waits as the verb words them, a source not read said; a room with nothing waiting says nothing of it.</summary>
    [Fact]
    public void COMMS1i_the_room_lists_what_each_line_waits_on_and_a_source_it_could_not_read()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            Waits = new HelpWaits([new HelpWait("s12", "engine", "default", "helper h7 · codex-acp · asked 16:29 · asked · delivery kept")], ["piece"]),
        });

        Assert.Contains("## What waits on what", room, StringComparison.Ordinal);
        Assert.Contains("s12 (engine): helper h7 · codex-acp · asked 16:29 · asked · delivery kept\n", room, StringComparison.Ordinal);
        Assert.Contains("propose no act for one", room, StringComparison.Ordinal);
        Assert.Contains("- not read: piece, so what a line waits on may be more.", room, StringComparison.Ordinal);
        Assert.DoesNotContain("## What waits on what", HelpRoom.Render(HelpRoomFixture.Machine with { Waits = HelpWaits.None }), StringComparison.Ordinal);
    }

    /// <summary>Not read yet, or not read at all, the room says so and says no wait, never that nothing waits.</summary>
    [Fact]
    public void COMMS1i_a_room_whose_waits_were_not_read_says_so()
    {
        var notYet = HelpRoom.Render(HelpRoomFixture.Machine with { NotReadYet = HelpNotReadYet.None with { Waits = true } });
        var never = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("What each line waits on is not read yet, so do not say what any session waits on", notYet, StringComparison.Ordinal);
        Assert.Contains("What each line waits on was not read, so do not say what any session waits on", never, StringComparison.Ordinal);
    }

    /// <summary>
    /// The facts' read over the home's files and the records (<see cref="AtWorkExchangeReading.Waits"/>): a line's head waits on the
    /// helper its predecessor asked; a helper, the record continued and a line that waits on nothing say nothing; a reviewer reading is a wait.
    /// </summary>
    [Fact]
    public void COMMS1i_the_facts_read_what_each_line_waits_on_from_the_one_read()
    {
        var home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "help-room-waits", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var files = new HelperFiles(home);
            Directory.CreateDirectory(Path.GetDirectoryName(files.PathOf("h7", "ask"))!);
            File.WriteAllText(files.PathOf("h7", "ask"), new JsonObject
            {
                ["id"] = "h7", ["asker"] = "s1", ["question"] = "Is the lock held?", ["agent"] = "codex-acp", ["askedBy"] = "agent",
                ["at"] = "2026-11-04T16:29:00Z",
            }.ToJsonString());
            SessionRecord[] records =
            [
                new("s1", "engine", "completed"),
                new("s2", "engine", "working") { From = "s1", Workspace = "work" },
                new("h1", "engine", "working") { Serves = "s2" },
                new("s3", "atlas", "completed"),
                new("r1", "atlas", "working") { Serves = "s3", Opinion = "o1", Adapter = "codex-acp" },
                new("s4", "docs", "completed"),
            ];

            var waits = AtWorkExchangeReading.Waits(home, records, quests: []);

            Assert.Equal(["s2", "s3"], waits.Waits.Select(wait => wait.Session).ToArray());
            Assert.Equal(("engine", "work"), (waits.Waits[0].Repository, waits.Waits[0].Workspace));
            Assert.StartsWith("helper h7 · codex-acp", waits.Waits[0].Line, StringComparison.Ordinal);
            Assert.Contains("asked by part s1", waits.Waits[0].Line, StringComparison.Ordinal);
            Assert.StartsWith("reviewer o1 · codex-acp", waits.Waits[1].Line, StringComparison.Ordinal);
            Assert.Empty(waits.Unread);
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }
}
