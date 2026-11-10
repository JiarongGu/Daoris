using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// A start's receipt names the focus its room was written for and where its connector starts (`docs/2026-11-11-ask-scope-ux-design.md`
/// §6, <c>HELP_WIRED</c>): the focus's workspace, a repository's own, or <c>all</c> for this machine.
/// </summary>
public sealed class HelpWiredFocusTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-wired-focus-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    public HelpWiredFocusTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_receipt_keeps_the_focus_and_where_the_connector_starts_and_answers_them()
    {
        var engine = HelpFocus.OfRepository("Games", "engine");
        new HelpWired(_home).Keep("h1", new HelpReceipt { At = At, Door = "claude-code", Folder = "room", Focus = engine, Starts = "Games" });

        var read = Assert.Single(new HelpWired(_home).Read("h1"));
        Assert.Equal(engine, read.Focus);
        Assert.Equal("Games", read.Starts);

        var answered = HelpWired.Answer("h1", [read]).GetProperty("receipts")[0];
        Assert.Equal("repository", answered.GetProperty("focus").GetProperty("kind").GetString());
        Assert.Equal("engine", answered.GetProperty("focus").GetProperty("repository").GetString());
        Assert.Equal("Games", answered.GetProperty("starts").GetString());
    }

    [Fact]
    public void A_receipt_from_before_names_no_focus()
    {
        new HelpWired(_home).Keep("h1", new HelpReceipt { At = At, Door = "claude-code", Folder = "room" });
        var read = Assert.Single(new HelpWired(_home).Read("h1"));
        Assert.Null(read.Focus);
        Assert.Null(read.Starts);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, HelpWired.Answer("h1", [read]).GetProperty("receipts")[0].GetProperty("focus").ValueKind);
    }

    /// <summary>The connector starts at the focus's workspace; this machine and <c>default</c> bind none, which answers for all.</summary>
    [Fact]
    public void The_connector_starts_at_the_focus_s_workspace_and_this_machine_binds_none()
    {
        Assert.Null(HelpCircle.OfFocus(HelpFocus.Machine));
        Assert.Null(HelpCircle.OfFocus(HelpFocus.OfWorkspace("default")));
        Assert.Equal("Games", HelpCircle.OfFocus(HelpFocus.OfWorkspace("Games")));
        Assert.Equal("Games", HelpCircle.OfFocus(HelpFocus.OfRepository("Games", "engine")));
        Assert.Equal("Work", HelpPlacement.In(_home, HelpRoom.PathOf(_home, "Games"), [], "Games", HelpFocus.OfWorkspace("Work")).Circle);
        Assert.Null(HelpPlacement.In(_home, HelpRoom.PathOf(_home, "Games"), [], "Games", HelpFocus.Machine).Circle);
        Assert.Equal("Games", HelpPlacement.In(_home, HelpRoom.PathOf(_home, "Games"), [], "Games").Circle);
    }
}
