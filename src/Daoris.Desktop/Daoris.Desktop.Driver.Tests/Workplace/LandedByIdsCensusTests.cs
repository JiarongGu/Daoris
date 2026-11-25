using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of the readings of "landed" by commit ids alone: a landing that replays a tree's commit leaves the same patch
/// under a new id, so a judgement of a session's work that counts ids says "not landed" for ever. What judges whether a session's work landed
/// reads <c>SessionTrees.WorkAsync</c> (by patch, as the tree's work reads it); the id-only counts (<c>UnlandedAsync</c>,
/// <c>UnlandedInTreeAsync</c>, <c>UnlandedHereAsync</c>) are named here, each with why it may count ids, and a new caller is refused until it
/// reads the tree's work or is named.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class LandedByIdsCensusTests
{
    /// <summary>The files that count ids, each with what it does with the count.</summary>
    private static readonly Dictionary<string, string> Counts = new(StringComparer.Ordinal)
    {
        ["Workplace/TreeFollows.cs"] = "the count itself, and the person's branches it counts against",
        ["Workplace/TreeWorkReads.cs"] = "the tree's work: the ancestry count the content and patch proofs then read past",
        ["Workplace/TreeSquashes.cs"] = "the ancestry proof with the record's squash, which a landing's door asks before its own per-commit reading",
        ["Workplace/SessionTrees.cs"] = "hands the squash-aware count to the landing door",
        ["Workplace/LandPerformer.cs"] = "the landing's gate: a count of zero closes a landing, a count above zero goes on to the per-commit reading that carries replays",
        ["Workplace/UnheldTips.cs"] = "whether a branch's tip is held by a branch of the person's, for the clean-up of branches, not a session's work",
        ["Workplace/Git/GitRepository.cs"] = "the port's own git read",
    };

    [Fact]
    public void Only_the_named_files_count_commit_ids_to_judge_a_session_s_work()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var pattern = new Regex(@"\b(UnlandedInTreeAsync|UnlandedHereAsync|UnlandedAsync)\s*\(");
        var found = new List<string>();
        foreach (var path in CensusScope.Judged(typeof(LandedByIdsCensusTests), SourceFiles.Under(root, "*.cs")))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal)) continue;
            // Code, never a comment's mention.
            if (pattern.IsMatch(SourceTexts.Code(SourceTexts.Of(path)))) found.Add(relative);
        }

        Assert.True(CensusScope.Paths is not null || found.Count >= 3, $"the census found {found.Count} files that count ids: its pattern has drifted");
        var unnamed = found.Where(file => !Counts.ContainsKey(file)).ToList();
        Assert.True(unnamed.Count == 0, "count commit ids to judge landed, and read neither the tree's work by patch nor are named here: " + string.Join(", ", unnamed));
        var listed = CensusScope.Judged(typeof(LandedByIdsCensusTests), Counts.Keys, name => $"src/Daoris.Desktop/Daoris.Desktop.Driver/{name}").ToList();
        Assert.All(listed, name => Assert.True(File.Exists(Path.Combine(root, name)), $"{name} is named but is not there"));
        Assert.All(listed, name => Assert.Contains(name, found));
    }
}
