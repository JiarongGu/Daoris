using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// The focus a turn takes (ASKSCOPE2c, `docs/2026-11-11-ask-scope-ux-design.md` §5 and §6): the next words apply the one waiting,
/// resolved against the registry then, a gone place widened, and the agent told in one line ahead of the person's words.
/// </summary>
public sealed class HelpFocusTurnTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-focus-turn-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly RepoView[] Registry =
    [
        new("engine", true, "C:/checkouts/engine", "Games"),
        new("api", true, "C:/checkouts/api", "Work"),
    ];

    private static readonly HelpFocus Games = HelpFocus.OfWorkspace("Games");
    private static readonly HelpFocus Engine = HelpFocus.OfRepository("Games", "engine");

    public HelpFocusTurnTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HelpConversations Kept => new(_home);

    [Fact]
    public void The_line_says_the_change_the_widening_and_the_machine_exactly()
    {
        Assert.Equal(
            "Focus is now repository `engine` in workspace `Games` (was workspace `Games`). Your reach is unchanged: name a workspace, or `all`, to look beyond it.",
            new HelpFocusChange(Games, Engine, null).Line);
        Assert.Equal(
            "Focus is now workspace `Games`: repository `engine` is no longer on this machine. Your reach is unchanged: name a workspace, or `all`, to look beyond it.",
            new HelpFocusChange(Engine, Games, Engine).Line);
        Assert.Equal("Focus is now this machine: every workspace (was workspace `Games`).", new HelpFocusChange(Games, HelpFocus.Machine, null).Line);
    }

    [Fact]
    public void The_next_words_apply_the_focus_waiting_append_it_to_the_foci_and_clear_it()
    {
        Kept.Change("h1", kept => kept with { FocusNext = Engine });
        var change = HelpFocusTurn.Apply(_home, "h1", "Games", Registry);
        Assert.Equal(new HelpFocusChange(Games, Engine, null), change);
        var after = Kept.Read("h1");
        Assert.Equal(Engine, after.Focus);
        Assert.Null(after.FocusNext);
        Assert.Equal([Games, Engine], after.Foci);
    }

    [Fact]
    public void Words_with_nothing_waiting_change_nothing_and_say_nothing()
    {
        Assert.Null(HelpFocusTurn.Apply(_home, "h1", "Games", Registry));
        Assert.Equal(Games, Kept.Read("h1").Focus);
        Assert.Null(HelpFocusTurn.Apply(_home, "h1", "Games", Registry));
    }

    [Fact]
    public void A_gone_repository_widens_to_its_workspace_and_a_gone_workspace_to_this_machine()
    {
        Kept.Change("h1", kept => kept with { FocusNext = HelpFocus.OfRepository("Games", "tools") });
        var widened = HelpFocusTurn.Apply(_home, "h1", "Games", Registry);
        Assert.Equal(new HelpFocusChange(Games, Games, HelpFocus.OfRepository("Games", "tools")), widened);
        Assert.True(widened!.Widened);

        Kept.Change("h2", kept => kept with { Focus = HelpFocus.OfWorkspace("Gone"), Foci = [HelpFocus.OfWorkspace("Gone")] });
        var gone = HelpFocusTurn.Apply(_home, "h2", "Gone", Registry);
        Assert.Equal(HelpFocus.Machine, gone!.To);
        Assert.Equal(HelpFocus.OfWorkspace("Gone"), gone.Gone);
        Assert.Equal(HelpFocus.Machine, Kept.Read("h2").Focus);
    }

    [Fact]
    public void The_line_goes_ahead_of_the_first_word_alone()
    {
        var words = new[]
        {
            new SaidWordView("w1", "And in its tests?", DateTimeOffset.UnixEpoch, [], false),
            new SaidWordView("w2", "And its docs?", DateTimeOffset.UnixEpoch, [], false),
        };
        var ahead = HelpFocusTurn.Ahead(words, "Focus is now this machine: every workspace (was workspace `Games`).");
        Assert.Equal("Focus is now this machine: every workspace (was workspace `Games`).\n\nAnd in its tests?", ahead[0].Text);
        Assert.Equal("w1", ahead[0].Id);
        Assert.Equal("And its docs?", ahead[1].Text);
    }
}
