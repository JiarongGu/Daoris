using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What `daoris-driver` was asked for (DRV8a, D104). Run with no verb to read its usage, it started a
/// headless loop on the install's home, and that loop took a quest two seconds before the desktop's
/// own. The loop is asked for by its verb, and everything else is the usage.
/// </summary>
public sealed class DriverCommandTests
{
    /// <summary>🔴 No verb is no loop: the usage, and nothing started.</summary>
    [Fact]
    public void A_bare_invocation_asks_for_no_loop()
    {
        Assert.Null(DriverCommand.Read([], out var problem));
        Assert.Null(problem);
    }

    /// <summary>The loop by its verb, in each of its modes, and the spelling scripts already use.</summary>
    [Theory]
    [InlineData(new[] { "drive" }, LoopMode.Watch, false)]
    [InlineData(new[] { "drive", "--once" }, LoopMode.Once, false)]
    [InlineData(new[] { "drive", "--until-idle" }, LoopMode.UntilIdle, false)]
    [InlineData(new[] { "drive", "--share" }, LoopMode.Watch, true)]
    [InlineData(new[] { "drive", "--until-idle", "--share" }, LoopMode.UntilIdle, true)]
    [InlineData(new[] { "--once" }, LoopMode.Once, false)]
    [InlineData(new[] { "--until-idle" }, LoopMode.UntilIdle, false)]
    [InlineData(new[] { "--until-idle", "--share" }, LoopMode.UntilIdle, true)]
    public void The_loop_is_asked_for_by_name(string[] args, LoopMode mode, bool share)
    {
        var asked = DriverCommand.Read(args, out var problem);

        Assert.Null(problem);
        Assert.Equal(new LoopRequest(mode, share), asked);
    }

    /// <summary>
    /// Anything else starts nothing, and says what was not understood — a word nobody answers used to fall
    /// through to the watch loop, which is how reading the usage became driving.
    /// </summary>
    [Theory]
    [InlineData(new[] { "watch" }, "`watch`")]
    [InlineData(new[] { "--share" }, "`--share`")]
    [InlineData(new[] { "drive", "--forever" }, "`--forever`")]
    [InlineData(new[] { "drive", "--once", "--until-idle" }, "one")]
    public void Anything_else_asks_for_no_loop_and_says_what_was_not_understood(string[] args, string named)
    {
        Assert.Null(DriverCommand.Read(args, out var problem));
        Assert.NotNull(problem);
        Assert.Contains(named, problem);
    }

    /// <summary>Asking for the usage is an answer, not a mistake.</summary>
    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Asking_for_help_is_no_loop_and_no_problem(string word)
    {
        Assert.Null(DriverCommand.Read([word], out var problem));
        Assert.Null(problem);
        Assert.True(DriverCommand.AskedForHelp([word]));
        Assert.False(DriverCommand.AskedForHelp([]));
    }

    /// <summary>
    /// The usage a bare invocation prints names every verb the host answers — read from the host's own
    /// source, so a verb added there without a line here is a failing test rather than a door nobody
    /// can find.
    /// </summary>
    [Fact]
    public void The_usage_names_every_verb_the_host_answers()
    {
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var verbs = Regex.Matches(program, @"args is \[""(?<verb>[a-z]+)""")
            .Select(match => match.Groups["verb"].Value)
            .Distinct()
            .ToList();

        Assert.True(verbs.Count >= 8, $"expected the host's verbs, found {string.Join(", ", verbs)}");
        foreach (var verb in verbs.Append("drive"))
        {
            Assert.Contains($"\n  {verb} ", DriverCommand.Usage.ReplaceLineEndings("\n"));
        }
    }

    /// <summary>
    /// WSR6: bringing a repository up to date after its pull request merged is a `trees` verb, and the host's
    /// terminal door says it in the usage — the list first, `--yes` the press.
    /// </summary>
    [Fact]
    public void The_usage_names_bringing_a_repository_up_to_date()
    {
        Assert.Contains("| sync [--repository <name>] [--yes]", DriverCommand.Usage);
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));
        Assert.Contains("case [\"sync\", ..]", program);
    }

    private static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver.Host")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("the desktop source tree was not found above the test binary");
    }
}
