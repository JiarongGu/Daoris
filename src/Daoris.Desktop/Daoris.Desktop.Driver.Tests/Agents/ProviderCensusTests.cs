using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row a (the agent-provider design §9, D213): every site in the desktop's code that knows a provider by a string or a
/// type is counted, and each is a named exception with the row that moves it, so a later row moves named sites and a new one cannot
/// appear unnoticed. The fixture is <c>fixtures/provider-census.json</c> (the exceptions, the providers' folders and the declared
/// references between them), the vocabulary seed <c>fixtures/provider-vocabulary.json</c>, which row t deletes once every word comes
/// from a provider's registration.
/// </summary>
/// <remarks>
/// <para><b>What is counted</b> (design §9): a door's test (<c>SessionWire.Acp</c> or <c>.Pipe</c>, the type <c>SessionWire</c>, a
/// <c>.Wire</c> read, the helpers that only hide that read, the spellings <c>"pipe"</c>, <c>"acp"</c> and <c>"protocol"</c>); an
/// agent's or a door's id as a literal, including a prefix test; a provider's type; and a provider's variable, file or wire key, from
/// the vocabulary seed. Comments are read out, and for the types and the doors so are strings, so a sentence naming one is no use of it.</para>
/// <para><b>How the three projects are read.</b> The structure analysis compiles the driver's sources alone, and the modules and the
/// host are not in that compilation, so this census reads all three the same way: by name in the source text, a type by its whole word
/// (a qualified, aliased or statically imported reference carries the name and counts; a different type of the same name would too, and
/// is named as another sense below). One reading for three projects keeps the exceptions in one table.</para>
/// <para><b>Outside the providers' folders</b> every site is an exception, listed by file and token with its reason; how many times a listed
/// file names a token is not pinned (CENSUSRULES1). <b>Inside</b> <c>Agents/Providers/&lt;Id&gt;/</c>
/// a provider's folder names another provider's type, variable or wire key only where the fixture declares that reference;
/// <c>Agents/Registry/Providers.cs</c>, the one registration list, names registrations and nothing else.</para>
/// </remarks>
public sealed class ProviderCensusTests
{
    private static readonly string[] Projects = ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules"];

