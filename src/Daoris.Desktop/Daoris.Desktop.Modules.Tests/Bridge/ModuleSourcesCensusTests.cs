using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What the modules' sources hold of the driver's doors, read as a reviewer would: the screens compose none of the driver's pieces
/// themselves, and a conversation's end is written in one place. The behaviour beside them is <see cref="DriverModuleLinesTests"/>'
/// and <see cref="DriverLoopChatTakenUpTests"/>', which read no source and are the fast half's.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class ModuleSourcesCensusTests
{
    /// <summary>
    /// The screen's withdrawal composes none of the driver's pieces itself, so it cannot part from the terminal's door: read from the
    /// modules' sources, the judgement, the keep, the delete and the record are <see cref="LandedBranchWithdrawal"/>'s.
    /// </summary>
    [Fact]
    public void The_screens_withdrawal_is_the_drivers_one_withdrawal()
    {
        var lines = File.ReadAllText(SourceFiles.Find(SourceFiles.ModulesProject, "LinesRoutes.cs"));

        Assert.Contains(".WithdrawAsync(service, repository, branch, why, plan, cancellationToken)", lines, StringComparison.Ordinal);
        Assert.Contains("TreesWords.Withdraw(withdrawal)", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("KeepDiscardedAsync", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteJudgedAsync", lines, StringComparison.Ordinal);
    }

    /// <summary>
    /// The screen's discard composes none of the driver's pieces itself, so it cannot part from the terminal's door
    /// again. Read from the modules' sources, as a reviewer would: the keep, the checkout and the removal are
    /// <see cref="SessionBranchDiscard"/>'s.
    /// </summary>
    [Fact]
    public void The_screens_discard_is_the_drivers_one_discard()
    {
        var sources = SourceFiles.All(SourceFiles.ModulesProject, "*Routes.cs")
            .Select(path => (File: Path.GetFileName(path), Text: File.ReadAllText(path))).ToList();

        Assert.Contains(sources, source => source.File == "LinesRoutes.cs" && source.Text.Contains(".DiscardAsync(service, repository, branch, force,", StringComparison.Ordinal));
        Assert.All(sources, source =>
        {
            Assert.DoesNotContain("RemoveBranchAsync(", source.Text);
            Assert.DoesNotContain("BranchOfTree(", source.Text);
        });
    }

    /// <summary>
    /// Every door's press asks the sessions in use again inside each repository's hold, so a session a driver
    /// opened since the look is seen: Settings' *Bring up to date* and Ask Daoris's sync card alike. Read from
    /// the modules' sources, as a reviewer would, since only a live driver can open a session between the two.
    /// </summary>
    [Fact]
    public void Every_press_of_bringing_up_to_date_asks_the_sessions_in_use_again()
    {
        var presses = SourceFiles.All(SourceFiles.ModulesProject, "*.cs")
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return System.Text.RegularExpressions.Regex.Matches(source, @"\.SyncAsync\(repositories\b")
                    .Select(press => (File: Path.GetFileName(path), Call: source[press.Index..source.IndexOf(';', press.Index)]));
            })
            .ToList();

        Assert.Contains(presses, press => press.File == "TreesTidyDesk.cs");
        Assert.All(presses, press => Assert.True(press.Call.Contains("inUseNow:", StringComparison.Ordinal),
            $"{press.File} presses bringing up to date without asking the sessions in use again: {press.Call}"));
    }

    /// <summary>
    /// A start's end and a chat taken up by itself reach the page in one shape, written in one place: the routes that start
    /// a conversation hand their runs the loop's announcement rather than spelling the event again.
    /// </summary>
    [Fact]
    public void A_conversations_end_has_one_writer()
    {
        var spelled = SourceFiles.All(SourceFiles.ModulesProject, "*.cs")
            .Where(path => File.ReadAllText(path).Contains("\"SESSION_ENDED\"", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["DriverLoop.cs"], spelled);
    }
}
