using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// A room is shared by every conversation of its home workspace, so its files say no conversation's focus: each run is told its own
/// with its first words (<see cref="HelpFocusTold"/>), this machine's, a workspace's or a repository's with that repository's own gathered
/// under <i>In focus</i> from the facts projected to the focus's workspace.
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

    private string Written(HelpMachine machine) => File.ReadAllText(Path.Combine(HelpRoom.Prepare(_home, machine), "AGENTS.md"));

    /// <summary>🔴 D280 finding 12: two conversations of one room on different focuses, written in either order, read one room naming neither.</summary>
    [Fact]
    public void The_room_says_no_conversation_s_focus_whichever_wrote_it_last()
    {
        var whole = HelpRoomScopeFixture.Machine();
        var own = Written(whole.Within("alpha"));
        var engineThenBeta = (Written(whole.Within("alpha", Engine)), Written(whole.Within("alpha", HelpFocus.OfWorkspace("beta"))));
        var betaThenMachine = (Written(whole.Within("alpha", HelpFocus.OfWorkspace("beta"))), Written(whole.Within("alpha", HelpFocus.Machine)));

        Assert.All([engineThenBeta.Item1, engineThenBeta.Item2, betaThenMachine.Item1, betaThenMachine.Item2], room => Assert.Equal(own, room));
        Assert.DoesNotContain("Its focus is", own, StringComparison.Ordinal);
        Assert.DoesNotContain("## In focus", own, StringComparison.Ordinal);
        Assert.DoesNotContain("its focus is in workspace", own, StringComparison.Ordinal);
        Assert.Contains("Its focus, what the person chose this conversation to be about, is told in its first words", own, StringComparison.Ordinal);
        Assert.Equal(HelpRoom.PathOf(_home, "alpha"), HelpRoom.Prepare(_home, whole.Within("alpha", Engine)));
    }

    [Fact]
    public void A_focus_on_this_machine_is_told_as_every_workspace()
    {
        var told = HelpFocusTold.Words(HelpRoomScopeFixture.Machine(), "alpha", HelpFocus.Machine);
        Assert.StartsWith("Its focus is this machine: every workspace.", told, StringComparison.Ordinal);
        Assert.Contains("`daoris-knowledge` answers for every workspace", told, StringComparison.Ordinal);
    }

    [Fact]
    public void A_focus_on_another_workspace_is_told_with_where_its_tools_start_and_what_the_room_lists()
    {
        var told = HelpFocusTold.Words(HelpRoomScopeFixture.Machine(), "alpha", HelpFocus.OfWorkspace("beta"));
        Assert.Contains("Its focus is workspace `beta`: what the person chose this conversation to be about, and where its\ntools start.", told, StringComparison.Ordinal);
        Assert.Contains("This conversation is workspace `alpha`'s, and its focus is in workspace `beta`.", told, StringComparison.Ordinal);
        Assert.Contains("`daoris-knowledge` answers for `beta` unless a call names another workspace", told, StringComparison.Ordinal);
        Assert.DoesNotContain("## In focus", told, StringComparison.Ordinal);
    }

    [Fact]
    public void A_focus_on_a_repository_gathers_its_own_from_its_workspace_s_facts()
    {
        var told = HelpFocusTold.Words(HelpRoomScopeFixture.Machine(), "alpha", Engine);
        Assert.StartsWith("Its focus is repository `engine` in workspace `alpha`: what the person chose this conversation to be about.", told, StringComparison.Ordinal);
        var section = told[told.IndexOf("## In focus: repository `engine`", StringComparison.Ordinal)..];
        Assert.Contains("- Its line: `main` (the checkout's own).\n", section, StringComparison.Ordinal);
        Assert.Contains("- Waiting on the person: `sa1`.\n", section, StringComparison.Ordinal);
        Assert.Contains("- Ended: `ea1`.\n", section, StringComparison.Ordinal);
        Assert.Contains("- Quests parked by their failed sessions: `#qa1`.\n", section, StringComparison.Ordinal);
        Assert.Contains("- Landed: `feature/qa1-tiles` (session `sa0`).\n", section, StringComparison.Ordinal);
        Assert.DoesNotContain("ea2", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Its_home_s_own_focus_or_a_machine_not_read_is_told_by_name_alone()
    {
        var own = HelpFocusTold.Words(HelpRoomScopeFixture.Machine(), "alpha", HelpFocus.OfWorkspace("alpha"));
        Assert.StartsWith("Its focus is workspace `alpha`:", own, StringComparison.Ordinal);
        Assert.DoesNotContain("its focus is in workspace", own, StringComparison.Ordinal);
        var unread = HelpFocusTold.Words(null, "alpha", Engine);
        Assert.StartsWith("Its focus is repository `engine` in workspace `alpha`", unread, StringComparison.Ordinal);
        Assert.DoesNotContain("## In focus", unread, StringComparison.Ordinal);
    }

    /// <summary>The focus rides the standing block of a conversation's first words, said in its account.</summary>
    [Fact]
    public void The_standing_block_carries_the_focus_it_is_handed()
    {
        var told = StandingBlock.Compose(new StandingFacts { Workspace = "alpha", Focus = HelpFocusTold.Words(null, "alpha", Engine) });
        Assert.Contains("\n\nIts focus is repository `engine` in workspace `alpha`", told.Text, StringComparison.Ordinal);
        Assert.Contains("its focus", told.Account.Said, StringComparison.Ordinal);
        Assert.DoesNotContain("Its focus is", StandingBlock.Compose(new StandingFacts { Workspace = "alpha" }).Text, StringComparison.Ordinal);
    }
}
