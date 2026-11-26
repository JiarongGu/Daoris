using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Who may settle a step is the door's alone, and the door reads the tenure, the speaker and the mark, never the step's kind; every door
/// that settles a run step's attempt asks it.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class SettleDoorCensusTests
{
    [Fact]
    public void No_settle_path_asks_the_steps_kind_who_may_settle_it()
    {
        var door = Code(SourceTexts.Of(SourceFiles.Driver("SettleDoor.cs")));
        Assert.DoesNotMatch(new Regex(@"\bKind\b|NodeKinds\.|NodeKit\.|\w+Node\b|NodeAuthorit"), door);

        foreach (var (settles, asks) in new[] { ("RunNodeWords.cs", "SettleDoor.Admit("), ("WorkPersonWord.cs", "SettleDoor.Admit("), ("StepHolding.cs", "SettleDoor.HandRefused(") })
        {
            var source = Code(SourceTexts.Of(SourceFiles.Driver(settles)));
            Assert.Contains(asks, source, StringComparison.Ordinal);
            // The epoch is judged by the door alone: no settle path reads an ended tenure for itself.
            Assert.DoesNotContain(".Ended(", source, StringComparison.Ordinal);
        }

        // Every route that settles or passes on a step names its speaker by the caller's channel, never the person by default, and its epoch.
        foreach (var route in new[] { "WorkflowRoutes.cs", "LinesRoutes.cs", "CastRoutes.cs" })
        {
            var source = Code(SourceTexts.Of(SourceFiles.Find(SourceFiles.Project("Daoris.Desktop.Modules"), route)));
            Assert.Contains("SpeakerOf(press.Epoch)", source, StringComparison.Ordinal);
            Assert.Contains("EpochOf(press.Epoch)", source, StringComparison.Ordinal);
        }

        // The whole review's stop speaks as its channel on every step it stops, and a working session's channel names its agent.
        Assert.Contains("settle: settle", Code(SourceTexts.Of(SourceFiles.Driver("ReviewStops.cs"))), StringComparison.Ordinal);
        Assert.Contains(
            "settle: CastRoutes.SpeakerOf(null)",
            Code(SourceTexts.Of(SourceFiles.Find(SourceFiles.Project("Daoris.Desktop.Modules"), "WorkflowRoutes.cs"))),
            StringComparison.Ordinal);
        Assert.Contains(
            "PipeDoor.WorkerAgent",
            Code(SourceTexts.Of(SourceFiles.Find(SourceFiles.Project("Daoris.Desktop.Modules"), "CastRoutes.cs"))),
            StringComparison.Ordinal);

        // Each standing attempt is cast its holder before the pass's rounds, not only after them.
        var runtime = Code(SourceTexts.Of(SourceFiles.Driver("NodeRuntime.cs")));
        Assert.True(runtime.IndexOf("AttemptTenures.Keep(", StringComparison.Ordinal) < runtime.IndexOf("for (var round = 0;", StringComparison.Ordinal));

        static string Code(string text) => Regex.Replace(text, @"^\s*///.*$|//.*$", "", RegexOptions.Multiline);
    }
}
