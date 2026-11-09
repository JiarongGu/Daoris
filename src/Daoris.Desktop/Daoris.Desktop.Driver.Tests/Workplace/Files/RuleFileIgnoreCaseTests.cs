using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>core.ignorecase</c> in the first form's translation (the tree-pool design §38.1, check (iv)): where the setting holds, a top's rule
/// differs from its upper-cased spelling's in nothing, for this repository's first-form rules, the person's fixture and this
/// repository's file in git's syntax.
/// </summary>
public sealed class RuleFileIgnoreCaseTests
{
    private static readonly string[] Names = ["local", "_fixtures", "src", "Daoris.Web", "node_modules", "obj", "tools", "x", "bin", "docs", "index", "drafts", "App", "scratch", "build", "P", "logs"];

    [Fact]
    public void A_tops_rule_under_ignorecase_is_its_upper_cased_spellings_for_each_first_form_file()
    {
        foreach (var text in new[] { RuleFileFirstFormCorpusTests.RepositoryFirstForm, RuleFileFirstFormCorpusTests.PersonFirstForm })
        {
            var rules = RuleFileFirstFormCorpusTests.FirstFormRules(text);
            var entries = rules.Select((rule, at) => RuleFileFirstForm.Entries(rule).Select(entry => (at, entry))).SelectMany(each => each).ToList();
            int Rule(string path) => entries.LastOrDefault(each => GitPattern.Covers(each.entry, path, true)) is { entry: not null } hit ? hit.at : -1;
            var differ = RuleFileFirstFormCorpusTests.TopsOf(Names, 3).Append("BIN/")
                .Where(top => Rule(top) != Rule(top.ToUpperInvariant())).Select(top => $"'{top}' is {Rule(top)}, its upper case {Rule(top.ToUpperInvariant())}");
            Assert.Empty(differ.Take(20));
        }
    }

    [Fact]
    public void This_repositorys_file_in_gits_syntax_reads_a_top_and_its_upper_cased_spelling_alike()
    {
        var read = RepositoryFilesReader.Read(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, DeclarationNames.Rules)));
        Assert.Empty(read.Problems);
        int Rule(string path) => read.Rules.ToList().FindLastIndex(rule => rule.Matches(path, true));
        var differ = RuleFileFirstFormCorpusTests.TopsOf(Names, 3).Append("BIN/")
            .Where(top => Rule(top) != Rule(top.ToUpperInvariant())).Select(top => $"'{top}' is {Rule(top)}, its upper case {Rule(top.ToUpperInvariant())}");
        Assert.Empty(differ.Take(20));
    }
}
