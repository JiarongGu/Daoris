using System.Text;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <see cref="GitPattern"/> against git itself (the tree-pool design §36.2, 1): each sample entry's translated patterns written as a
/// <c>.gitignore</c> and every top asked of <c>git check-ignore --no-index</c>, the answer equal to the entry's cover top by top, with
/// <c>core.ignorecase</c> set both ways and each top asked again upper-cased. The sample is this repository's rule file in git's syntax,
/// the first form's corpus (every twentieth generated path, and the words both reads named) through its translation, and the excluded
/// parent with a <c>!</c> under it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitPatternOracleTests(ITestOutputHelper output) : IAsyncLifetime
{
    private static readonly string[] CorpusNames = ["a", "b", "aa", "ab", "q", "[", "a ", "*a"];

    private static readonly string[] RepositoryNames = ["local", "_fixtures", "src", "Daoris.Web", "node_modules", "obj", "tools", "bin", "docs", "index", "wwwroot", "dist"];

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-pattern-oracle-" + Guid.NewGuid().ToString("N")[..8]);

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        Assert.Equal(0, (await GitFixture.RunAsync(_root, "init", "-q", "-b", "main")).ExitCode);
    }

    public Task DisposeAsync()
    {
        GitSeed.Discard(_root);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_first_forms_translation_and_the_excluded_parent_read_as_git_reads_them()
    {
        List<IReadOnlyList<string>> entries = [new[] { "out/", "!out/keep/" }, new[] { "/*", "!/*/" }, new[] { "a/", "!a/b/" }];
        var corpus = RuleFileFirstFormCorpusTests.Generated().Where((_, at) => at % 20 == 0)
            .Concat(RuleFileFirstFormCorpusTests.FirstFormRules(RuleFileFirstFormCorpusTests.RepositoryFirstForm).SelectMany(rule => rule.Paths))
            .Concat(RuleFileFirstFormCorpusTests.FirstFormRules(RuleFileFirstFormCorpusTests.PersonFirstForm).SelectMany(rule => rule.Paths));
        entries.AddRange(corpus.SelectMany(path => RuleFileFirstForm.Entries(new FileRule(FileKind.Scratch, [path]))).Where(entry => !entry.FirstForm).Select(entry => entry.Paths));

        var tops = RuleFileFirstFormCorpusTests.TopsOf(CorpusNames, 2).Concat(RuleFileFirstFormCorpusTests.TopsOf(CorpusNames[..3], 3));
        await AssertAgreeAsync(entries, tops.Concat(["out/", "out/keep/", "out/keep/x", "out/x", "a/b/c"]));
    }

    [Fact]
    public async Task This_repositorys_rule_file_in_gits_syntax_reads_as_git_reads_it()
    {
        var read = RepositoryFilesReader.Read(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, RepositoryFilesReader.FileName)));
        Assert.Empty(read.Problems);
        Assert.Empty(read.Rules.Where(rule => rule.FirstForm));
        var entries = read.Rules.Select(rule => rule.Paths).ToList();
        var named = entries.SelectMany(paths => paths).Where(path => !path.StartsWith('!')).Select(path => GitPattern.Probe(path)!)
            .SelectMany(top => new[] { top, top + "/", top + "/x", top + "/x/" });
        var tops = RuleFileFirstFormCorpusTests.TopsOf(RepositoryNames[..8], 3).Concat(named).Distinct(StringComparer.Ordinal).ToList();
        await AssertAgreeAsync(entries, tops);
    }

    private async Task AssertAgreeAsync(IReadOnlyList<IReadOnlyList<string>> entries, IEnumerable<string> tops)
    {
        var distinct = entries.GroupBy(paths => string.Join('\n', paths)).Select(group => group.First()).ToList();
        var asked = tops.Concat(tops.Select(top => top.ToUpperInvariant())).Distinct(StringComparer.Ordinal).ToList();
        var input = string.Concat(asked.Select(top => top + "\0"));
        var differ = new List<string>();
        foreach (var ignoreCase in new[] { false, true })
        {
            foreach (var patterns in distinct)
            {
                await File.WriteAllTextAsync(Path.Combine(_root, ".gitignore"), string.Join('\n', patterns) + "\n", new UTF8Encoding(false));
                var run = await GitFixture.RunWithAsync(_root, null, input, "-c", $"core.ignorecase={(ignoreCase ? "true" : "false")}", "check-ignore", "--no-index", "--stdin", "-z");
                Assert.True(run.ExitCode is 0 or 1, run.Stderr);
                var ignored = run.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                foreach (var top in asked)
                {
                    if (ignored.Contains(top) != GitPattern.Covers(patterns, top, ignoreCase)) differ.Add($"[{string.Join(" ", patterns)}] ignorecase={ignoreCase} at '{top}': git {ignored.Contains(top)}");
                }
            }
        }

        output.WriteLine($"{distinct.Count} entries x {asked.Count} tops x 2 settings = {distinct.Count * asked.Count * 2} answers compared with git, {differ.Count} differ");
        Assert.True(distinct.Count > 0);
        Assert.Empty(differ.Take(20));
    }
}
