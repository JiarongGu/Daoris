using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The fields a page type of <c>data/api.ts</c> reads, resolved through the service's declaration it is derived from
/// (<c>Omit&lt;QuestResponse, 'attachments'&gt; &amp; { … }</c>, <c>SyncStandingResponse</c> alone): the response record's parameters in the
/// contracts, less what the type omits, plus the fields it adds. The generated declarations are not committed, so the record is the source.
/// </summary>
internal static partial class PageTypeFields
{
    /// <summary>The fields of <c>export type {name} = …;</c> in <c>data/api.ts</c>.</summary>
    public static IReadOnlyList<string> Of(string name)
    {
        var root = Root();
        var text = Comments().Replace(File.ReadAllText(Path.Combine(root, "src", "Daoris.Web", "src", "data", "api.ts")), "");
        var start = text.IndexOf($"export type {name} =", StringComparison.Ordinal);
        Assert.True(start >= 0, $"data/api.ts declares no `export type {name} =`");
        var rhs = Declaration(text, start + $"export type {name} =".Length);

        var fields = new List<string>();
        var rest = rhs;
        var omit = Omit().Match(rhs);
        if (omit.Success)
        {
            var kept = ServiceFields(root, omit.Groups["record"].Value).Except(Quoted().Matches(omit.Groups["omitted"].Value).Select(each => each.Groups[1].Value));
            fields.AddRange(kept);
            rest = rest.Remove(omit.Index, omit.Length);
        }
        else if (Bare().Match(rhs.Trim()) is { Success: true } bare)
        {
            fields.AddRange(ServiceFields(root, bare.Groups["record"].Value));
            rest = rhs.Trim()[bare.Length..];
        }

        fields.AddRange(Shapes(rest));
        return [.. fields.Distinct()];
    }

    /// <summary>The right side of a type declaration, up to the <c>;</c> that ends it outside any bracket.</summary>
    private static string Declaration(string text, int from)
    {
        var depth = 0;
        for (var at = from; at < text.Length; at++)
        {
            var c = text[at];
            if (c is '{' or '(' or '[' || (c == '<')) depth++;
            else if (c is '}' or ')' or ']' || (c == '>' && text[at - 1] != '=')) depth--;
            else if (c == ';' && depth == 0) return text[from..at];
        }

        throw new InvalidOperationException("a type declaration with no end");
    }

    /// <summary>The top-level fields of each <c>{ … }</c> shape of <paramref name="rhs"/>, a field's own nested shape left out.</summary>
    private static IEnumerable<string> Shapes(string rhs)
    {
        var top = new System.Text.StringBuilder();
        var depth = 0;
        foreach (var c in rhs)
        {
            if (c is '{' or '(' or '[' or '<') depth++;
            else if (c is '}' or ')' or ']' or '>') depth--;
            else if (depth == 1) top.Append(c);
            if (depth == 0 && c == '}') top.Append(';');
        }

        return Field().Matches(top.ToString()).Select(each => each.Groups["name"].Value);
    }

    /// <summary>The parameters of the contracts' <c>record {record}(…);</c>, as the page names them.</summary>
    public static IEnumerable<string> ServiceFields(string record) => ServiceFields(Root(), record);

    private static IEnumerable<string> ServiceFields(string root, string record)
    {
        var file = Directory.EnumerateFiles(Path.Combine(root, "src", "Daoris.Contracts"), "*.cs", SearchOption.AllDirectories)
            .Where(each => !each.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !each.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .FirstOrDefault(each => File.ReadAllText(each).Contains($"record {record}(", StringComparison.Ordinal));
        Assert.True(file is not null, $"the contracts declare no `record {record}(`");
        var block = Regex.Match(Comments().Replace(File.ReadAllText(file), ""), $@"record {record}\((?<body>.*?)\);", RegexOptions.Singleline);
        return block.Groups["body"].Value is var body
            ? ParameterName().Matches(body).Select(each => char.ToLowerInvariant(each.Groups["name"].Value[0]) + each.Groups["name"].Value[1..])
            : [];
    }

    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"Omit<(?<record>\w+),\s*(?<omitted>[^>]*)>")]
    private static partial Regex Omit();

    [GeneratedRegex(@"^(?<record>\w+Response)\b")]
    private static partial Regex Bare();

    [GeneratedRegex(@"'([^']+)'")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"(?:^|[;,\s])(?<name>\w+)\??\s*:")]
    private static partial Regex Field();

    [GeneratedRegex(@"\b(?<name>[A-Z]\w*)\s*(?==|,|$)")]
    private static partial Regex ParameterName();
}
