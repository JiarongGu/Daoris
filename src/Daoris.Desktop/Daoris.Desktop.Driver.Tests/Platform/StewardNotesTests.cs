using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The steward's own records (STEWARDROLE1b, D209 §5.1; the design §10 row 1b's notes' sweep): every move note, resumed brief and
/// planner reason a steward's act writes is its own code, a behavioural sibling of the person's, and none of them, stored English or
/// worded en and zh, reads <i>person</i> or <i>you</i>; and every reader of the person's codes, found by search, reads the steward's too.
/// </summary>
public sealed class StewardNotesTests
{
    /// <summary>Each person's code beside its steward's sibling.</summary>
    private static readonly (NoteCode Person, NoteCode Steward)[] Siblings =
    [
        (NoteCodes.EndedStopped, NoteCodes.EndedStoppedSteward),
        (NoteCodes.StoppedCheckpointStopped, NoteCodes.StoppedCheckpointStoppedSteward),
        (NoteCodes.StoppedCheckpointFinished, NoteCodes.StoppedCheckpointFinishedSteward),
        (NoteCodes.StartedReleased, NoteCodes.StartedReleasedSteward),
    ];

    private static readonly Regex Person = new(@"\b(person|person's|you|your|yours)\b", RegexOptions.IgnoreCase);

    /// <summary>What the steward's acts write: the records' stored English, and the planner's reason for its stop.</summary>
    public static TheoryData<string> Written() =>
    [
        Observation.StoppedBySteward.Note,
        SessionMoves.ByTheStewardNoted("stopped").Note,
        SessionMoves.ByTheStewardNoted("completed").Note,
        Assert.Single(Planner.Plan(
            new Snapshot([new QuestView("q1", "Asker", "Game", "Ask q1", "Why.", "Taken")], [new RepoView("Game", true, "D:/fam/Game")], [])
            {
                LastRun = new Dictionary<string, PriorSession>
                {
                    ["q1"] = new("s1", "D:/trees/s1", "stopped", Observation.StoppedBySteward.Note) { NoteParts = Observation.StoppedBySteward.Parts },
                },
            },
            DriverConfig.Empty with { Drivable = ["Game"] })).Reason,
    ];

    [Theory]
    [MemberData(nameof(Written))]
    public void Nothing_a_steward_s_act_writes_says_the_person_did_it(string written)
    {
        Assert.Contains("steward", written, StringComparison.Ordinal);
        Assert.DoesNotMatch(Person, written);
    }

    /// <summary>Each steward's code is worded in en and zh, naming the steward and never the person or 你 as who acted.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Each_steward_s_code_is_worded_as_the_steward_s_in_both_languages(string language)
    {
        var catalogue = NoteCodesTests.Catalogue(language);
        foreach (var (_, steward) in Siblings)
        {
            Assert.True(catalogue.TryGetProperty("note." + steward.Code, out var words), $"{language}: note.{steward.Code} is not worded");
            var text = words.GetString()!;
            Assert.DoesNotMatch(Person, text);
            Assert.DoesNotContain("你", text, StringComparison.Ordinal);
            Assert.Contains(language == "zh" ? "管家" : "steward", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Every reader of the person's codes has the steward's case: a source file of the driver or the modules that names a person's code
    /// names its steward's sibling too, so a reader added later for the one fails here until it reads the other.
    /// </summary>
    [Fact]
    public void Every_reader_of_the_person_s_codes_reads_the_steward_s()
    {
        var missing = new List<string>();
        foreach (var project in new[] { "Daoris.Desktop.Driver", "Daoris.Desktop.Modules" })
        {
            var folder = Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), project);
            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || Path.GetFileName(file) == "NoteCodes.cs")
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                foreach (var (person, steward) in Siblings)
                {
                    var personName = Name(person);
                    if (Regex.IsMatch(source, $@"NoteCodes\.{personName}\b") && !Regex.IsMatch(source, $@"NoteCodes\.{Name(steward)}\b"))
                    {
                        missing.Add($"{Path.GetFileName(file)} reads {personName} and not {Name(steward)}");
                    }
                }
            }
        }

        Assert.Empty(missing);
    }

    private static string Name(NoteCode code) =>
        typeof(NoteCodes).GetFields().Single(field => field.FieldType == typeof(NoteCode) && ReferenceEquals(field.GetValue(null), code)).Name;
}
