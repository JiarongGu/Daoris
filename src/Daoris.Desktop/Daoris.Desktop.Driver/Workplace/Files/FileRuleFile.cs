using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The rule file's one format for every layer: its <c>form</c>, and <c>rules</c> read in order, each entry a <c>kind</c>, its
/// <c>paths</c> in git's syntax and its rule set; a file without <c>form</c> is read through <see cref="RuleFileFirstForm"/>. An
/// entry that does not read is refused alone and said, since a layer's other entries mean what they say without it.
/// </summary>
internal static class FileRuleFile
{
    internal const int Form = 2;

    private static readonly string[] Shared = ["kind", "paths", "evidence", "other", "size"];

    internal static int? FormOf(JsonElement root) =>
        root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("form", out var form) ? 1
        : form.ValueKind == JsonValueKind.Number && form.TryGetInt32(out var number) && number == Form ? Form
        : null;

    internal static string Unknown(string file) => $"{file} is written in a form this Daoris does not read (it reads form {Form}, and a file that says none)";

    /// <summary>The rules of <paramref name="scope"/>'s <c>rules</c>, each problem named from <paramref name="where"/>.</summary>
    internal static List<FileRule> Rules(JsonElement scope, string where, List<string> problems, int form) =>
        [.. Numbered(scope, where, problems, form).Select(each => each.Rule)];

    /// <summary>As <see cref="Rules"/>, each entry with its rule's place in the list, which a first-form rule's entries share.</summary>
    internal static List<(int Number, FileRule Rule)> Numbered(JsonElement scope, string where, List<string> problems, int form)
    {
        if (scope.ValueKind != JsonValueKind.Object || !scope.TryGetProperty("rules", out var listed)) return [];
        if (listed.ValueKind != JsonValueKind.Array)
        {
            problems.Add($"{where}rules: is not a list");
            return [];
        }

        var rules = new List<(int, FileRule)>();
        var number = 0;
        foreach (var each in listed.EnumerateArray())
        {
            number++;
            try
            {
                if (form == Form) rules.Add((number, Rule(each)));
                else rules.AddRange(RuleFileFirstForm.Entries(FirstFormRule(each)).Select(entry => (number, entry)));
            }
            catch (FormatException refused)
            {
                problems.Add($"{where}rules[{number}]: {refused.Message}");
            }
        }

        return rules;
    }

    /// <exception cref="FormatException">The rule does not read, with why.</exception>
    internal static FileRule FirstFormRule(JsonElement each)
    {
        var (kind, paths) = Head(each, RoomPaths.Unfit, FileKinds.Fields);
        var copy = Flag(each, "copy");
        return Fields(each, kind, paths) with { Across = Flag(each, "share"), Reach = copy is { } copied ? copied ? "copy" : "tree" : null };
    }

    private static FileRule Rule(JsonElement each)
    {
        var (kind, paths) = Head(each, GitPattern.Unfit, FileKindSets.Fields);
        var reach = Word(each, "reach", FileKindSets.Reaches);
        if (FileKindSets.Unshared(kind, reach) is { } unshared) throw new FormatException(unshared);
        var builds = Text(each, "builds");
        if (builds is not null && FileKindSets.Unbuildable(builds) is { } unbuildable) throw new FormatException(unbuildable);
        return Fields(each, kind, paths) with
        {
            Across = Flag(each, "across"),
            Reach = reach,
            Take = Word(each, "take", FileKindSets.Takes),
            Finish = Word(each, "finish", FileKindSets.Finishes),
            Builds = builds,
            Refuses = Strings(each, "refuses"),
        };
    }

