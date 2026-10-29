using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Service.Tests;

/// <summary>
/// AGENTPROVIDER1 row a, part 3 (the agent-provider design §9, D213): the service's twin of the driver's <c>ProviderCensusTests</c>. The
/// service names no driver type, so what can cross is a literal and a key: every site in the service's four production projects that
/// names a provider's id, variable, file or wire key is counted over the source with its comments blanked, and each is a named exception
/// in the <c>service</c> section of the driver's <c>fixtures/provider-census.json</c>, with its exact count, the design's row that moves
/// it and its class. The ids and the vocabulary are read from <c>fixtures/provider-vocabulary.json</c>, the one table the four censuses
/// share; nothing here copies a word of it. The service's ordered call readers (<c>Core/Sessions/CallLineage.cs</c>, §3.3) and its open's
/// default (D23's supported default, §6) are named exceptions with the row or the decision that keeps them.
/// </summary>
public sealed class ProviderCensusTests
{
    /// <summary>The design's rows (§11) that can remove a site; <c>-</c> is a site no row removes.</summary>
    private static readonly string[] Rows =
        ["-", "b", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t"];

    private static readonly string[] Classes = ["behaviour", "permanent", "words", "measurement"];

    private sealed record Token(string Name, Regex Pattern);

    private sealed record Row(string File, string Token, int Count, string Rank, string Class, string Why);

    private static string Fixtures() => Path.Combine(
        SourceFiles.WorkspaceFolder(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures");

    private static string Escape(string text) => Regex.Escape(text);

    private static readonly Lazy<(string[] Roots, List<Row> Rows)> Section = new(() =>
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures(), "provider-census.json")));
        var service = document.RootElement.GetProperty("service");
        return (
            service.GetProperty("roots").EnumerateArray().Select(each => each.GetString()!).ToArray(),
            service.GetProperty("exceptions").EnumerateArray().Select(row => new Row(
                row[0].GetString()!, row[1].GetString()!, row[2].GetInt32(), row[3].GetString()!, row[4].GetString()!, row[5].GetString()!)).ToList());
    });

    private static readonly Lazy<List<Token>> Tokens = new(() =>
    {
        using var vocabulary = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures(), "provider-vocabulary.json")));
        var tokens = new List<Token>();
        foreach (var row in vocabulary.RootElement.GetProperty("ids").EnumerateArray())
        {
            var id = row[0].GetString()!;
            tokens.Add(new Token($"\"{id}\"", new Regex(Escape($"\"{id}\""), RegexOptions.CultureInvariant)));
        }

        tokens.Add(new Token("StartsWith(agent)", new Regex(@"\.StartsWith\(\s*""(?:claude|codex|dsh)", RegexOptions.CultureInvariant)));
        // An id as an example or a name in a sentence (help, a refusal): words, but still a site that names a provider.
        tokens.Add(new Token("id in a sentence",
            new Regex(@"(?<![\w@/.\-])(?:claude-code-acp|claude-code|codex-acp|acp-stub|alt-stub)(?![\w""\-])", RegexOptions.CultureInvariant)));
        foreach (var row in vocabulary.RootElement.GetProperty("words").EnumerateArray())
        {
            var word = row[0].GetString()!;
            var plain = word.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && !word.StartsWith('_') && !word.EndsWith('_');
            tokens.Add(new Token(word, plain ? new Regex($@"(?<![A-Za-z0-9_]){Escape(word)}(?![A-Za-z0-9_])") : new Regex(Escape(word))));
        }

        return tokens;
    });

    private static readonly Lazy<Dictionary<string, Dictionary<string, int>>> Baseline = new(() => Census(Sources()));

    /// <summary>Every site of every source: file, then token, then how many times.</summary>
    private static Dictionary<string, Dictionary<string, int>> Census(IEnumerable<(string File, string Source)> sources)
    {
        var found = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var (file, source) in sources)
        {
            var text = StripComments(source);
            foreach (var token in Tokens.Value)
            {
                var count = token.Pattern.Matches(text).Count;
                if (count == 0) continue;
                if (!found.TryGetValue(file, out var counts)) found[file] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
                counts[token.Name] = count;
            }
        }

        return found;
    }

    /// <summary>The problems a census holds against the fixture, in order; none is a passing census.</summary>
    private static List<string> Problems(Dictionary<string, Dictionary<string, int>> found, List<Row> rows, bool exact = true)
    {
        var allowed = rows.ToDictionary(each => (each.File, each.Token), each => each.Count);
        var problems = new List<string>();
        foreach (var (file, counts) in found.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            foreach (var (token, count) in counts.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                if (!allowed.TryGetValue((file, token), out var named)) problems.Add($"{file}: unnamed site {token} x{count}");
                else if (count != named) problems.Add($"{file}: {token} found {count}, the fixture names {named}");
            }
        }

        if (exact)
        {
            // An allowance nothing needs is taken back, so the table stays the sites that are true.
            foreach (var each in rows)
            {
                if (!found.TryGetValue(each.File, out var counts) || !counts.ContainsKey(each.Token))
                {
                    problems.Add($"{each.File}: {each.Token} is no site now: take it off the fixture");
                }
            }
        }

        return problems;
    }

    [Fact]
    public void Every_site_in_the_service_that_knows_a_provider_is_a_named_exception_with_the_row_that_moves_it()
    {
        var problems = Problems(Baseline.Value, Section.Value.Rows);

        Assert.True(problems.Count == 0, $"{problems.Count} problem(s):\n" + string.Join('\n', problems) + "\n\nthe sites found, as fixture rows:\n"
            + string.Join('\n', Baseline.Value.OrderBy(each => each.Key, StringComparer.Ordinal).SelectMany(each => each.Value
                .OrderBy(token => token.Key, StringComparer.Ordinal).Select(token => $"[\"{each.Key}\", \"{token.Key.Replace("\"", "\\\"")}\", {token.Value}]"))));
    }

    /// <summary>The fixture's own shape: every row a known row letter and class, a reason, a file that exists, no duplicate, in order.</summary>
    [Fact]
    public void The_fixtures_service_section_names_each_exception_once_in_order_with_its_row_class_and_reason()
    {
        var rows = Section.Value.Rows;
        var known = Tokens.Value.Select(token => token.Name).ToHashSet(StringComparer.Ordinal);
        var workspace = SourceFiles.WorkspaceFolder();

        Assert.Equal(
            rows.OrderBy(each => each.File, StringComparer.Ordinal).ThenBy(each => each.Token, StringComparer.Ordinal).Select(each => (each.File, each.Token)),
            rows.Select(each => (each.File, each.Token)));
        Assert.Equal(rows.Count, rows.Select(each => (each.File, each.Token)).Distinct().Count());
        foreach (var each in rows)
        {
            Assert.Contains(each.Rank, Rows);
            Assert.Contains(each.Class, Classes);
            Assert.True(each.Count > 0 && each.Why.Length > 0, $"{each.File}: {each.Token} says no reason");
            Assert.Contains(each.Token, known);
            Assert.True(File.Exists(Path.Combine(workspace, each.File)), $"{each.File} is no file");
            // Only a behaviour is removed by a row; a permanent site, an id in words and a measurement stay.
            Assert.Equal(each.Class != "behaviour", each.Rank == "-");
        }
    }

    [Fact]
    public void The_census_reads_its_ids_and_its_vocabulary_from_the_shared_table_a_word_for_each()
    {
        using var vocabulary = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures(), "provider-vocabulary.json")));
        var names = Tokens.Value.Select(token => token.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(vocabulary.RootElement.GetProperty("ids").GetArrayLength() >= 9);
        foreach (var row in vocabulary.RootElement.GetProperty("ids").EnumerateArray()) Assert.Contains($"\"{row[0].GetString()}\"", names);
        foreach (var row in vocabulary.RootElement.GetProperty("words").EnumerateArray()) Assert.Contains(row[0].GetString()!, names);
    }

    // ---- the planted cases (design §11 row a): each must go red ----

    public static TheoryData<string, string, string> Planted => new()
    {
        { "src/Daoris.Service/Daoris.Service.Http/Planted.cs", "if (adapter == \"codex-acp\") return;", "\"codex-acp\"" },
        { "src/Daoris.Service/Daoris.Service.Core/Sessions/Planted.cs", "var key = meta[\"thread_active_writer\"];", "thread_active_writer" },
        { "src/Daoris.Service/Daoris.Service.Core/Sessions/Planted.cs", "env[\"CLAUDE_CONFIG_DIR\"] = home;", "CLAUDE_CONFIG_DIR" },
        { "src/Daoris.Service/Daoris.Service.Mcp/Planted.cs", "if (agent.StartsWith(\"claude\")) return;", "StartsWith(agent)" },
        { "src/Daoris.Service/Daoris.Service.Shared/Planted.cs", "var label = \"claude-code\";", "\"claude-code\"" },
    };

    [Theory]
    [MemberData(nameof(Planted))]
    public void A_site_planted_in_the_service_is_named_by_the_census(string file, string source, string token)
    {
        var problems = Problems(Census([(file, source)]), Section.Value.Rows, exact: false);

        Assert.True(problems.Count > 0 && problems.All(each => each.StartsWith(file + ":", StringComparison.Ordinal)) && problems.Any(each => each.Contains(token)),
            $"expected {token} named in {file}, got: {string.Join(" | ", problems)}");
    }

    /// <summary>A second site in a file that already holds a named one, and a named one gone, are both named: the counts are exact.</summary>
    [Fact]
    public void A_site_added_to_a_named_file_or_gone_from_it_is_named_by_the_census()
    {
        var first = Section.Value.Rows[0];
        var more = Baseline.Value.ToDictionary(each => each.Key, each => new Dictionary<string, int>(each.Value));
        more[first.File][first.Token] = first.Count + 1;
        var fewer = Baseline.Value.ToDictionary(each => each.Key, each => new Dictionary<string, int>(each.Value));
        fewer[first.File].Remove(first.Token);

        Assert.Contains($"{first.File}: {first.Token} found {first.Count + 1}, the fixture names {first.Count}", Problems(more, Section.Value.Rows));
        Assert.Contains($"{first.File}: {first.Token} is no site now: take it off the fixture", Problems(fewer, Section.Value.Rows));
    }

    /// <summary>The census reads comments out of every scan, so a sentence naming a provider in one is no use of it.</summary>
    [Fact]
    public void A_comment_naming_a_provider_is_no_use_of_it()
    {
        var found = Census([("src/Daoris.Service/Daoris.Service.Core/Words.cs",
            "// CLAUDE_CONFIG_DIR and \"codex-acp\"\n/* CODEX_HOME */ var a = 1; /// thread_active_writer")]);

        Assert.Empty(found);
    }

    // ---- reading ----

    /// <summary>A source's text with its comments blanked and its strings kept, every offset and line kept.</summary>
    private static string StripComments(string text)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') { builder.Append(' '); i++; }
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? text.Length : end + 2;
                for (; i < end; i++) builder.Append(text[i] == '\n' ? '\n' : ' ');
            }
            else if (c == '"')
            {
                var verbatim = i > 0 && text[i - 1] == '@' || i > 1 && text[i - 1] == '$' && text[i - 2] == '@';
                var raw = i + 2 < text.Length && text[i + 1] == '"' && text[i + 2] == '"';
                var start = i;
                if (raw)
                {
                    var quotes = 0;
                    while (start + quotes < text.Length && text[start + quotes] == '"') quotes++;
                    var end = text.IndexOf(new string('"', quotes), start + quotes, StringComparison.Ordinal);
                    end = end < 0 ? text.Length : end + quotes;
                    builder.Append(text, start, end - start);
                    i = end;
                    continue;
                }

                builder.Append(c);
                i++;
                while (i < text.Length)
                {
                    var d = text[i];
                    builder.Append(d);
                    i++;
                    if (!verbatim && d == '\\' && i < text.Length) { builder.Append(text[i]); i++; continue; }
                    if (d == '"')
                    {
                        if (verbatim && i < text.Length && text[i] == '"') { builder.Append('"'); i++; continue; }
                        break;
                    }

                    if (d == '\n' && !verbatim) break;
                }
            }
            else if (c == '\'')
            {
                var end = i + 1;
                while (end < text.Length && text[end] != '\'' && text[end] != '\n') end += text[end] == '\\' ? 2 : 1;
                end = Math.Min(end + 1, text.Length);
                builder.Append(text, i, end - i);
                i = end;
            }
            else
            {
                builder.Append(c);
                i++;
            }
        }

        return builder.ToString();
    }

    /// <summary>Every production source of the section's projects, by its path under the workspace; the build's output is none.</summary>
    private static IEnumerable<(string File, string Source)> Sources()
    {
        var workspace = SourceFiles.WorkspaceFolder();
        foreach (var root in Section.Value.Roots)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(workspace, root), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                yield return (Path.GetRelativePath(workspace, file).Replace('\\', '/'), File.ReadAllText(file).Replace("\r\n", "\n"));
            }
        }
    }
}
