using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The repository's own layer, its tracked <c>daoris.files.json</c>, read and judged: each entry in the rule file's one format, and a
/// dependency refused where a later rule of the layer takes one of its paths, its paths being one install's unit. The rest still read.
/// </summary>
/// <remarks>Read from git's objects at the commit the checkout's line stands at (<see cref="AtAsync"/>), never a working file or a
/// branch's: what Daoris does to the next line is never one branch's say.</remarks>
internal static class RepositoryFilesReader
{
    internal const string FileName = "daoris.files.json";

    /// <summary>The rules <paramref name="text"/> states; null text is no file.</summary>
    internal static RepositoryFilesRead Read(string? text)
    {
        if (text is null) return RepositoryFilesRead.None;
        try
        {
            using var document = JsonDocument.Parse(text.TrimStart('﻿'));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return RepositoryFilesRead.None with { Problems = [$"{FileName} is not a JSON object"] };

            if (FileRuleFile.FormOf(root) is not { } form) return RepositoryFilesRead.None with { Problems = [FileRuleFile.Unknown(FileName)] };
            var problems = new List<string>();
            var numbered = FileRuleFile.Numbered(root, "", problems, form);
            return new RepositoryFilesRead(Judged(numbered, problems), problems) { FirstForm = form != FileRuleFile.Form };
        }
        catch (JsonException)
        {
            return RepositoryFilesRead.None with { Problems = [$"{FileName} is not valid JSON"] };
        }
    }

    /// <summary>The layer at <paramref name="commit"/>, read from git's objects in <paramref name="root"/>.</summary>
    internal static async Task<RepositoryFilesRead> AtAsync(string root, string commit, CancellationToken ct) =>
        Read(await FileGit.ShowAsync(root, commit, FileName, ct).ConfigureAwait(false));

    /// <summary>
    /// The entries kept: a dependency's refused where a later rule's entry meets it, its paths being one install's unit. A pattern
    /// that reaches a tracked path is git's to cover; the floor keeps the tracked path at the match.
    /// </summary>
    private static List<FileRule> Judged(List<(int Number, FileRule Rule)> rules, List<string> problems)
    {
        var kept = new List<FileRule>();
        for (var i = 0; i < rules.Count; i++)
        {
            var (number, rule) = rules[i];
            if (rule.Kind == FileKind.Dependency && rules.Skip(i + 1).Any(later => later.Number != number && GitPattern.Meet(rule, later.Rule)))
            {
                problems.Add($"rules[{number}]: '{rule.Paths[0]}' is the dependency's path a later rule takes too, so it acts on nothing");
                continue;
            }

            kept.Add(rule);
        }

        return kept;
    }
}

/// <summary>A repository's own rules, or none, and why a rule or the file does not read.</summary>
internal sealed record RepositoryFilesRead(IReadOnlyList<FileRule> Rules, IReadOnlyList<string> Problems)
{
    internal static readonly RepositoryFilesRead None = new([], []);

    /// <summary>Whether the file says no form and is read as the first form, which the Files tab says.</summary>
    internal bool FirstForm { get; init; }
}