    private static (FileKind Kind, IReadOnlyList<string> Paths) Head(JsonElement each, Func<string, string?> unfit, Func<FileKind, IReadOnlyList<string>> fields)
    {
        if (each.ValueKind != JsonValueKind.Object) throw new FormatException("is not an object");
        var word = each.TryGetProperty("kind", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
        if (FileKinds.Of(word) is not { } kind)
        {
            throw new FormatException($"`{word}` is not a kind: {string.Join(", ", Enum.GetValues<FileKind>().Select(each => each.Word()))}");
        }

        var paths = Strings(each, "paths") ?? [];
        if (paths.Count == 0) throw new FormatException("names no paths");
        foreach (var path in paths)
        {
            if (unfit(path) is { } why) throw new FormatException($"'{path}' {why}");
        }

        var taken = fields(kind);
        if (each.EnumerateObject().Select(field => field.Name).FirstOrDefault(name => !name.StartsWith('_') && !Shared.Contains(name) && !taken.Contains(name)) is { } stray)
        {
            throw new FormatException($"`{stray}` is not a field of {FileKinds.Article(kind)} `{kind.Word()}` rule");
        }

        return (kind, paths);
    }

    private static FileRule Fields(JsonElement each, FileKind kind, IReadOnlyList<string> paths) => new(kind, paths)
    {
        Install = Text(each, "install"),
        Setup = Strings(each, "setup"),
        Per = Per(each),
        Key = Strings(each, "key"),
        Toolchain = Strings(each, "toolchain"),
        Env = Strings(each, "env"),
        Secret = Strings(each, "secret"),
        Caches = Strings(each, "caches"),
        Bound = Flag(each, "bound"),
        Evidence = Strings(each, "evidence") ?? [],
        Other = FileKinds.Of(Text(each, "other")),
        Size = Size(each),
    };

    internal static JsonObject Written(FileRule rule)
    {
        var written = new JsonObject { ["kind"] = rule.Kind.Word(), ["paths"] = List(rule.Paths) };
        if (rule.Reach is { } reach) written["reach"] = reach;
        if (rule.Take is { } take) written["take"] = take;
        if (rule.Finish is { } finish) written["finish"] = finish;
        if (rule.Install is { } install) written["install"] = install;
        if (rule.Setup is { } setup) written["setup"] = List(setup);
        if (rule.Per is { } per) written["per"] = per;
        if (rule.Key is { } key) written["key"] = List(key);
        if (rule.Toolchain is { } toolchain) written["toolchain"] = List(toolchain);
        if (rule.Env is { } env) written["env"] = List(env);
        if (rule.Secret is { } secret) written["secret"] = List(secret);
        if (rule.Caches is { } caches) written["caches"] = List(caches);
        if (rule.Bound is { } bound) written["bound"] = bound;
        if (rule.Across is { } across) written["across"] = across;
        if (rule.Builds is { } builds) written["builds"] = builds;
        if (rule.Refuses is { } refuses) written["refuses"] = List(refuses);
        if (rule.Evidence.Count > 0) written["evidence"] = List(rule.Evidence);
        if (rule.Other is { } other) written["other"] = other.Word();
        if (rule.Size is { } size) written["size"] = new JsonObject { ["files"] = size.Files, ["bytes"] = size.Bytes, ["more"] = size.More };
        return written;
    }

    internal static JsonArray List(IEnumerable<string> values) => [.. values.Select(value => (JsonNode)value)];

    private static string? Text(JsonElement each, string name) =>
        !each.TryGetProperty(name, out var value) ? null
        : value.ValueKind == JsonValueKind.String ? value.GetString()
        : throw new FormatException($"`{name}` is not a string");

    private static string? Word(JsonElement each, string name, IReadOnlyList<string> words) =>
        Text(each, name) is not { } said ? null
        : words.Contains(said) ? said
        : throw new FormatException($"`{name}` is not one of {string.Join(", ", words.Select(word => $"`{word}`"))}");

    private static string? Per(JsonElement each) =>
        Text(each, "per") is not { } per ? null
        : per is "repository" or "tree" ? per
        : throw new FormatException("`per` is not `repository` or `tree`");

    private static IReadOnlyList<string>? Strings(JsonElement each, string name)
    {
        if (!each.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            throw new FormatException($"`{name}` is not a list of strings");
        }

        return [.. value.EnumerateArray().Select(item => item.GetString()!)];
    }

    private static bool? Flag(JsonElement each, string name) =>
        !each.TryGetProperty(name, out var value) ? null
        : value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
        : throw new FormatException($"`{name}` is not true or false");

    private static FileSize? Size(JsonElement each) =>
        each.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Object
            ? new FileSize(Json.Long(size, "files") ?? 0, Json.Long(size, "bytes") ?? 0, Json.Flag(size, "more"))
            : null;
}