    /// <summary>The design's rows (§11) that can remove a site; <c>-</c> is a site no row removes.</summary>
    private static readonly string[] Rows =
        ["-", "b", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t"];

    /// <summary>What kind of site an exception is (design §9): behaviour a row moves, a permanent one with its decision, an id as words, a measurement.</summary>
    private static readonly string[] Classes = ["behaviour", "permanent", "words", "measurement"];

    private enum Scan { Code, Text }

    private sealed record Token(string Kind, string Name, Regex Pattern, Scan On, string Owner);

    private sealed record Allowance(string File, string Token, string Row, string Class, string Why);

    private sealed record Fixture(
        string ProviderFolders, string RegistrationList, Dictionary<string, string> Folders,
        List<(string Folder, string Token, string Why)> Declared, List<(string File, string Token, string Why)> OtherSense,
        List<Allowance> Exceptions);

    private static Regex Word(string name) => new($@"\b{Regex.Escape(name)}\b", RegexOptions.CultureInvariant);

    private static Regex Quoted(string id) => new(Regex.Escape($"\"{id}\""), RegexOptions.CultureInvariant);

    /// <summary>The types a provider owns, by owner (the design's list, §9, and the rest of what the scan found declared for one).</summary>
    private static readonly (string Owner, string[] Types)[] ProviderTypes =
    [
        ("claude-code", ["ClaudeStreamJson", "ClaudeStreams", "ClaudeUsage", "ClaudeTrust", "ClaudeErrors", "ClaudeAcpErrors", "ClaudeAcp",
            "ClaudeLimits", "ClaudeWindows", "ClaudeSignIn", "ClaudeReleases", "ClaudeReleaseDownloads", "ClaudeCodeAdapter", "ClaudeAcpAdapter"]),
        ("codex", ["CodexUsage", "CodexAccounts", "CodexLimits", "CodexReleases", "CodexReleaseDownloads", "CodexAcpAdapter"]),
        ("dsh", ["DshProfile", "DshAdapter"]),
        ("stub", ["StubAdapter", "AcpStubAdapter"]),
    ];

    private static List<Token> Tokens()
    {
        var tokens = new List<Token>
        {
            new("door", "SessionWire.Acp", new Regex(@"\bSessionWire\s*\.\s*Acp\b"), Scan.Code, ""),
            new("door", "SessionWire.Pipe", new Regex(@"\bSessionWire\s*\.\s*Pipe\b"), Scan.Code, ""),
            new("door", "SessionWire", new Regex(@"\bSessionWire\b(?!\s*\.\s*(?:Acp|Pipe)\b)"), Scan.Code, ""),
            new("door", ".Wire", new Regex(@"\.\s*Wire\b(?!\s*\()"), Scan.Code, ""),
            new("door", "Wire(", new Regex(@"\b(?:SessionsConsole|[Hh]arnesses|Adapters)\s*\.\s*Wire\s*\("), Scan.Code, ""),
            new("door", "\"pipe\"", Quoted("pipe"), Scan.Text, ""),
            new("door", "\"acp\"", Quoted("acp"), Scan.Text, ""),
            new("door", "\"protocol\"", Quoted("protocol"), Scan.Text, ""),
            new("id", "StartsWith(agent)", new Regex(@"\.StartsWith\(\s*""(?:claude|codex|dsh)"), Scan.Text, ""),
            // An id as an example or a name in a sentence (help, a refusal): words, but still a site that names a provider.
            new("id", "id in a sentence", new Regex(@"(?<![\w@/.\-])(?:claude-code-acp|claude-code|codex-acp|acp-stub|alt-stub)(?![\w""\-])"), Scan.Text, ""),
            new("id", "AccessKey.ClaudeCode", new Regex(@"\b(?:AccessKey|AccessRules)\.ClaudeCode\b"), Scan.Code, "claude-code"),
        };
        foreach (var (owner, types) in ProviderTypes)
        {
            tokens.AddRange(types.Select(type => new Token("type", type, Word(type), Scan.Code, owner)));
        }

        using var vocabulary = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureFolder(), "provider-vocabulary.json")));
        // The ids an agent or a door goes by are the vocabulary seed's too, so the four censuses read one table.
        foreach (var row in vocabulary.RootElement.GetProperty("ids").EnumerateArray())
        {
            tokens.Add(new Token("id", $"\"{row[0].GetString()}\"", Quoted(row[0].GetString()!), Scan.Text, row[1].GetString()!));
        }

        foreach (var row in vocabulary.RootElement.GetProperty("words").EnumerateArray())
        {
            var word = row[0].GetString()!;
            var plain = word.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && !word.StartsWith('_') && !word.EndsWith('_');
            var pattern = plain ? new Regex($@"(?<![A-Za-z0-9_]){Regex.Escape(word)}(?![A-Za-z0-9_])") : new Regex(Regex.Escape(word));
            tokens.Add(new Token("vocabulary", word, pattern, Scan.Text, row[1].GetString()!));
        }

        return tokens;
    }

    private static string FixtureFolder() => Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures");

    private static Fixture ReadFixture()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureFolder(), "provider-census.json")));
        var root = document.RootElement;
        return new Fixture(
            root.GetProperty("providerFolders").GetString()!,
            root.GetProperty("registrationList").GetString()!,
            root.GetProperty("folders").EnumerateObject().ToDictionary(each => each.Name, each => each.Value.GetString()!, StringComparer.Ordinal),
            root.GetProperty("declaredReferences").EnumerateArray().Select(row => (row[0].GetString()!, row[1].GetString()!, row[2].GetString()!)).ToList(),
            root.GetProperty("otherSense").EnumerateArray().Select(row => (row[0].GetString()!, row[1].GetString()!, row[2].GetString()!)).ToList(),
            root.GetProperty("exceptions").EnumerateArray().Select(row => new Allowance(
                row[0].GetString()!, row[1].GetString()!, row[2].GetString()!, row[3].GetString()!, row[4].GetString()!)).ToList());
    }

    private static readonly Lazy<Fixture> TheFixture = new(ReadFixture);
    private static readonly Lazy<List<Token>> TheTokens = new(Tokens);
    private static readonly Lazy<Dictionary<string, Dictionary<string, int>>> Baseline = new(() => Census(Sources()));

    // ---- the census ----

    /// <summary>Every site of every source: file, then token, then how many times, the other senses left out.</summary>
    private static Dictionary<string, Dictionary<string, int>> Census(IEnumerable<(string File, string Source)> sources)
    {
        var found = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var tokens = TheTokens.Value;
        foreach (var (file, source) in sources)
        {
            string? code = null, text = null;
            foreach (var token in tokens)
            {
                var haystack = token.On == Scan.Code ? code ??= StructureCheck.CodeOf(source) : text ??= StripComments(source);
                var count = token.Pattern.Matches(haystack).Count;
                if (count == 0 || TheFixture.Value.OtherSense.Any(other => other.File == file && other.Token == token.Name)) continue;
                if (!found.TryGetValue(file, out var counts)) found[file] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
                counts[token.Name] = count;
            }
        }

        return found;
    }

    /// <summary>The problems a census holds against the fixture, in order; none is a passing census.</summary>
    private static List<string> Problems(Dictionary<string, Dictionary<string, int>> found, Fixture fixture, bool exact = true)
    {
        var problems = new List<string>();
        var owners = TheTokens.Value.ToDictionary(token => token.Name, token => token.Owner, StringComparer.Ordinal);
        // How often a listed file names a token is not the rule's (CENSUSRULES1): a file and token that are listed pass at any count.
        var allowed = fixture.Exceptions.Select(each => (each.File, each.Token)).ToHashSet();
        foreach (var (file, counts) in found.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            foreach (var (token, count) in counts.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                if (file == fixture.RegistrationList)
                {
                    problems.Add($"{file}: names {token} ({count}); the one registration list names registrations and nothing else");
                }
                else if (file.StartsWith(fixture.ProviderFolders, StringComparison.Ordinal))
                {
                    var folder = file[fixture.ProviderFolders.Length..].Split('/')[0];
                    if (!fixture.Folders.TryGetValue(folder, out var own))
                    {
                        problems.Add($"{file}: {folder} is no provider's folder the fixture names");
                    }
                    else if (owners[token] != "" && owners[token] != own && !fixture.Declared.Any(each => each.Folder == folder && each.Token == token))
                    {
                        problems.Add($"{file}: {own}'s folder names {token}, which is {owners[token]}'s, and its registration declares no such reference");
                    }
                }
                else if (!allowed.Contains((file, token)))
                {
                    problems.Add($"{file}: unnamed site {token} x{count}");
                }
            }
        }

        if (exact)
        {
            foreach (var each in fixture.Exceptions)
            {
                // An allowance nothing needs is taken back, so the table stays the sites that are true.
                if (!found.TryGetValue(each.File, out var counts) || !counts.ContainsKey(each.Token))
                {
                    problems.Add($"{each.File}: {each.Token} is no site now: take it off the fixture");
                }
            }
        }

        return problems;
    }

    [Fact]
    public void Every_site_that_knows_a_provider_is_a_named_exception_with_the_row_that_moves_it()
    {
        var problems = Problems(Baseline.Value, TheFixture.Value);

        Assert.True(problems.Count == 0, $"{problems.Count} problem(s):\n" + string.Join('\n', problems) + "\n\nthe sites found, as fixture rows:\n"
            + string.Join('\n', Baseline.Value.OrderBy(each => each.Key, StringComparer.Ordinal).SelectMany(each => each.Value
                .OrderBy(token => token.Key, StringComparer.Ordinal).Select(token => $"[\"{each.Key}\", \"{token.Key.Replace("\"", "\\\"")}\"]"))));
    }

    /// <summary>The fixture's own shape: every row a known row letter and class, a reason, a file that exists, no duplicate, in order.</summary>
    [Fact]
    public void The_fixture_names_each_exception_once_in_order_with_its_row_class_and_reason()
    {
        var fixture = TheFixture.Value;
        var tokens = TheTokens.Value.Select(token => token.Name).ToHashSet(StringComparer.Ordinal);
        var root = EveryChildIsHandedTheToolsTests.SourceRoot();

        Assert.Equal(
            fixture.Exceptions.OrderBy(each => each.File, StringComparer.Ordinal).ThenBy(each => each.Token, StringComparer.Ordinal).Select(each => (each.File, each.Token)),
            fixture.Exceptions.Select(each => (each.File, each.Token)));
        Assert.Equal(fixture.Exceptions.Count, fixture.Exceptions.Select(each => (each.File, each.Token)).Distinct().Count());
        foreach (var each in fixture.Exceptions)
        {
            Assert.Contains(each.Row, Rows);
            Assert.Contains(each.Class, Classes);
            Assert.True(each.Why.Length > 0,$"{each.File}: {each.Token} says no reason");
            Assert.Contains(each.Token, tokens);
            Assert.True(File.Exists(Path.Combine(root, each.File)), $"{each.File} is no file");
            // Only a behaviour is removed by a row; a permanent site, an id in words and a measurement stay.
            Assert.Equal(each.Class != "behaviour", each.Row == "-");
        }

        foreach (var other in fixture.OtherSense)
        {
            Assert.Contains(other.Token, tokens);
            Assert.True(File.Exists(Path.Combine(root, other.File)), $"{other.File} is no file");
        }
    }

    /// <summary>The other senses the census leaves out still hold the spelling they were verified for: take one off when it does not.</summary>
    [Fact]
    public void Each_other_sense_still_has_the_spelling_it_was_verified_for()
    {
        var root = EveryChildIsHandedTheToolsTests.SourceRoot();
        foreach (var (file, tokenName, _) in TheFixture.Value.OtherSense)
        {
            var token = TheTokens.Value.Single(each => each.Name == tokenName);
            var source = File.ReadAllText(Path.Combine(root, file)).Replace("\r\n", "\n");
            var haystack = token.On == Scan.Code ? StructureCheck.CodeOf(source) : StripComments(source);
            Assert.True(token.Pattern.IsMatch(haystack), $"{file} no longer has {tokenName}: take it off otherSense");
        }
    }

    // ---- the planted cases (design §11 row a): each must go red, and the terminal's pipe must not ----

    public static TheoryData<string, string, string> Planted => new()
    {
        { "Daoris.Desktop.Driver/Loop/Planted.cs", "if (adapter == \"codex-acp\") return;", "\"codex-acp\"" },
        { "Daoris.Desktop.Driver/Course/Planted.cs", "if (wire == SessionWire.Acp) return;", "SessionWire.Acp" },
        { "Daoris.Desktop.Driver/Participants/Planted.cs", "var streams = new ClaudeStreams(a, b);", "ClaudeStreams" },
        { "Daoris.Desktop.Driver/Setup/Planted.cs", "if (adapter.StartsWith(\"claude\")) return;", "StartsWith(agent)" },
        { "Daoris.Desktop.Modules/Platform/Planted.cs", "env[\"CODEX_HOME\"] = home;", "CODEX_HOME" },
        { "Daoris.Desktop.Driver/Agents/Registry/Providers.cs", "static string Id() => \"codex-acp\";", "\"codex-acp\"" },
        { "Daoris.Desktop.Driver/Agents/Registry/Providers.cs", "static bool Is(Provider p) => p is ClaudeCodeAdapter;", "ClaudeCodeAdapter" },
        { "Daoris.Desktop.Driver/Agents/Registry/Providers.cs", "static int Of(string id) { switch (id) { case \"stub\": return 1; default: return 0; } }", "\"stub\"" },
        { "Daoris.Desktop.Driver.Host/Planted.cs", "if (adapter == \"codex-acp\") return;", "\"codex-acp\"" },
        { "Daoris.Desktop.Driver/Agents/Providers/AltStub/Planted.cs", "var streams = new ClaudeStreams(a, b);", "ClaudeStreams" },
        { "Daoris.Desktop.Driver/Agents/Providers/Codex/Planted.cs", "env[\"CLAUDE_CONFIG_DIR\"] = home;", "CLAUDE_CONFIG_DIR" },
    };

    [Theory]
    [MemberData(nameof(Planted))]
    public void A_site_planted_outside_its_place_is_named_by_the_census(string file, string source, string token)
    {
        var problems = Problems(Census([(file, source)]), TheFixture.Value, exact: false);

        Assert.True(problems.Count > 0 && problems.All(each => each.StartsWith(file + ":", StringComparison.Ordinal)) && problems.Any(each => each.Contains(token)),
            $"expected {token} named in {file}, got: {string.Join(" | ", problems)}");
    }

    /// <summary>A second site in a file that already holds a named one passes, and a named one gone is named: the sites are not counted (CENSUSRULES1).</summary>
    [Fact]
    public void A_site_added_to_a_named_file_passes_and_one_gone_from_it_is_named_by_the_census()
    {
        const string file = "Daoris.Desktop.Driver/Course/HandOver/HandOverQuest.cs";
        var source = File.ReadAllText(Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), file)).Replace("\r\n", "\n");

        var more = Census(Sources().Where(each => each.File != file).Append((file, source + "\nvar again = wire == SessionWire.Acp;")));
        var fewer = Census(Sources().Where(each => each.File != file).Append((file, source.Replace("SessionWire.Acp", "Other"))));

        Assert.Empty(Problems(more, TheFixture.Value));
        Assert.Contains($"{file}: SessionWire.Acp is no site now: take it off the fixture", Problems(fewer, TheFixture.Value));
    }

    /// <summary>A provider's own folder names its own types, and another's where its registration declares the reference.</summary>
    [Fact]
    public void A_provider_folder_names_its_own_vocabulary_and_the_references_it_declares()
    {
        var own = Census([("Daoris.Desktop.Driver/Agents/Providers/ClaudeCode/Own.cs", "var s = new ClaudeStreams(a, b); env[\"CLAUDE_CONFIG_DIR\"] = home;")]);
        var borrowing = Census([("Daoris.Desktop.Driver/Agents/Providers/AltStub/Borrow.cs", "var s = new ClaudeStreams(a, b);")]);
        var fixture = TheFixture.Value with
        {
            Folders = new Dictionary<string, string>(TheFixture.Value.Folders) { ["ClaudeCode"] = "claude-code", ["AltStub"] = "alt-stub" },
        };

        Assert.Empty(Problems(own, fixture, exact: false));
        Assert.Single(Problems(borrowing, fixture, exact: false));
        Assert.Empty(Problems(borrowing, fixture with { Declared = [("AltStub", "ClaudeStreams", "OPENROUTER1 runs Claude Code's program")] }, exact: false));
    }

    /// <summary>The terminal's named pipe is no door: its files and a pipe server planted anywhere hold no unnamed site.</summary>
    [Fact]
    public void The_terminals_named_pipe_is_no_door_test()
    {
        var planted = Census([("Daoris.Desktop.Modules/Platform/PlantedPipe.cs",
            "using var server = new NamedPipeServerStream(PipeName.For(home), PipeDirection.InOut); var pipe = HomePipe.Of(home);")]);

        Assert.Empty(planted);
        // The files that hold the terminal's pipe are in the census as the permanent sites they are, by file.
        var named = TheFixture.Value.Exceptions.Where(each => each.Class == "permanent" && each.Token == "\"pipe\"")
            .Select(each => each.File).ToList();
        Assert.Contains("Daoris.Desktop.Driver/Platform/DaorisHome.cs", named);
    }

    /// <summary>The census reads strings out of the doors' and types' scan, and comments out of every scan.</summary>
    [Fact]
    public void A_sentence_naming_a_door_or_a_type_is_no_use_of_it()
    {
        var found = Census([("Daoris.Desktop.Driver/Loop/Words.cs",
            "// SessionWire.Acp and ClaudeStreams, and \"codex-acp\"\nvar text = \"SessionWire.Acp, ClaudeStreams\"; /* CODEX_HOME */")]);

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

    /// <summary>Every production source of the three projects, by its path under the desktop folder.</summary>
    private static IEnumerable<(string File, string Source)> Sources()
    {
        var root = EveryChildIsHandedTheToolsTests.SourceRoot();
        foreach (var project in Projects)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                yield return (Path.GetRelativePath(root, file).Replace('\\', '/'), File.ReadAllText(file).Replace("\r\n", "\n"));
            }
        }
    }
}
