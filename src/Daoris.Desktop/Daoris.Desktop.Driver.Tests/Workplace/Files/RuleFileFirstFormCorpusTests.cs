using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The first form's translation proved by generation (the tree-pool design §38.1): every path of up to three tokens over the first
/// form's grammar, the words both reads named, this repository's file as it stood before its rewrite, the person's fixture and the
/// signatures, each read by the first form's own matcher and by its translation through <see cref="GitPattern"/> over every top of up
/// to three parts, the two agreeing top by top, and every word in exactly one class.
/// </summary>
public sealed class RuleFileFirstFormCorpusTests
{
    private static readonly string[] Tokens = ["a", "/", "**/", "**", "*", "?", "[", " ", "{", "{a,}", "{a,b/}", "{*,a}", "{a/,b/}"];

    private static readonly string[] Names = ["a", "b", "aa", "ab", "q", "[", "a ", "*a"];

    private static readonly IReadOnlyList<string> Tops = TopsOf(Names, 3);

    /// <summary>This repository's file as it stood in the first form, before its rewrite in git's syntax.</summary>
    internal const string RepositoryFirstForm = """
        { "rules": [
          { "kind": "local", "paths": ["local/"] },
          { "kind": "local", "paths": ["local/lyntai-local.props"], "copy": true },
          { "kind": "local", "paths": ["local/shenora-local.props"], "copy": true },
          { "kind": "scratch", "paths": ["_fixtures/", "**/_fixtures/"] },
          { "kind": "dependency", "paths": ["src/Daoris.Web/node_modules/"], "key": ["src/Daoris.Web/package-lock.json"] },
          { "kind": "dependency", "paths": ["node_modules/"], "key": ["package-lock.json"] },
          { "kind": "intermediate", "paths": ["src/**/obj/", "tools/**/obj/"] },
          { "kind": "output", "paths": ["src/Daoris.Contracts/bin/", "src/Daoris.Desktop/*/bin/", "src/Daoris.Service/*/bin/",
            "src/Daoris.Devkit/*/bin/", "tools/*/bin/", "src/Daoris.Service/Daoris.Service.Http/wwwroot/", "src/Daoris.Cli/dist/",
            "docs/index/", "src/Daoris.Web/src/data/generated/", "src/Daoris.Web/src/bridge/generated/"] } ] }
        """;

    /// <summary>The person's fixture: every sequence of both reads of rounds 14 to 17.</summary>
    internal const string PersonFirstForm = """
        { "rules": [
          { "kind": "local", "paths": ["{drafts,notes}/"] },
          { "kind": "dependency", "paths": ["src/a/node_modules/"] },
          { "kind": "dependency", "paths": ["node_modules/"] },
          { "kind": "output", "paths": ["src/{App,Lib}/bin/", "a**b", "[x]"] },
          { "kind": "local", "paths": ["local/x.txt"], "copy": true },
          { "kind": "dependency", "paths": ["deps/"], "share": true },
          { "kind": "scratch", "paths": ["scratch/**"] },
          { "kind": "scratch", "paths": ["scratch/", "scratch"] },
          { "kind": "output", "paths": ["build/**", "{*.js,x}"] },
          { "kind": "local", "paths": ["P/"] },
          { "kind": "output", "paths": ["P/*"] },
          { "kind": "cache", "paths": ["Xab**", "X**/Y", "{bin/,obj/}", "logs/**/", "*"] },
          { "kind": "dependency", "paths": ["d1/", "d2/", "d.lock"] } ] }
        """;

    /// <summary>The shipped signatures' paths as the first form wrote them, row by row and rule by rule.</summary>
    private static readonly string[][] SignaturesFirstForm =
    [
        ["node_modules/"], ["node_modules/"], ["node_modules/", ".pnp.cjs", ".pnp.loader.mjs"], [".yarn/cache/"], ["obj/"], ["bin/"], ["target/"],
        [".venv/"], ["**/__pycache__/", ".pytest_cache/", ".mypy_cache/", ".ruff_cache/"], [".gradle/"], ["build/"], ["target/"],
        ["build/", "out/build/"], ["vcpkg_installed/"], ["dist/"],
    ];

    [Fact]
    public void Every_generated_path_covers_every_top_as_the_first_form_did()
    {
        var paths = Generated().ToList();
        Assert.Equal(3899, paths.Count);
        var differ = new List<string>();
        foreach (var path in paths)
        {
            var entries = RuleFileFirstForm.Entries(new FileRule(FileKind.Scratch, [path]));
            Assert.All(entries, entry => Assert.NotEmpty(entry.Paths));
            var old = RoomPaths.Declared(path);
            foreach (var top in Tops)
            {
                if (old.IsMatch(top) != entries.Any(entry => GitPattern.Covers(entry, top, false))) differ.Add($"'{path}' at '{top}'");
            }

            if (differ.Count > 20) break;
        }

        Assert.Empty(differ);
    }

