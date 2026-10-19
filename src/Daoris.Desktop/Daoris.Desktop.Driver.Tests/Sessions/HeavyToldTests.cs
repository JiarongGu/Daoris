using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1b1 (D180's HEAVYRUN1 notes): a driven session in a repository that marks heavy gates is told to run the checks that
/// cover what it changes while it works, and those gates once, at its end, through <c>daoris-driver run-heavy</c>, which waits its
/// turn behind another session's run of the same repository. Said only where its PATH finds the verb: an install's launchers beside
/// the home, which every session's PATH starts with. A repository that marks none, and a workspace build, are told nothing.
/// Reads files only, so the fast half's.
/// </summary>
public sealed class HeavyToldTests : IDisposable
{
    private const string Said =
        "While you work, run the checks that cover what you change. Once, when the work is done, run this repository's heavy gates "
        + "(`build`, `e2e`) through `daoris-driver run-heavy`, from this tree's root: it waits its turn behind another session's run "
        + "of this repository rather than running beside it. Never run those gates whole another way.";

    private readonly HeavyFixture _fixture = new("heavy-told");

    public HeavyToldTests() => Directory.CreateDirectory(Home);

    public void Dispose() => _fixture.Dispose();

    /// <summary>An install: its home is <c>data/</c>, its launchers in <c>app/bin/</c> beside it.</summary>
    private string Install => _fixture.At("install");

    private string Home => Path.Combine(Install, DaorisHome.InstallData);

    private void Launcher(string name)
    {
        var bin = Path.Combine([Install, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, name), "");
    }

    private void Marked() =>
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("build", "dotnet build", true), ("unit", "npm test", false), ("e2e", "npm run e2e", true));

    [Fact]
    public void A_repository_that_marks_heavy_gates_is_told_them_in_declared_order_with_the_verb()
    {
        Launcher("daoris-driver.cmd");
        Marked();

        Assert.Equal(Said, HeavyTold.For(_fixture.Checkout, Home));
        Assert.Equal(Said, HeavyTold.Words(["build", "e2e"]));
    }

    [Fact]
    public void A_repository_that_declares_no_gates_or_marks_none_heavy_is_told_nothing()
    {
        Launcher("daoris-driver.cmd");

        Assert.Null(HeavyTold.For(_fixture.Checkout, Home));

        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("unit", "npm test", false));
        Assert.Null(HeavyTold.For(_fixture.Checkout, Home));

        // A file `run-heavy` would refuse tells nothing either: the session runs its gates as it would.
        File.WriteAllText(Path.Combine(_fixture.Checkout, HeavyGates.FileName), "{ not json");
        Assert.Null(HeavyTold.For(_fixture.Checkout, Home));
        File.WriteAllText(Path.Combine(_fixture.Checkout, HeavyGates.FileName), """{"gates":[{"name":"build","heavy":true}]}""");
        Assert.Null(HeavyTold.For(_fixture.Checkout, Home));
    }

    /// <summary>
    /// The verb is reached by its bare name on the session's PATH, which starts with the install's launchers wherever they are beside
    /// the home, wiring step or none: where they are not (a workspace build, no home), nothing promises a verb the session cannot run.
    /// </summary>
    [Fact]
    public void A_start_whose_path_holds_no_driver_launcher_is_told_nothing()
    {
        Marked();

        Assert.Null(HeavyTold.For(_fixture.Checkout, Home));
        Assert.Null(HeavyTold.For(_fixture.Checkout, null));

        Launcher("daoris.cmd");
        Assert.Null(HeavyTold.For(_fixture.Checkout, Home));

        Launcher("daoris-driver");
        Assert.Equal(Said, HeavyTold.For(_fixture.Checkout, Home));
    }

    /// <summary>
    /// Every driven instruction says it before its close, counted with the close as the driver's own words, so no page catalogue
    /// changes; and an instruction told nothing reads as it did.
    /// </summary>
    [Fact]
    public void Every_driven_instruction_says_it_before_its_close_counted_with_the_close()
    {
        var bare = TargetPromptGoldenTests.Bare;
        var resumed = bare with { Answered = new QuestView("q2", "console-ui", "console-api", "Which field?", "Need it.", "Done") };
        var carried = bare with { CutOff = "timed out after 30 minutes and was killed." };

        foreach (var target in new[] { bare, resumed, carried, bare with { Ready = true } })
        {
            var told = target with { Heavy = Said };
            var text = TargetPrompt.Compose(told);
            var account = TargetPrompt.Composed(told).Account;

            Assert.Contains($"\n{Said}\n", text, StringComparison.Ordinal);
            Assert.True(text.IndexOf(Said, StringComparison.Ordinal) < text.IndexOf("under its own doctrine and", StringComparison.Ordinal));
            Assert.Equal(HandedSources.DriverWords, Assert.Single(account.Sections, section => section.Name == HandedSections.Close).Source);
            Assert.Equal(text.Length, account.Sections.Sum(section => section.Chars));
            Assert.Equal(TargetPrompt.Compose(target), TargetPrompt.Compose(target with { Heavy = null }));
        }
    }
}
