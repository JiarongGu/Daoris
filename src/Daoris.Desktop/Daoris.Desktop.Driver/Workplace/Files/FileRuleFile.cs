using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The rule file's one format, read and written for every layer: an object whose <c>rules</c> is a list read in order, each rule a
/// <c>kind</c>, its <c>paths</c> and its kind's fields, a key opening with <c>_</c> the file's own note. A rule that does not read is
/// refused alone and said, and the rest still read, since a layer's other rules mean what they say without it.
/// </summary>
internal static class FileRuleFile
{
    private static readonly string[] Shared = ["kind", "paths", "evidence", "other", "size"];

    /// <summary>The rules of <paramref name="scope"/>'s <c>rules</c>, each problem named from <paramref name="where"/>.</summary>
    internal static List<FileRule> Rules(JsonElement scope, string where, List<string> problems) =>
        [.. Numbered(scope, where, problems).Select(each => each.Rule)];

    /// <summary>As <see cref="Rules"/>, each rule with its place in the list, so a later judgement names the rule a person reads there.</summary>
    internal static List<(int Number, FileRule Rule)> Numbered(JsonElement scope, string where, List<string> problems)
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
                rules.Add((number, Rule(each)));
            }
            catch (FormatException refused)
            {
                problems.Add($"{where}rules[{number}]: {refused.Message}");
            }
        }

        return rules;
    }

    /// <exception cref="FormatException">The rule does not read, with why.</exception>
    private static FileRule Rule(JsonElement each)
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
            if (RoomPaths.Unfit(path) is { } why) throw new FormatException($"'{path}' {why}");
        }

        var fields = FileKinds.Fields(kind);
        if (each.EnumerateObject().Select(field => field.Name).FirstOrDefault(name => !name.StartsWith('_') && !Shared.Contains(name) && !fields.Contains(name)) is { } stray)
        {
            throw new FormatException($"`{stray}` is not a field of {FileKinds.Article(kind)} `{kind.Word()}` rule");
        }

        return new FileRule(kind, paths)
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
            Share = Flag(each, "share"),
            Copy = Flag(each, "copy"),
            Evidence = Strings(each, "evidence") ?? [],
            Other = FileKinds.Of(Text(each, "other")),
            Size = Size(each),
        };
    }

    /// <summary>A rule as the file writes it: its kind and paths first, then each field it states.</summary>
    internal static JsonObject Written(FileRule rule)
    {
        var written = new JsonObject { ["kind"] = rule.Kind.Word(), ["paths"] = List(rule.Paths) };
        if (rule.Install is { } install) written["install"] = install;
        if (rule.Setup is { } setup) written["setup"] = List(setup);
        if (rule.Per is { } per) written["per"] = per;
        if (rule.Key is { } key) written["key"] = List(key);
        if (rule.Toolchain is { } toolchain) written["toolchain"] = List(toolchain);
        if (rule.Env is { } env) written["env"] = List(env);
        if (rule.Secret is { } secret) written["secret"] = List(secret);
        if (rule.Caches is { } caches) written["caches"] = List(caches);
        if (rule.Bound is { } bound) written["bound"] = bound;
        if (rule.Share is { } share) written["share"] = share;
        if (rule.Copy is { } copy) written["copy"] = copy;
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