    [Fact]
    public void Every_word_is_in_exactly_one_class_and_the_named_words_in_theirs()
    {
        foreach (var path in Generated())
        {
            foreach (var word in RuleFileFirstForm.Words(path)) Assert.True(Enum.IsDefined(RuleFileFirstForm.ClassOf(word)));
        }

        Assert.Equal([FirstFormClass.Closed], Classes("logs/**/"));
        Assert.Equal([FirstFormClass.Files], Classes("*"));
        Assert.Equal([FirstFormClass.Kept], Classes("P/*"));
        Assert.Equal([FirstFormClass.Files, FirstFormClass.Kept], Classes("X**/{a,}"));
        Assert.Equal([FirstFormClass.Kept], Classes("X**{/}*"));
        Assert.Equal([FirstFormClass.Closed], Classes("scratch/**"));
        Assert.Equal([FirstFormClass.Closed], Classes("scratch/"));
        Assert.Equal([FirstFormClass.Files], Classes("scratch"));
        Assert.Equal([FirstFormClass.Closed], Classes("build/**"));
        Assert.Equal([FirstFormClass.Files, FirstFormClass.Files], Classes("{*.js,x}"));
        Assert.Equal([FirstFormClass.Kept, FirstFormClass.Kept], Classes("{bin/,obj/}"));

        Assert.Equal(["/logs/"], RuleFileFirstForm.Entries(new FileRule(FileKind.Cache, ["logs/**/"])).Single().Paths);
        Assert.Equal(["/*", "!/*/"], RuleFileFirstForm.Entries(new FileRule(FileKind.Cache, ["*"])).Single().Paths);
        Assert.True(RuleFileFirstForm.Entries(new FileRule(FileKind.Cache, ["P/*"])).Single().FirstForm);
    }

    [Fact]
    public void A_rules_paths_stay_a_union_and_the_later_rule_wins_for_every_pair_of_classes()
    {
        string[] representatives = ["a", "a*", "a/", "a/**", "*", "a/*", "{a/,b/}", "a**/", "a**", "a/**/"];
        foreach (var one in representatives)
        foreach (var other in representatives)
        {
            AssertSame([new FileRule(FileKind.Scratch, [one, other])]);
            AssertSame([new FileRule(FileKind.Scratch, [one]), new FileRule(FileKind.Output, [other])]);
        }
    }

    [Fact]
    public void This_repositorys_file_and_the_persons_fixture_read_alike_and_the_repositorys_holds_no_kept_word()
    {
        var repository = FirstFormRules(RepositoryFirstForm);
        Assert.DoesNotContain(repository.SelectMany(RuleFileFirstForm.Entries), entry => entry.FirstForm);
        AssertSame(repository, TopsOf(["local", "lyntai-local.props", "_fixtures", "src", "Daoris.Web", "node_modules", "obj", "tools", "x", "bin", "docs", "index"], 3));

        var person = FirstFormRules(PersonFirstForm);
        AssertSame(person, TopsOf(["drafts", "notes", "src", "App", "a", "bin", "node_modules", "aXb", "x", "scratch", "build", "y.js", "P", "deep", "Xabc", "Y", "logs", "d1", "d.lock", "[x]", "local", "x.txt"], 3));
        var copy = RuleFileFirstForm.Entries(person[4]).Single();
        Assert.Equal("copy", copy.Reach);
        Assert.Equal(["/local/x.txt", "!/local/x.txt/"], copy.Paths);
        Assert.True(person[5].Across);
    }

    [Fact]
    public void The_shipped_signatures_in_gits_syntax_cover_what_their_first_form_did()
    {
        var shipped = FileSignatures.Shipped.Rows.SelectMany(row => row.Rules).ToList();
        Assert.Equal(SignaturesFirstForm.Length, shipped.Count);
        var tops = TopsOf(["node_modules", ".pnp.cjs", ".yarn", "cache", "obj", "bin", "target", ".venv", "__pycache__", ".pytest_cache", "build", "out", "dist", "x"], 3);
        for (var i = 0; i < shipped.Count; i++)
        {
            var old = new FileRule(shipped[i].Kind, SignaturesFirstForm[i]);
            foreach (var top in tops) Assert.True(old.Paths.Any(path => RoomPaths.Declared(path).IsMatch(top)) == shipped[i].Matches(top), $"{string.Join(",", old.Paths)} at {top}");
        }
    }

    private static void AssertSame(IReadOnlyList<FileRule> rules, IReadOnlyList<string>? tops = null)
    {
        var entries = rules.Select((rule, at) => RuleFileFirstForm.Entries(rule).Select(entry => (at, entry))).SelectMany(each => each).ToList();
        foreach (var top in tops ?? Tops)
        {
            var old = rules.Select((rule, at) => (rule, at)).LastOrDefault(each => each.rule.Paths.Any(path => RoomPaths.Declared(path).IsMatch(top)));
            var now = entries.LastOrDefault(each => GitPattern.Covers(each.entry, top, false));
            Assert.True((old.rule is null ? -1 : old.at) == (now.entry is null ? -1 : now.at), $"{string.Join(" | ", rules.Select(rule => string.Join(",", rule.Paths)))} at '{top}'");
        }
    }

    internal static List<FileRule> FirstFormRules(string text)
    {
        using var document = JsonDocument.Parse(text);
        return [.. document.RootElement.GetProperty("rules").EnumerateArray().Select(FileRuleFile.FirstFormRule)];
    }

    private static IEnumerable<FirstFormClass> Classes(string path) => RuleFileFirstForm.Words(path).Select(RuleFileFirstForm.ClassOf);

    private static IEnumerable<string> Generated()
    {
        IEnumerable<string> paths = Tokens;
        IEnumerable<string> all = Tokens;
        for (var length = 2; length <= 3; length++)
        {
            paths = [.. paths.SelectMany(path => Tokens.Select(token => path + token))];
            all = all.Concat(paths);
        }

        return all.SelectMany(path => new[] { path, path + "/" }).Distinct(StringComparer.Ordinal).Where(path => RoomPaths.Unfit(path) is null);
    }

    internal static List<string> TopsOf(IReadOnlyList<string> names, int depth)
    {
        var tops = new List<string>();
        IEnumerable<string> level = names;
        for (var i = 1; i <= depth; i++)
        {
            tops.AddRange(level.SelectMany(path => new[] { path, path + "/" }));
            level = [.. level.SelectMany(path => names.Select(name => path + "/" + name))];
        }

        return tops;
    }
}
