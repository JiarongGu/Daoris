using System.Reflection;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1e part 1 (the foundation design §3.1, §10 row 1e): the vocabulary the course decides over, read by nothing. Its closed
/// enums keep their members and their docs, its one <c>from_why</c>/<c>from_by</c> enum is today's reasons and authors one to one, and no
/// production source names a type it added (C5-3.3), so Daoris works exactly as before until rows h and i adopt it one path per branch.
/// </summary>
public sealed class CourseVocabularyTests
{
    /// <summary>The vocabulary's files, under <c>Course/Domain/</c>: part 1's three.</summary>
    private static readonly string[] Files = ["CourseFacts.cs", "CourseTriggers.cs", "CourseAnswers.cs"];

    /// <summary>The decision's own file, part 2's, beside them: <c>LineCourse.Decide</c> and nothing else that decides.</summary>
    private const string Decision = "LineCourse.Decide.cs";

    /// <summary>Every file the course is written in, read by nothing outside them.</summary>
    private static readonly string[] Course = [.. Files, Decision];

    private static string Driver => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");

    private static string Code(string file) => CourseCensusTests.WithoutComments(File.ReadAllText(Path.Combine(Driver, "Course", "Domain", file)));

    /// <summary>Each closed enum keeps its members, in the order the design states them.</summary>
    [Fact]
    public void The_closed_enums_keep_their_members()
    {
        Assert.Equal(
            [
                "WordsKept", "Answer", "LimitEnd", "LimitWake", "TransientPastBound", "TurnEnd", "HandOver", "BriefedPress", "ConversationLost",
                "CarryOn", "SendBack", "GoOnNew", "StartFrom", "ComeUp",
            ],
            Enum.GetNames<CourseTrigger>());
        Assert.Equal(["CutOff", "StopReleased", "Retried", "Limited", "BroughtForward"], Enum.GetNames<CourseCarryOn>());
        Assert.Equal(["TurnRunning", "BetweenTurns", "ProcessExited", "Reserved", "WindingUp"], Enum.GetNames<CourseBoundary>());
        Assert.Equal(["Person", "Daoris", "Helper", "Findings", "Supervisor"], Enum.GetNames<CourseAuthor>());
        // Part 2 widened four of them where LineCourse.Decide needs a member to say today's answer: Try again, a pick named, an update's
        // restart word, a line that stays and a press refused, and three doors (FOUNDATION1e part 2).
        Assert.Equal(["None", "Pick", "NewSession", "Briefed", "HandOver", "StartFrom", "Stop", "Pause", "TryAgain"], Enum.GetNames<CoursePressKind>());
        Assert.Equal(["Decided", "Delivering", "Held", "Delivered", "FellBack", "Closed"], Enum.GetNames<CourseMovePhase>());
        Assert.Equal(["Now", "TurnEnd"], Enum.GetNames<CourseWhen>());
        Assert.Equal(["Brief", "Carry"], Enum.GetNames<CourseHow>());
        Assert.Equal(["Own", "Named", "Settled", "Pick"], Enum.GetNames<CourseAccountSource>());
        Assert.Equal(["None", "LimitReset", "TransientRetry", "Restart"], Enum.GetNames<CourseDaorisWord>());
        Assert.Equal(["AccountCooling", "LimitReset", "TransientRetry"], Enum.GetNames<CourseWaitWhy>());
        Assert.Equal(
            ["AnswersAlone", "Account", "TransientBound", "CannotGoOn", "ConsentRefused", "SwitchOff", "MoveHeld", "Stays", "Refused"],
            Enum.GetNames<CourseHoldWhy>());
        Assert.Equal(
            ["GoOnNew", "StartFrom", "TryAgain", "Say", "LetAnswerGoOn", "NotNow", "GoOnBrief", "HandOver", "LetAskGo"],
            Enum.GetNames<CourseDoor>());
        Assert.Equal(["Teammate", "Intake", "StoodDown", "Superseded", "StandingMove"], Enum.GetNames<CourseNeverWhy>());
        Assert.Equal(
            ["Press", "Near", "Limit", "Agent", "Curve", "Resume", "CarryOn", "Retry", "NotOffered"], Enum.GetNames<CourseFromWhy>());
        Assert.Equal(["Person", "Daoris", "Agent"], Enum.GetNames<CourseFromBy>());
    }

    /// <summary>A member nobody explained is one nobody can adopt: every enum member of the three files has its doc line.</summary>
    [Fact]
    public void Every_enum_member_is_documented()
    {
        var bare = new List<string>();
        foreach (var file in Files)
        {
            var inside = false;
            var documented = false;
            foreach (var raw in File.ReadAllLines(Path.Combine(Driver, "Course", "Domain", file)))
            {
                var line = raw.Trim();
                if (Regex.IsMatch(line, @"^(public|internal) enum \w+")) inside = true;
                else if (inside && line == "}") inside = false;
                else if (inside && line.StartsWith("///", StringComparison.Ordinal)) documented = true;
                else if (inside && Regex.IsMatch(line, @"^\w+,?$"))
                {
                    if (!documented) bare.Add($"{file}: {line}");
                    documented = false;
                }
            }
        }

        Assert.True(bare.Count == 0, "enum members without a doc: " + string.Join(", ", bare));
    }

