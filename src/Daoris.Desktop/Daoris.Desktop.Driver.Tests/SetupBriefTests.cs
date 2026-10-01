using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAYOUT7 (D117 §6.2–§6.3, as D124 §2.2–§2.7 amends them): the set-up quest's words. The session that carries it
/// cannot read the adoption playbook, which is Daoris's own, so the body is the playbook in the canon's words, and
/// the two are twins: a test here reads the playbook's steps and holds that the body names each.
/// </summary>
public sealed partial class SetupBriefTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    /// <summary>
    /// The twin's table: each step of the playbook (<c>.claude/knowledge/adoption.md</c>), by its heading, and the
    /// words the whole set-up's body names it by. A step added to the playbook has no row and fails here, until the
    /// composer says it too.
    /// </summary>
    private static readonly (string Heading, string Body)[] Playbook =
    [
        ("`init`, then read what it prints", "`daoris init --harness agents`"),
        ("`sync --dry-run` and read the collisions", "`daoris sync --dry-run`"),
        ("Preserve the mechanism in a local companion", "keep its mechanism"),
        ("Hunt renamed twins by hand", "renamed twins"),
        ("`sync --force`, then `check`", "`daoris sync --force`"),
        ("Initialise the knowledge", "**Initialise the knowledge.**"),
        ("Write the brief", "**Write the brief**"),
        ("Declare the documents and the rooms", "**Declare the documents and the rooms.**"),
        ("Declare the safe work", "**Declare the safe work**"),
        ("Expect the budget to be over, and do not paper over it", "the always-loaded budget"),
        ("Verify the repository, not just the doctrine", "own build and tests"),
        ("Hand it over for review — this is the human checkpoint", "the branch is what is reviewed"),
    ];

    /// <summary>The canon's words for the knowledge step (D124 §2.5), for the quest body and the playbook alike.</summary>
    private const string CanonKnowledge =
        "Write what a session in another repository would need from this one and could not find: what it owns and "
        + "where, what it promises and the shape of its data, and how the figures others rely on are computed, each "
        + "fact with the place in the code that holds it. Say which facts the code did not confirm.";

    [Theory]
    [InlineData(SetupCase.Whole, SetupQuests.SetUp)]
    [InlineData(SetupCase.Move, SetupQuests.Move)]
    [InlineData(SetupCase.Declare, SetupQuests.Declare)]
    public void The_title_is_the_press_words_with_the_day_so_the_log_knows_it_for_a_set_up(SetupCase kind, string stem)
    {
        var (title, _) = SetupBrief.Compose(Input(kind));

        Assert.Equal($"{stem} (2026-10-01)", title);
        Assert.True(SetupQuests.IsSetup(title));
    }

    [Fact]
    public void The_whole_set_ups_body_names_each_of_the_playbooks_steps()
    {
        // Every step heading, whatever it is numbered: a step added as `6b.` or unnumbered is a step too.
        var headings = Regex.Matches(File.ReadAllText(PlaybookPath()).ReplaceLineEndings("\n"), @"^### (?:[0-9]+[a-z]?\.\s+)?(.+)$", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value.Trim())
            .ToList();
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole));

        Assert.Equal(Playbook.Select(row => row.Heading), headings);
        foreach (var (heading, words) in Playbook)
        {
            Assert.True(body.Contains(words, StringComparison.Ordinal), $"the body names no `{heading}` (looked for {words}).");
        }
    }

    /// <summary>The playbook says the knowledge step in the canon's words, and the body says the same words.</summary>
    [Fact]
    public void The_knowledge_step_is_in_the_canons_words_in_the_body_and_the_playbook()
    {
        var playbook = string.Join(" ", File.ReadAllText(PlaybookPath()).ReplaceLineEndings("\n").Split('\n').Select(line => line.Trim()));
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole));

        Assert.Contains(CanonKnowledge, string.Join(" ", body.Split('\n').Select(line => line.Trim())));
        Assert.Contains(CanonKnowledge, playbook);
    }

    /// <summary>
    /// D124 §2.3's order: the tool's check first, the doctrine taken up, then the knowledge, then the brief, the
    /// documents, the safe work, the checks, and the commit.
    /// </summary>
    [Fact]
    public void The_steps_run_tool_doctrine_knowledge_brief_documents_safe_work_verify_commit()
    {
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole));

        string[] order =
        [
            "**The tool.**", "`daoris init --harness agents`", "`daoris sync --dry-run`", "**Initialise the knowledge.**",
            "**Write the brief**", "**Declare the documents and the rooms.**", "**Declare the safe work**", "**Verify.**", "**Commit**",
        ];
        var at = order.Select(words => body.IndexOf(words, StringComparison.Ordinal)).ToList();

        Assert.DoesNotContain(-1, at);
        Assert.Equal(at.Order(), at);
    }

    /// <summary>The tool is checked first, at the version the press found, and every command is the bare name: never a runner.</summary>
    [Fact]
    public void The_tool_is_checked_first_at_its_version_and_is_never_run_through_npx()
    {
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole) with { Version = "0.0.7" });

        Assert.Contains("Run `daoris --version`. It prints `0.0.7`.", body);
        Assert.Contains("*the doctrine command could not run here*", body);
        Assert.DoesNotContain("npx", body);
    }

    /// <summary>What the press adds is what the body asks the session to run: each exact verb of the rule appears in it.</summary>
    [Fact]
    public void Every_verb_the_press_allows_is_one_the_body_asks_for()
    {
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole));

        foreach (var verb in SetupPress.Verbs) Assert.Contains($"`{verb}`", body);
    }

    /// <summary>
    /// 🔴 Read in a repository that may know nothing of Daoris: no decision number, no row of its backlog, no path of
    /// Daoris's own, and no machine path, only the layout's own file names.
    /// </summary>
    [Theory]
    [InlineData(SetupCase.Whole)]
    [InlineData(SetupCase.Move)]
    [InlineData(SetupCase.Declare)]
    public void The_body_is_in_the_canons_words(SetupCase kind)
    {
        var (title, body) = SetupBrief.Compose(Input(kind));
        var text = title + "\n" + body;

        Assert.DoesNotMatch(DecisionNumber(), text);
        Assert.DoesNotMatch(@"\b(LAYOUT|WSSETUP|TOOLS|UNBLOCK|DOC)\d", text);
        foreach (var path in new[] { "docs/", "src/Daoris", "adoption.md", "DAORIS_HOME", "daoris-driver", @"C:\", "/Users/" })
        {
            Assert.DoesNotContain(path, text);
        }
    }

    /// <summary>D124 §2.6: what a set-up writes, and the bounds it never crosses.</summary>
    [Fact]
    public void The_body_says_what_it_writes_and_what_it_never_does()
    {
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole));

        Assert.Contains("Write only in this tree, on this branch", body);
        foreach (var never in new[]
        {
            "push, merge or open a pull request", "write outside this tree", "publish a request to another repository",
            "run `daoris connect` or `daoris upstream`", "`remote.join` or `remote.knowledge`", "change a source, build or CI file",
        })
        {
            Assert.Contains(never, body);
        }
    }

    /// <summary>D124 §2.7: the close names what the owner reviews.</summary>
    [Fact]
    public void The_close_names_what_the_owner_reviews_or_the_reason_it_declined()
    {
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole));

        foreach (var item in new[]
        {
            "Close it `done`", "the tool's version", "each collision and how it was resolved", "each twin retired",
            "the domain, in its words", "each fact marked not confirmed", "that the safe work waits for the person's yes",
            "the root `AGENTS.md`'s bytes", "what was chosen about links", "Or decline, with the reason",
        })
        {
            Assert.Contains(item, body);
        }
    }

    /// <summary>D124 §2.2: what was read there, from the line at a named commit, and what the workspace knows of it.</summary>
    [Fact]
    public void The_body_says_what_was_read_from_the_line_and_the_workspace()
    {
        var input = Input(SetupCase.Whole) with
        {
            Description = new RepositoryDescription("Reports — the monthly figures.", [".NET", "Angular"], "README.md"),
            Entries = ["docs/DECISIONS.md", "docs/DECISIONS.md", "CHANGELOG.md"],
            Neighbours = ["billing", "ledger"],
        };

        var (_, body) = SetupBrief.Compose(input);

        Assert.Contains("from its line `main` at commit `abc1234`", body);
        Assert.Contains("Reports — the monthly figures.", body);
        Assert.Contains(".NET, Angular", body);
        Assert.Contains("holds 3 entries for it, from 2 files: `docs/DECISIONS.md` (2), `CHANGELOG.md` (1)", body);
        Assert.Contains("`billing`, `ledger`", body);
        Assert.Contains("`daoris.json`: absent", body);
    }

    [Fact]
    public void An_empty_index_and_a_workspace_of_one_are_said_too()
    {
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole));

        Assert.Contains("The workspace's index holds nothing for it yet.", body);
        Assert.Contains("This workspace holds no other repository.", body);
    }

    /// <summary>
    /// 🔴 The reference's shape on a checkout without links: the body says what an agent here reads, and that the
    /// choice of a file holding the import is the repository's.
    /// </summary>
    [Fact]
    public void A_claude_md_link_on_a_checkout_without_links_is_said_with_both_answers()
    {
        var facts = new LayoutReaderTests.Scratch().File("AGENTS.md", "# Brief\n").Link("CLAUDE.md", "AGENTS.md").Read(symlinks: false);

        var (_, body) = SetupBrief.Compose(Input(SetupCase.Whole) with { Facts = facts });

        Assert.Contains("`CLAUDE.md`: a link to `AGENTS.md`", body);
        Assert.Contains("this checkout holds links as text (`core.symlinks=false`)", body);
        Assert.Contains("a real file holding `@AGENTS.md`", body);
        Assert.Contains("the choice is this repository's", body);
    }

    /// <summary>The knowledge step alone, for an adopter that declares nothing: no init, no move.</summary>
    [Fact]
    public void Declaring_carries_the_tool_the_knowledge_the_checks_and_the_commit_alone()
    {
        var (_, body) = SetupBrief.Compose(Input(SetupCase.Declare));

        Assert.Contains("**The tool.**", body);
        Assert.Contains("**Initialise the knowledge.**", body);
        Assert.Contains("**Verify.**", body);
        Assert.Contains("**Commit**", body);
        Assert.DoesNotContain("daoris init", body);
        Assert.DoesNotContain("**Write the brief**", body);
    }

    /// <summary>A move names the manifest's two fields and the `git mv`, and carries the knowledge step only when the manifest declares nothing.</summary>
    [Fact]
    public void A_move_names_the_two_fields_and_carries_the_knowledge_step_only_when_nothing_is_declared()
    {
        var silent = new LayoutReaderTests.Scratch().File("daoris.json", "{}").File("daoris.lock", """{"entries":[]}""").Read();
        var declared = new LayoutReaderTests.Scratch().File("daoris.json", """{"domain":{"summary":"x"}}""").File("daoris.lock", """{"entries":[]}""").Read();

        var (_, moving) = SetupBrief.Compose(Input(SetupCase.Move) with { Facts = silent });
        var (_, keeping) = SetupBrief.Compose(Input(SetupCase.Move) with { Facts = declared });

        Assert.Contains("`\"harness\": \"agents\"`", moving);
        Assert.Contains("`\"target\": \".agents\"`", moving);
        Assert.Contains("`git mv`", moving);
        Assert.DoesNotContain("daoris init", moving);
        Assert.Contains("**Initialise the knowledge.**", moving);
        Assert.DoesNotContain("**Initialise the knowledge.**", keeping);
    }

    private static SetupBriefInput Input(SetupCase kind) => new(
        kind, Day, "reports", new LayoutReaderTests.Scratch().File("README.md", "# Reports\n").Read(), "0.0.1",
        Description: null, Entries: [], Neighbours: []);

    private static string PlaybookPath()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return Path.Combine(at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary"),
            ".claude", "knowledge", "adoption.md");
    }

    [GeneratedRegex(@"\bD\d{1,3}\b")]
    private static partial Regex DecisionNumber();
}
