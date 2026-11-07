using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The gates file's <c>trees</c> read and judged: its paths written as the gates' <c>paths</c> are
/// (<see cref="RoomPaths"/>), none that git tracks, since a tracked path is git's; none of the local inputs, the local class being
/// <c>localInputs</c>; none in two classes; each dependency keyed by tracked files; and each <c>prepare</c> command one the same file's
/// <c>safe</c> list holds. A declaration with any problem carries nothing, as half of one is never acted on.
/// </summary>
/// <remarks>Read from the checkout's file, never a tree's, as <see cref="TreeLocalInputs"/> reads its key: what crosses from one line to the
/// next is the checkout's say, never a branch's.</remarks>
internal static class TreeClassesReader
{
    private static readonly string[] Classes = ["dependencies", "intermediates", "outputs", "prepare"];

    /// <summary>The classes <paramref name="text"/> declares against <paramref name="tracked"/>, git's tracked paths; null text is no gates file.</summary>
    internal static TreeClassesRead Read(string? text, IReadOnlyCollection<string> tracked)
    {
        if (text is null) return TreeClassesRead.None;
        try
        {
            using var document = JsonDocument.Parse(text.TrimStart('﻿'));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return TreeClassesRead.None with { Problems = [$"{GeneratedWriters.DeclarationFile} is not a JSON object"] };
            if (!root.TryGetProperty("trees", out var trees)) return TreeClassesRead.None;
            if (trees.ValueKind != JsonValueKind.Object) return TreeClassesRead.None with { Problems = ["trees: is not an object"] };

            var problems = new List<string>();
            // A key opening with `_` is the file's own note, as `_why` and `_reviewedObjects` are elsewhere in it.
            foreach (var key in trees.EnumerateObject().Select(each => each.Name).Where(name => !name.StartsWith('_') && !Classes.Contains(name, StringComparer.Ordinal)))
            {
                problems.Add(key == "local" ? "trees.local: the local class is localInputs, declared once" : $"trees.{key}: is not dependencies, intermediates, outputs or prepare");
            }

            var classes = new TreeClasses(
                Dependencies(trees, tracked, problems), Strings(trees, "intermediates", problems), Strings(trees, "outputs", problems),
                Strings(trees, "prepare", problems, "commands"));
            Judge(classes, root, tracked, problems);
            return problems.Count > 0 ? TreeClassesRead.None with { Problems = problems } : new(classes, []);
        }
        catch (JsonException)
        {
            return TreeClassesRead.None with { Problems = [$"{GeneratedWriters.DeclarationFile} is not valid JSON"] };
        }
    }

    private static void Judge(TreeClasses classes, JsonElement root, IReadOnlyCollection<string> tracked, List<string> problems)
    {
        var local = LocalInputs(root);
        var seen = new List<string>();
        foreach (var (kind, path) in classes.Paths)
        {
            if (RoomPaths.Unfit(path) is { } why)
            {
                problems.Add($"trees.{kind}: '{path}' {why}");
                continue;
            }

            var test = RoomPaths.Declared(path);
            if (tracked.FirstOrDefault(test.IsMatch) is { } held) problems.Add($"trees.{kind}: '{path}' holds {held}, which git tracks");
            if (local.FirstOrDefault(test.IsMatch) is { } input) problems.Add($"trees.{kind}: '{path}' holds the local input {input}: the local class is localInputs, declared once");
            if (seen.Exists(other => RoomPaths.Meet(other, path))) problems.Add($"trees.{kind}: '{path}' is declared in two classes");
            seen.Add(path);
        }

        var safe = Safe(root);
        foreach (var command in classes.Prepare.Where(command => !safe.Contains(command))) problems.Add($"trees.prepare: '{command}' is not in the safe list");
    }

    private static List<TreeDependency> Dependencies(JsonElement trees, IReadOnlyCollection<string> tracked, List<string> problems)
    {
        var found = new List<TreeDependency>();
        if (!trees.TryGetProperty("dependencies", out var listed)) return found;
        if (listed.ValueKind != JsonValueKind.Array)
        {
            problems.Add("trees.dependencies: is not a list");
            return found;
        }

        var number = 0;
        foreach (var each in listed.EnumerateArray())
        {
            number++;
            if (each.ValueKind != JsonValueKind.Object || Json.Text(each, "path") is not { Length: > 0 } path || Json.Strings(each, "key") is not { Count: > 0 } key)
            {
                problems.Add($"trees.dependencies: entry {number} is not an object with a path and a key");
                continue;
            }

            foreach (var file in key.Where(file => !tracked.Contains(file))) problems.Add($"trees.dependencies: the key '{file}' is not a tracked file");
            found.Add(new TreeDependency(path, key, Json.Text(each, "toolchain")));
        }

        return found;
    }

    private static List<string> Strings(JsonElement trees, string key, List<string> problems, string what = "paths")
    {
        if (!trees.TryGetProperty(key, out var listed)) return [];
        if (listed.ValueKind != JsonValueKind.Array || listed.EnumerateArray().Any(each => each.ValueKind != JsonValueKind.String))
        {
            problems.Add($"trees.{key}: is not a list of {what}");
            return [];
        }

        return [.. listed.EnumerateArray().Select(each => each.GetString()!)];
    }

    /// <summary>The local inputs <see cref="TreeLocalInputs"/> reads from the same file: <c>localInputs</c> and the sensitive patterns file.</summary>
    private static List<string> LocalInputs(JsonElement root)
    {
        var inputs = root.TryGetProperty("localInputs", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!).ToList()
            : [];
        if (root.TryGetProperty("sensitive", out var sensitive) && sensitive.ValueKind == JsonValueKind.Object && Json.Text(sensitive, "patternsFile") is { Length: > 0 } named)
        {
            inputs.Add(named);
        }

        return inputs;
    }

    /// <summary>The <c>safe</c> list's exact commands, the declaration of what a session may run without asking.</summary>
    private static HashSet<string> Safe(JsonElement root) =>
        root.TryGetProperty("safe", out var safe) && safe.ValueKind == JsonValueKind.Array
            ? [.. safe.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
            : [];
}

/// <summary>A gates file's classes, or <see cref="TreeClasses.None"/> and why it cannot be read.</summary>
internal sealed record TreeClassesRead(TreeClasses Classes, IReadOnlyList<string> Problems)
{
    internal static readonly TreeClassesRead None = new(TreeClasses.None, []);
}