    /// <summary>
    /// The closed <c>from_why</c> set is today's nine reasons one to one, and its <c>from_by</c> set today's three authors; the old
    /// constants stay as they are (<c>NoteCodesTests</c> pins the nine). <c>ContinueWhy.Account</c> is a box reason, never a <c>from_why</c>.
    /// </summary>
    [Fact]
    public void The_from_enum_is_todays_reasons_and_authors_one_to_one()
    {
        var reasons = Constants(typeof(HandOverWhy));
        Assert.Equal(9, reasons.Count);
        Assert.Equal(reasons.Count, Enum.GetValues<CourseFromWhy>().Length);
        foreach (var code in reasons)
        {
            var why = CourseFrom.WhyOf(code);
            Assert.True(why is not null, $"{code} has no from_why");
            Assert.Equal(code, CourseFrom.Code(why.Value));
        }

        Assert.Equal(reasons.Count, reasons.Select(code => CourseFrom.WhyOf(code)).Distinct().Count());
        Assert.Equal(reasons.OrderBy(code => code), Enum.GetValues<CourseFromWhy>().Select(CourseFrom.Code).OrderBy(code => code));

        var authors = Constants(typeof(HandOverBy));
        Assert.Equal(3, authors.Count);
        foreach (var code in authors)
        {
            var by = CourseFrom.ByOf(code);
            Assert.True(by is not null, $"{code} has no from_by");
            Assert.Equal(code, CourseFrom.Code(by.Value));
        }

        Assert.Equal(authors.OrderBy(code => code), Enum.GetValues<CourseFromBy>().Select(CourseFrom.Code).OrderBy(code => code));

        Assert.Null(CourseFrom.WhyOf(ContinueWhy.Account));
        Assert.DoesNotContain("Account", Enum.GetNames<CourseFromWhy>());
        Assert.Null(CourseFrom.WhyOf(null));
        Assert.Null(CourseFrom.WhyOf("someday"));
        Assert.Null(CourseFrom.ByOf(null));
        Assert.Null(CourseFrom.ByOf("someone"));
    }

    /// <summary>The facts carry the answers-alone count as a value and the bound the hold reads; the answers are the six of §3.1's table.</summary>
    [Fact]
    public void The_facts_take_the_hold_as_values_and_the_answers_are_the_tables_six()
    {
        Assert.Equal(AnswersAlone.Bound, new CourseAlone(Count: 0).Bound);
        Assert.False(new CourseAlone(Count: AnswersAlone.Bound - 1).AtBound);
        Assert.True(new CourseAlone(Count: AnswersAlone.Bound).AtBound);
        Assert.False(new CourseAlone(Count: AnswersAlone.Bound, Released: true).AtBound);

        Assert.Equal(
            ["Hold", "Move", "Never", "Resume", "Start", "Wait"],
            typeof(LineCourse).GetNestedTypes().Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(typeof(LineCourse).GetNestedTypes(), type => Assert.True(type.IsSubclassOf(typeof(LineCourse)), type.Name));
    }

    /// <summary>
    /// The vocabulary declares no method that decides: <c>LineCourse.Decide</c> is part 2's, in its own file beside them, the one decision
    /// the table (<c>LineCourseTests</c>) proves.
    /// </summary>
    [Fact]
    public void Only_the_decision_s_own_file_decides()
    {
        foreach (var file in Files) Assert.DoesNotMatch(@"\bDecide\s*\(", Code(file));
        Assert.Single(Regex.Matches(Code(Decision), @"public static LineCourse Decide\(CourseFacts facts, CourseTrigger trigger\)"));
    }

    /// <summary>
    /// C5-3.3: no production source names a type this part added, so no path reads the course before rows h and i adopt it. A site that
    /// reads the course then also leaves the census's list (<see cref="CourseCensusTests"/>) and this check moves with that row.
    /// </summary>
    [Fact]
    public void No_production_source_names_a_type_the_vocabulary_added()
    {
        var names = Course
            .SelectMany(file => Regex.Matches(
                    Code(file),
                    @"^public\s+(?:abstract\s+|sealed\s+|static\s+|readonly\s+|partial\s+)*(?:record(?:\s+struct)?|enum|class|struct)\s+(\w+)",
                    RegexOptions.Multiline)
                .Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.True(names.Count >= 20, $"found {names.Count} types: the pattern has drifted");
        Assert.Contains("LineCourse", names);

        var mine = Course.Select(file => Path.Combine("Course", "Domain", file)).ToHashSet(StringComparer.Ordinal);
        var readers = new List<string>();
        foreach (var path in CourseCensusTests.ProductionFiles())
        {
            if (mine.Contains(Path.GetRelativePath(Driver, path))) continue;

            var text = CourseCensusTests.WithoutComments(File.ReadAllText(path));
            foreach (var name in names.Where(name => Regex.IsMatch(text, $@"\b{name}\b"))) readers.Add($"{Path.GetFileName(path)} names {name}");
        }

        Assert.True(readers.Count == 0, "read by nothing, yet: " + string.Join("; ", readers));
    }

    private static List<string> Constants(Type type) =>
        [.. type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)];
}
