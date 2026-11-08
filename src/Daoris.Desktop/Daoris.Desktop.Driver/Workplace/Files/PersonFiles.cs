using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One scope of the person's layer: its rules in order and its permissions.</summary>
internal sealed record FileScope(IReadOnlyList<FileRule> Rules, FilePermissions Permissions)
{
    internal static readonly FileScope Empty = new([], FilePermissions.None);
}

/// <summary>
/// The person's layer as read: the <c>machine</c> scope, each repository's, the rules and permissions refused alone, and
/// <see cref="Unread"/>, why a file that stands does not read.
/// </summary>
internal sealed record PersonFilesRead(FileScope Machine, IReadOnlyDictionary<string, FileScope> Repositories, IReadOnlyList<string> Problems)
{
    internal static readonly PersonFilesRead Empty = new(FileScope.Empty, new Dictionary<string, FileScope>(), []);

    /// <summary>Why the file stands and does not read, or null. Its defaults restrict nothing, so read as empty it would drop every restriction the person set.</summary>
    internal string? Unread { get; init; }

    /// <summary>Whether the file says no form and is read as the first form.</summary>
    internal bool FirstForm { get; init; }

    /// <summary>Whether any scope holds a path git's syntax cannot state, read as written until the person words it.</summary>
    internal bool Keeps => Machine.Rules.Concat(Repositories.Values.SelectMany(scope => scope.Rules)).Any(rule => rule.FirstForm);

    internal FileScope For(string repository) =>
        Repositories.FirstOrDefault(pair => string.Equals(pair.Key, repository, StringComparison.OrdinalIgnoreCase)).Value ?? FileScope.Empty;
}

/// <summary>
/// The person's layer, the home's <c>data/files.json</c>: <c>machine</c> rules and permissions for every repository here, then each
/// repository's own, one reader and one writer, a hand edit read. Absent, it is empty; standing and unreadable, it is said and holds
/// every act as an ask (<see cref="FileRules.Judge"/>), and nothing writes over it.
/// </summary>
internal static class PersonFiles
{
    internal const string FileName = "files.json";

    internal const string LockName = "person-files";

    internal static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Files);

    internal static PersonFilesRead Read(string home)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return PersonFilesRead.Empty;
        try
        {
            var read = Parse(AtomicFile.ReadText(path));
            return PersonFilesForm.Due(read) && PersonFilesForm.Rewrite(home) ? Parse(AtomicFile.ReadText(path)) : read;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return PersonFilesRead.Empty with { Unread = $"{FileName} could not be opened ({error.GetType().Name})" };
        }
    }

    internal static PersonFilesRead Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text.TrimStart('﻿'));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return PersonFilesRead.Empty with { Unread = $"{FileName} is not a JSON object" };

            if (FileRuleFile.FormOf(root) is not { } form) return PersonFilesRead.Empty with { Unread = FileRuleFile.Unknown(FileName) };
            var problems = new List<string>();
            var machine = root.TryGetProperty("machine", out var own) ? Scope(own, "machine.", problems, form) : FileScope.Empty;
            var repositories = new Dictionary<string, FileScope>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("repositories", out var listed) && listed.ValueKind == JsonValueKind.Object)
            {
                foreach (var each in listed.EnumerateObject()) repositories[each.Name] = Scope(each.Value, $"repositories.{each.Name}.", problems, form);
            }

            return new PersonFilesRead(machine, repositories, problems) { FirstForm = form != FileRuleFile.Form };
        }
        catch (JsonException error)
        {
            return PersonFilesRead.Empty with { Unread = $"{FileName} is not valid JSON ({error.Message})" };
        }
    }

    private static FileScope Scope(JsonElement scope, string where, List<string> problems, int form) =>
        new(FileRuleFile.Rules(scope, where, problems, form), FilePermissions.Read(scope, where, problems));

    /// <summary>A rule added at the end of the machine's rules (<paramref name="repository"/> null) or the repository's, so it wins there.</summary>
    /// <exception cref="DriverException">The file stands and does not read.</exception>
    internal static void Set(string home, string? repository, FileRule rule) => Edit(home, repository, rules =>
    {
        rules.Add(FileRuleFile.Written(rule));
        return 1;
    });

    /// <summary>Each of the scope's rules naming <paramref name="path"/> loses it, a rule left naming none removed; how many named it.</summary>
    /// <exception cref="DriverException">The file stands and does not read.</exception>
    internal static int Remove(string home, string? repository, string path) => Edit(home, repository, rules =>
    {
        var removed = 0;
        foreach (var rule in rules.OfType<JsonObject>().ToList())
        {
            if (rule["paths"] is not JsonArray paths) continue;
            var named = paths.Where(each => each?.GetValueKind() == JsonValueKind.String && each.GetValue<string>() == path).ToList();
            if (named.Count == 0) continue;
            removed++;
            foreach (var each in named) paths.Remove(each);
            if (paths.Count == 0) rules.Remove(rule);
        }

        return removed;
    });

    /// <summary>Written beside and renamed over, BOM-less and LF, keeping every key a hand or a newer build wrote.</summary>
    private static int Edit(string home, string? repository, Func<JsonArray, int> edit) => EditScope(home, repository, scope =>
    {
        if (scope["rules"] is not JsonArray rules) scope["rules"] = rules = [];
        return edit(rules);
    });

    internal static int EditScope(string home, string? repository, Func<JsonObject, int> edit)
    {
        var path = PathOf(home);
        using var held = RecordLock.Take(RecordLock.PathOf(home, LockName));
        JsonObject root = new() { ["form"] = FileRuleFile.Form };
        if (File.Exists(path))
        {
            var text = AtomicFile.ReadText(path);
            var read = Parse(text);
            if (read.Unread is { } why)
            {
                throw new DriverException($"{FileName} does not read ({why}), so nothing was written over it: fix the file or remove it, then make the change again.");
            }

            root = JsonNode.Parse(text.TrimStart('﻿')) as JsonObject ?? [];
            if (read.FirstForm) root = PersonFilesForm.Translated(root, read);
        }

        var scope = repository is null ? Child(root, "machine") : Child(Child(root, "repositories"), repository);
        var changed = edit(scope);
        if (changed == 0) return 0;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n");
        return changed;
    }

    private static JsonObject Child(JsonObject parent, string name)
    {
        var key = parent.Select(pair => pair.Key).FirstOrDefault(each => string.Equals(each, name, StringComparison.OrdinalIgnoreCase)) ?? name;
        if (parent[key] is JsonObject child) return child;
        parent[key] = child = [];
        return child;
    }
}
