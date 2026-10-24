using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// ASKSCOPE1b (D158's ASKSCOPE1 note point 4): each workspace's Ask Daoris room reads its own workspace. A named workspace has a
/// room folder of its own beside <c>help</c>, written from the facts projected to it before any count or limit, its first lines
/// saying what is the workspace's and what the machine's; <c>default</c>'s stays <c>help</c> and the whole machine, as before.
/// </summary>
/// <remarks>No process and no git: the facts are the fixture's (<see cref="HelpRoomScopeFixture"/>).</remarks>
public sealed class HelpRoomScopeTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-scope-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpRoomScopeTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Agents(string room) => File.ReadAllText(Path.Combine(room, "AGENTS.md"));

    /// <summary>
    /// <c>default</c>'s room is <c>help</c>, however it is spelled or unsaid, so every conversation started before keeps the folder
    /// its harness keeps it by. A named workspace's is a sibling, one name for one workspace whatever its letters' case, and no
    /// two workspaces' names ever meet, nor meet <c>help</c>: a name is kept letter for letter where it is plain, and every other
    /// character is spelled by its bytes.
    /// </summary>
    [Fact]
    public void Default_s_room_is_help_and_each_named_workspace_has_a_sibling_of_its_own()
    {
        var help = HelpRoom.PathOf(_home);
        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.HelpRoom), help);
        Assert.All(new string?[] { null, "", " ", "default", " Default " }, unsaid => Assert.Equal(help, HelpRoom.PathOf(_home, unsaid)));
        // A sibling of the default's room: under `work/rooms/` in a new home (HOMELAYOUT3), beside `help` in one from before.
        Assert.Equal(Path.Combine(Path.GetDirectoryName(help)!, "help-alpha"), HelpRoom.PathOf(_home, "alpha"));
        Assert.Equal(HelpRoom.PathOf(_home, "alpha"), HelpRoom.PathOf(_home, " ALPHA "));

        string[] names =
        [
            "alpha", "beta", "a b", "a-b", "a_b", "a.b", "a/b", "a_2db", "help", "..", "con", "游戏", "Ünïcode",
            new string('x', 60), new string('x', 61), new string('x', 60) + "y",
        ];
        var rooms = names.Select(name => HelpRoom.PathOf(_home, name)).ToList();
        Assert.Equal(names.Length, rooms.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(rooms, room =>
        {
            Assert.NotEqual(help, room, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(Path.GetDirectoryName(help), Path.GetDirectoryName(room));
            var folder = Path.GetFileName(room);
            Assert.StartsWith("help-", folder, StringComparison.Ordinal);
            Assert.True(folder.Length <= 64, folder);
            Assert.All(folder, letter => Assert.True(char.IsAsciiLetterLower(letter) || char.IsAsciiDigit(letter) || letter is '-' or '_', folder));
        });
    }

    /// <summary>
    /// 🔴 The room's facts are its workspace's, projected before any count or limit: its repositories and their lines, the sessions
    /// waiting and the list's groups, the asks and their count, the quests parked, held and paused, the finished work, the work
    /// and its recoveries, the landings and the kinds of work. The agents and their accounts, the driver and its cap stay the
    /// machine's. Since ASKSCOPE1c the checkouts it may read are its workspace's too: they are what its spawn is granted, so the room
    /// lists what its rules allow (<see cref="HelpCircleTests"/>).
    /// </summary>
    [Fact]
    public void A_workspace_s_room_says_its_own_facts_and_the_machine_s_alone_for_the_rest()
    {
        var whole = HelpRoomScopeFixture.Machine();
        var alpha = whole.Within("alpha");

        Assert.Null(whole.Workspace);
        Assert.Equal("alpha", alpha.Workspace);
        Assert.Equal(["engine", "site"], alpha.Repositories.Select(repository => repository.Name));
        Assert.Equal(["sa1"], alpha.Waiting.Select(session => session.Id));
        Assert.Equal(1, alpha.Asks);
        Assert.Equal(["aa1"], alpha.OpenAsks.Select(ask => ask.Id));
        Assert.Equal(["qa1"], alpha.Parked.Select(parked => parked.Quest));
        Assert.Empty(alpha.Held);
        Assert.Equal([new PausedBy(WorkScope.Quest, "qa1")], alpha.Paused);
        Assert.Equal(["alpha"], alpha.History!.Select(reading => reading.Workspace));
        Assert.Equal(["ua1"], alpha.Work!.Select(unit => unit.Unit));
        Assert.Equal(["`site` has no checkout here, so its recovery refs were not read"], alpha.WorkUnread);
        Assert.Equal(["engine"], alpha.WorkRecovery.Select(recovery => recovery.Repository));
        Assert.Equal(["engine"], alpha.Landed.Select(landed => landed.Repository));
        Assert.Equal(["docs"], alpha.WorkflowKinds.Select(kind => kind.Kind));
        // Another workspace's line not judged yet is no part of this room, so it says nothing is not read yet.
        Assert.False(alpha.NotReadYet.Any);
        Assert.True(whole.NotReadYet.Any);

        // The machine's: the same in every workspace's room.
        Assert.Equivalent(whole.Agents, alpha.Agents, strict: true);
        Assert.Equal((whole.Adapter, whole.Helper, whole.Cap), (alpha.Adapter, alpha.Helper, alpha.Cap));
        Assert.Equal(["engine"], alpha.Reads.Select(read => read.Name));
        Assert.Equal(["console", "engine", "tools"], whole.Reads.Select(read => read.Name));

        // `default`'s room is the whole machine, as before.
        Assert.Same(whole, whole.Within("default"));
        Assert.Same(whole, whole.Within(null));
        Assert.Equal(["engine", "site", "console", "tools"], whole.Repositories.Select(repository => repository.Name));
    }

    /// <summary>
    /// 🔴 Another workspace's sessions never fill a room's lists or counts: <c>beta</c>'s twelve ended sessions are all newer than
    /// <c>alpha</c>'s two, and the room lists ten. Projected first, <c>alpha</c>'s room lists and counts its own two; the machine's
    /// counts every one.
    /// </summary>
    [Fact]
    public void Another_workspace_s_sessions_never_fill_a_room_s_lists_or_counts()
    {
        var whole = HelpRoomScopeFixture.Machine();
        var alpha = whole.Within("alpha").Sessions!;
        var beta = whole.Within("beta").Sessions!;

        Assert.Equal((1, 0, 0, 0, 2), (alpha.You, alpha.Review, alpha.Working, alpha.Later, alpha.Ended));
        Assert.Equal([new HelpEndedSession("ea2", "site"), new HelpEndedSession("ea1", "engine")], alpha.Listed);
        Assert.Equal((12, HelpRoomMachineNow.EndedListed), (beta.Ended, beta.Listed.Count));
        Assert.Equal(15, whole.Sessions!.Ended);

        var room = HelpRoom.Render(whole.Within("alpha"));
        Assert.Contains("- Workspace `alpha`'s sessions by the Sessions list's groups, archived ones and Ask Daoris's own not counted: "
            + "1 waiting on you, 0 to review, 0 working, 0 resuming later, 2 ended.", room);
        Assert.Contains("- Ended sessions, newest first: `ea2` (in `site`), `ea1` (in `engine`).", room);
        Assert.Contains("- In workspace `alpha`: 1 session waits on the person; 1 ask waits for an answer.", room);
        Assert.DoesNotContain("`eb", room);
        Assert.DoesNotContain("`sb1`", room);
    }

    /// <summary>
    /// The room's first lines say which is which: what is the workspace's, named, and what is the machine's. <c>default</c>'s room
    /// says the machine as it always did.
    /// </summary>
    [Fact]
    public void A_workspace_s_room_says_its_scope_first()
    {
        var whole = HelpRoomScopeFixture.Machine();
        var room = HelpRoom.Render(whole.Within("alpha"));

        var scope = room.IndexOf("This conversation is workspace `alpha`'s", StringComparison.Ordinal);
        Assert.True(scope > 0 && scope < room.IndexOf("## What you may do", StringComparison.Ordinal), room);
        Assert.Contains("machine's, shared by every workspace", room);
        Assert.Contains("### Workspace `alpha`", room);
        Assert.DoesNotContain("### Workspace `beta`", room);
        Assert.DoesNotContain("### Workspace `default`", room);
        Assert.DoesNotContain("This conversation is workspace", HelpRoom.Render(whole));
    }

    /// <summary>
    /// 🔴 Two workspaces' rooms written at once never cross: each folder holds its own workspace's room, whole, as often as they
    /// are written side by side, and the machine's room beside them stays the machine's.
    /// </summary>
    [Fact]
    public void Two_workspaces_rooms_written_at_once_never_cross()
    {
        var whole = HelpRoomScopeFixture.Machine();
        var said = new Dictionary<string, string?>
        {
            ["alpha"] = HelpRoom.Render(whole.Within("alpha")),
            ["beta"] = HelpRoom.Render(whole.Within("beta")),
            ["default"] = HelpRoom.Render(whole),
        };

        var written = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 60, new ParallelOptions { MaxDegreeOfParallelism = 6 }, n =>
        {
            var workspace = (n % 3) switch { 0 => "alpha", 1 => "beta", _ => "default" };
            written.Add(HelpRoom.Prepare(_home, whole.Within(workspace)));
        });

        Assert.Equal(
            [HelpRoom.PathOf(_home), HelpRoom.PathOf(_home, "alpha"), HelpRoom.PathOf(_home, "beta")],
            written.Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(said["alpha"], Agents(HelpRoom.PathOf(_home, "alpha")));
        Assert.Equal(said["beta"], Agents(HelpRoom.PathOf(_home, "beta")));
        Assert.Equal(said["default"], Agents(HelpRoom.PathOf(_home)));
        Assert.DoesNotContain("### Workspace `beta`", Agents(HelpRoom.PathOf(_home, "alpha")));
        Assert.DoesNotContain("### Workspace `alpha`", Agents(HelpRoom.PathOf(_home, "beta")));
        Assert.All(new[] { "alpha", "beta" }, workspace =>
            Assert.Equal("@AGENTS.md\n", File.ReadAllText(Path.Combine(HelpRoom.PathOf(_home, workspace), "CLAUDE.md"))));
    }

    /// <summary>
    /// 🔴 The catch-up writes again only the rooms that said something not read yet, each from its own workspace's facts: one
    /// workspace's landing never writes another's room, nor a room never written.
    /// </summary>
    [Fact]
    public async Task The_catch_up_writes_again_only_its_own_workspace_s_room()
    {
        // `site`'s line not read yet: `alpha`'s room says so; `beta`'s has everything it says read.
        var unread = HelpRoomScopeFixture.Machine(["site"]);
        HelpRoom.Prepare(_home, unread.Within("alpha"));
        HelpRoom.Prepare(_home, unread.Within("beta"));
        Assert.True(HelpRoom.SaidNotReadYet(_home, "alpha"));
        Assert.False(HelpRoom.SaidNotReadYet(_home, "beta"));
        var beta = Path.Combine(HelpRoom.PathOf(_home, "beta"), "AGENTS.md");
        File.WriteAllText(beta, "beta's room as written\n");

        var landed = HelpRoomScopeFixture.Machine([]);
        var catchUp = new HelpRoomCatchUp(_home, () => landed);
        catchUp.Landed();

        var alpha = Path.Combine(HelpRoom.PathOf(_home, "alpha"), "AGENTS.md");
        // Read as the room's writer is read (READSHARE1): a plain read meeting the rename refuses, and failed this twice under load.
        await Poll.Until(() => AtomicFile.ReadText(alpha) == HelpRoom.Render(landed.Within("alpha")));
        await Poll.Until(() => !catchUp.Writing);
        Assert.False(HelpRoom.SaidNotReadYet(_home, "alpha"));
        Assert.Equal("beta's room as written\n", File.ReadAllText(beta));
        Assert.False(Directory.Exists(HelpRoom.PathOf(_home)));
    }
}
