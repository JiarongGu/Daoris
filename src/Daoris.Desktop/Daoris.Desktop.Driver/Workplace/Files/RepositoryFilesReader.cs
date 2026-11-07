using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The repository's own layer, its tracked <c>daoris.files.json</c>, read and judged: each rule in the rule file's one format, a rule
/// reaching a path git tracks refused alone, since a tracked path is git's, and a dependency refused where a later rule of the layer
/// takes one of its paths, its paths being one install's unit. The rest still read.
/// </summary>
/// <remarks>Read from git's objects at the commit the checkout's line stands at (<see cref="AtAsync"/>), never a working file or a
/// branch's: what Daoris does to the next line is never one branch's say.</remarks>
internal static class RepositoryFilesReader
{
    internal const string FileName = "daoris.files.json";

    /// <summary>The rules <paramref name="text"/> states against <paramref name="tracked"/>, git's tracked paths; null text is no file.</summary>
    internal static RepositoryFilesRead Read(string? text, IReadOnlyCollection<string> tracked)
    {
        if (text is null) return RepositoryFilesRead.None;
        try
        {
            using var document = JsonDocument.Parse(text.TrimStart('﻿'));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return RepositoryFilesRead.None with { Problems = [$"{FileName} is not a JSON object"] };

            var problems = new List<string>();
            var numbered = FileRuleFile.Numbered(root, "", problems);
            return new RepositoryFilesRead(Judged(numbered, tracked, problems), problems);
        }
        catch (JsonException)
        {
            return RepositoryFilesRead.None with { Problems = [$"{FileName} is not valid JSON"] };
        }
    }

    /// <summary>The layer at <paramref name="commit"/>, read from git's objects in <paramref name="root"/>.</summary>
    internal static async Task<RepositoryFilesRead> AtAsync(string root, string commit, CancellationToken ct)
    {
        var tracked = await FileGit.TrackedAsync(root, commit, ct).ConfigureAwait(false);
        return Read(await FileGit.ShowAsync(root, commit, FileName, ct).ConfigureAwait(false), tracked);
    }

    private static List<FileRule> Judged(List<(int Number, FileRule Rule)> rules, IReadOnlyCollection<string> tracked, List<string> problems)
    {
        var kept = new List<FileRule>();
        for (var i = 0; i < rules.Count; i++)
        {
            var (number, rule) = rules[i];
            var refusal = rule.Paths.Select(path => FileFloor.TrackedIn(path, tracked) is { } held ? $"rules[{number}]: '{path}' holds {held}, which git tracks" : null)
                .FirstOrDefault(said => said is not null);
            if (refusal is null && rule.Kind == FileKind.Dependency
                && rule.Paths.FirstOrDefault(path => rules.Skip(i + 1).Any(later => later.Rule.Paths.Any(other => RoomPaths.Meet(path, other)))) is { } taken)
            {
                refusal = $"rules[{number}]: '{taken}' is the dependency's path a later rule takes too, so it acts on nothing";
            }

            if (refusal is null) kept.Add(rule);
            else problems.Add(refusal);
        }

        return kept;
    }
}

/// <summary>A repository's own rules, or none, and why a rule or the file does not read.</summary>
internal sealed record RepositoryFilesRead(IReadOnlyList<FileRule> Rules, IReadOnlyList<string> Problems)
{
    internal static readonly RepositoryFilesRead None = new([], []);
}
