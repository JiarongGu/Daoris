using System.Collections.Immutable;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The repository's own layer, its tracked <c>daoris.files.json</c>: a dependency a later rule's entry meets is refused, the rest read.</summary>
/// <remarks>Read at the commit the checkout's line stands at (<see cref="AtAsync"/>): the next line is never one branch's say.</remarks>
internal static class RepositoryFilesReader
{
    /// <summary>The rules <paramref name="text"/> states; null text is no file.</summary>
    internal static RepositoryFilesRead Read(string? text)
    {
        if (text is null) return RepositoryFilesRead.None;
        try
        {
            using var document = JsonDocument.Parse(text.TrimStart('﻿'));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return RepositoryFilesRead.None with { Problems = [$"{DeclarationNames.Rules} is not a JSON object"] };

            if (FileRuleFile.FormOf(root) is not { } form) return RepositoryFilesRead.None with { Problems = [FileRuleFile.Unknown(DeclarationNames.Rules)] };
            var problems = new List<string>();
            var numbered = FileRuleFile.Numbered(root, "", problems, form);
            return new RepositoryFilesRead([.. Judged(numbered, problems)], [.. problems]) { FirstForm = form != FileRuleFile.Form };
        }
        catch (JsonException)
        {
            return RepositoryFilesRead.None with { Problems = [$"{DeclarationNames.Rules} is not valid JSON"] };
        }
    }

    /// <summary>The layer at <paramref name="commit"/>, read from git's objects in <paramref name="root"/>.</summary>
    internal static async Task<RepositoryFilesRead> AtAsync(string root, string commit, CancellationToken ct) =>
        Read(await FileGit.ShowAsync(root, commit, DeclarationNames.Rules, ct).ConfigureAwait(false));

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
internal sealed record RepositoryFilesRead(ImmutableArray<FileRule> Rules, ImmutableArray<string> Problems)
{
    internal static readonly RepositoryFilesRead None = new([], []);

    internal bool FirstForm { get; init; }
}
