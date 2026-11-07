using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row a, part 3 (the agent-provider design §9, D213): the service's twin of <see cref="ProviderCensusTests"/>. The
/// service names no driver type, so what can cross is a literal and a key: every site in the service's four production projects that
/// names a provider's id, variable, file or wire key is counted over the source with its comments blanked, and each is a named exception
/// in the <c>service</c> section of <c>fixtures/provider-census.json</c>, by file and token (the count is not pinned, CENSUSRULES1), the
/// design's row that moves it and its class. The ids and the vocabulary are the registrations' own, <see cref="ProviderVocabulary"/>, read
/// here in the suite that holds them: the service's tests reference no driver, and the page's generated types are written by no service
/// gate and tracked by no commit. A change to the service's sources reaches this suite through the service lane's gates.
/// The service's ordered call readers (<c>Core/Sessions/CallLineage.cs</c>, §3.3) and its open's default (D23's supported default, §6)
/// are named exceptions with the row or the decision that keeps them.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class ServiceProviderCensusTests
{
    /// <summary>The design's rows (§11) that can remove a site; <c>-</c> is a site no row removes.</summary>
    private static readonly string[] Rows =
        ["-", "b", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t"];

    private static readonly string[] Classes = ["behaviour", "permanent", "words", "measurement"];

    private sealed record Token(string Name, Regex Pattern);

    private sealed record Row(string File, string Token, string Rank, string Class, string Why);

    private static readonly Lazy<(string[] Roots, List<Row> Rows)> Section = new(() =>
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "provider-census.json")));
        var service = document.RootElement.GetProperty("service");
        return (
            service.GetProperty("roots").EnumerateArray().Select(each => each.GetString()!).ToArray(),
            service.GetProperty("exceptions").EnumerateArray().Select(row => new Row(
                row[0].GetString()!, row[1].GetString()!, row[2].GetString()!, row[3].GetString()!, row[4].GetString()!)).ToList());
    });

    private static readonly Lazy<List<Token>> Tokens = new(() =>
    {
        var tokens = new List<Token>();
        var ids = ProviderVocabulary.Ids.Select(row => row.Id).ToList();
        tokens.AddRange(ids.Select(id => new Token($"\"{id}\"", new Regex(Regex.Escape($"\"{id}\""), RegexOptions.CultureInvariant))));

        tokens.Add(new Token("StartsWith(agent)", new Regex(@"\.StartsWith\(\s*""(?:claude|codex|dsh)", RegexOptions.CultureInvariant)));
        // An id as an example or a name in a sentence (help, a refusal): words, but still a site that names a provider. A hyphened id is no
        // ordinary word, so every one the registrations hold is counted.
        var hyphened = string.Join('|', ids.Where(id => id.Contains('-')).OrderByDescending(id => id.Length).Select(Regex.Escape));
        tokens.Add(new Token("id in a sentence", new Regex($@"(?<![\w@/.\-])(?:{hyphened})(?![\w""\-])", RegexOptions.CultureInvariant)));
        foreach (var word in ProviderVocabulary.Words.Select(row => row.Word))
        {
            var plain = word.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && !word.StartsWith('_') && !word.EndsWith('_');
            tokens.Add(new Token(word, plain ? new Regex($@"(?<![A-Za-z0-9_]){Regex.Escape(word)}(?![A-Za-z0-9_])") : new Regex(Regex.Escape(word))));
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
            var text = ProviderCensusTests.StripComments(source);
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
        var allowed = rows.Select(each => (each.File, each.Token)).ToHashSet();
        var problems = new List<string>();
        foreach (var (file, counts) in found.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            foreach (var (token, count) in counts.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                if (!allowed.Contains((file, token))) problems.Add($"{file}: unnamed site {token} x{count}");
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
                .OrderBy(token => token.Key, StringComparer.Ordinal).Select(token => $"[\"{each.Key}\", \"{token.Key.Replace("\"", "\\\"")}\"]"))));
    }

    /// <summary>The fixture's own shape: every row a known row letter and class, a reason, a file that exists, no duplicate, in order.</summary>
    [Fact]
    public void The_fixtures_service_section_names_each_exception_once_in_order_with_its_row_class_and_reason()
    {
        var rows = Section.Value.Rows;
        var known = Tokens.Value.Select(token => token.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            rows.OrderBy(each => each.File, StringComparer.Ordinal).ThenBy(each => each.Token, StringComparer.Ordinal).Select(each => (each.File, each.Token)),
            rows.Select(each => (each.File, each.Token)));
        Assert.Equal(rows.Count, rows.Select(each => (each.File, each.Token)).Distinct().Count());
        foreach (var each in rows)
        {
            Assert.Contains(each.Rank, Rows);
            Assert.Contains(each.Class, Classes);
            Assert.True(each.Why.Length > 0, $"{each.File}: {each.Token} says no reason");
            Assert.Contains(each.Token, known);
            Assert.True(File.Exists(Path.Combine(WorkspaceRoot.Folder, each.File)), $"{each.File} is no file");
            // Only a behaviour is removed by a row; a permanent site, an id in words and a measurement stay.
            Assert.Equal(each.Class != "behaviour", each.Rank == "-");
        }
    }

    [Fact]
    public void The_census_reads_its_ids_and_its_vocabulary_from_the_registrations_a_word_for_each()
    {
        var names = Tokens.Value.Select(token => token.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(ProviderVocabulary.Ids.Count >= 9);
        Assert.True(ProviderVocabulary.Words.Count >= 15);
        foreach (var row in ProviderVocabulary.Ids) Assert.Contains($"\"{row.Id}\"", names);
        foreach (var row in ProviderVocabulary.Words) Assert.Contains(row.Word, names);
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

    /// <summary>A second site in a file that already holds a named one passes, and a named one gone is named: the sites are not counted (CENSUSRULES1).</summary>
    [Fact]
    public void A_site_added_to_a_named_file_passes_and_one_gone_from_it_is_named_by_the_census()
    {
        var first = Section.Value.Rows[0];
        var more = Baseline.Value.ToDictionary(each => each.Key, each => new Dictionary<string, int>(each.Value));
        more[first.File][first.Token] += 1;
        var fewer = Baseline.Value.ToDictionary(each => each.Key, each => new Dictionary<string, int>(each.Value));
        fewer[first.File].Remove(first.Token);

        Assert.Empty(Problems(more, Section.Value.Rows));
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

    /// <summary>Every production source of the section's projects, by its path under the workspace; the build's output is none.</summary>
    private static IEnumerable<(string File, string Source)> Sources()
    {
        var workspace = WorkspaceRoot.Folder;
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
