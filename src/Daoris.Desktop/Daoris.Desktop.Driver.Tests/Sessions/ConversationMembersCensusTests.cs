using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of a conversation's membership: sending, reading, the brief, the course's counts and a go-on's admission read a
/// conversation's parts one way, the records whose <c>line</c> names it (<c>LineKeys</c>), and nothing walks back by <c>from</c> to find
/// them. A walk back by <c>from</c> in the driver, its host or the modules is a failing line until it is named here with what it reads
/// that is not a conversation's parts.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class ConversationMembersCensusTests
{
    /// <summary>Walks back by <c>from</c> that read no conversation's parts: what each reads.</summary>
    private static readonly Dictionary<string, string> NotMembership = new()
    {
        ["PartnerAdmission.cs"] = "a partner's lead chain, which a hand-over does not reset, for the partner's own admission",
        ["WorkflowRun.Read.cs"] = "the quest a record's work is of, the first part that names one",
    };

    [GeneratedRegex(@"for\s*\(\s*var\s+at\s*=[^;]*;[^;]*;[^)]*\bFrom\b|TryGetValue\(\s*at\s*,[^)]*\)[^;{]*\bFrom\b|\bat\s*=\s*\w+\.From\b|\bfromOf\.TryGetValue\(\s*at\b")]
    private static partial Regex WalkBack();

    [Fact]
    public void Nothing_walks_back_by_from_to_find_a_conversation_s_parts()
    {
        var walks = Sources().Where(source => WalkBack().IsMatch(source.Source)).Select(source => source.File).Distinct().Order().ToList();

        Assert.Equal(NotMembership.Keys.Order(), walks);
    }

    [Fact]
    public void The_one_read_is_used_by_the_brief_the_course_and_the_admission()
    {
        var readers = Sources().Where(source => source.Source.Contains("LineKeys.", StringComparison.Ordinal)).Select(source => source.File).ToHashSet();

        Assert.Superset(new HashSet<string> { "ContextBriefs.cs", "LineCounts.cs", "ContinuationAdmissions.cs", "BriefedMove.cs", "Exchanges.cs" }, readers);
    }

    private static IEnumerable<(string File, string Source)> Sources()
    {
        foreach (var project in new[] { SourceFiles.DriverProject, "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules" })
        {
            foreach (var path in SourceFiles.All(project, "*.cs")) yield return (Path.GetFileName(path), File.ReadAllText(path));
        }
    }
}
