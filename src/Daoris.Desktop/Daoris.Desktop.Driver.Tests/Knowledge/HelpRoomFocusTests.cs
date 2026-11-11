using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// The room is written for its conversation's focus (`docs/2026-11-11-ask-scope-ux-design.md` §5): this machine's whole, a
/// workspace's projection, or a repository's workspace with that repository's own gathered first under <i>In focus</i>; always in
/// its home's folder, its first lines naming the focus and the reach.
/// </summary>
/// <remarks>No process and no git: the facts are the fixture's (<see cref="HelpRoomScopeFixture"/>).</remarks>
public sealed class HelpRoomFocusTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-room-focus-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly HelpFocus Engine = HelpFocus.OfRepository("alpha", "engine");

    public HelpRoomFocusTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_focus_on_this_machine_reads_the_whole_machine_in_its_home_s_room()
    {
        var machine = HelpRoomScopeFixture.Machine().Within("alpha", HelpFocus.Machine);
        Assert.Null(machine.Workspace);
        Assert.Equal("alpha", machine.Home);
        Assert.Equal(["console", "engine", "site", "tools"], machine.Repositories.Select(repository => repository.Name).Order(StringComparer.Ordinal));
        Assert.Equal(HelpRoom.PathOf(_home, "alpha"), HelpRoom.Prepare(_home, machine));
        Assert.Contains("Its focus is this machine: every workspace.", HelpRoom.Render(machine), StringComparison.Ordinal);
    }

    [Fact]
    public void A_focus_on_another_workspace_lists_that_workspace_in_the_home_s_room()
    {
        var machine = HelpRoomScopeFixture.Machine().Within("alpha", HelpFocus.OfWorkspace("beta"));
        Assert.Equal("beta", machine.Workspace);
        Assert.Equal(["console"], machine.Repositories.Select(repository => repository.Name));
        Assert.Equal(HelpRoom.PathOf(_home, "alpha"), HelpRoom.Prepare(_home, machine));
        var room = HelpRoom.Render(machine);
        Assert.Contains("This conversation is workspace `alpha`'s, and its focus is in workspace `beta`.", room, StringComparison.Ordinal);
        Assert.Contains("Its focus is workspace `beta`: what the person chose this conversation to be about, and where its\ntools start.", room, StringComparison.Ordinal);
        Assert.Contains("`daoris-knowledge` answers for `beta` unless a call names another workspace", room, StringComparison.Ordinal);
    }

    [Fact]
    public void A_focus_on_a_repository_gathers_its_own_first_and_lists_the_rest_of_its_workspace()
    {
        var machine = HelpRoomScopeFixture.Machine().Within("alpha", Engine);
        Assert.Equal("alpha", machine.Workspace);
        Assert.Equal(["engine", "site"], machine.Repositories.Select(repository => repository.Name).Order(StringComparer.Ordinal));
        var room = HelpRoom.Render(machine);
        var inFocus = room.IndexOf("## In focus: repository `engine`", StringComparison.Ordinal);
        var mayDo = room.IndexOf("## What you may do", StringComparison.Ordinal);
        Assert.True(inFocus > 0 && inFocus < mayDo, room);
        var section = room[inFocus..mayDo];
        Assert.Contains("- Its line: `main` (the checkout's own).\n", section, StringComparison.Ordinal);
        Assert.Contains("- Waiting on the person: `sa1`.\n", section, StringComparison.Ordinal);
        Assert.Contains("- Ended: `ea1`.\n", section, StringComparison.Ordinal);
        Assert.Contains("- Quests parked by their failed sessions: `#qa1`.\n", section, StringComparison.Ordinal);
        Assert.Contains("- Landed: `feature/qa1-tiles` (session `sa0`).\n", section, StringComparison.Ordinal);
        Assert.DoesNotContain("ea2", section, StringComparison.Ordinal);
        Assert.Contains("Its focus is repository `engine` in workspace `alpha`: what the person chose this conversation to be about.", room, StringComparison.Ordinal);
    }

    [Fact]
    public void A_workspace_focus_or_none_writes_no_in_focus_and_names_the_home_s_own()
    {
        var whole = HelpRoomScopeFixture.Machine();
        var own = HelpRoom.Render(whole.Within("alpha"));
        Assert.DoesNotContain("## In focus", own, StringComparison.Ordinal);
        Assert.Contains("Its focus is workspace `alpha`:", own, StringComparison.Ordinal);
        Assert.Equal(own, HelpRoom.Render(whole.Within("alpha", HelpFocus.OfWorkspace("alpha"))));
        Assert.Contains("Its focus is this machine: every workspace.", HelpRoom.Render(whole), StringComparison.Ordinal);
    }

    [Fact]
    public void A_room_written_for_a_focus_is_written_again_for_it()
    {
        var whole = HelpRoomScopeFixture.Machine();
        HelpRoom.Prepare(_home, whole.Within("alpha", Engine));
        Assert.Equal(Engine, HelpRoom.FocusWritten(_home, "alpha"));
        HelpRoom.Prepare(_home, whole.Within("alpha"));
        Assert.Null(HelpRoom.FocusWritten(_home, "alpha"));
    }
}
