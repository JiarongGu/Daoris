using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The helper's instruction rendered whole (MIXAGENT2a-ii, D163 point 2; the mixed agents design §3): a question the asker wrote,
/// as it wrote it, and what a helper may do and never. A sentence added, moved or reworded shows here as the whole
/// instruction's difference, byte for byte, in <c>golden/helper-prompt/</c>; this class runs no process, so it is in the fast
/// half (MOD8).
/// </summary>
public sealed class HelperPromptGoldenTests
{
    internal static readonly HelperPacket Asked = new(
        "s1a2b3c4", "reports", "2222222222222222222222222222222222222222",
        "Why does the daily report drop its last day?\n\nI read src/report.ts and the window looks exclusive at the end,\nbut the test passes. What am I missing?", 15);

    internal static string GoldenPath(string file)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        var root = at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
        return Path.Combine(root, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "golden", "helper-prompt", file);
    }

    [Fact]
    public void The_helpers_instruction_is_its_golden_text()
    {
        Assert.Equal(File.ReadAllText(GoldenPath("asked.md")), HelperInstruction.Compose(Asked));
    }

    /// <summary>The asker's work in progress laid over its copy (COAGENT1a, D199 point 5), as a helper is told of it.</summary>
    internal static readonly HelperCaptured Captured = new(
        "0123456789ab", "2222222222222222222222222222222222222222", DateTimeOffset.Parse("2026-10-22T09:30:00Z"),
        [new CapturedFile("src/report.ts", new string('a', 64), 120), new CapturedFile("src/window.ts", new string('b', 64), 40)], ["old.md"], []);

    /// <summary>
    /// COAGENT1a: a helper handed a capture is told its copy holds the asker's uncommitted work, by the capture's name, its moment and
    /// what it holds, that git's status and diff show it, that ignored files were not carried and that the work may have moved; one
    /// with nothing uncommitted is told so. Neither is told its copy lacks the uncommitted work.
    /// </summary>
    [Fact]
    public void A_helper_handed_a_capture_is_told_its_copy_holds_the_work_in_progress()
    {
        var text = HelperInstruction.Compose(Asked with { Capture = Captured });

        Assert.Equal(File.ReadAllText(GoldenPath("captured.md")), text);
        Assert.DoesNotContain("What it has not committed is not in your copy", text);
        var clean = HelperInstruction.Compose(Asked with { Capture = Captured with { Files = [], Deleted = [] } });
        Assert.Contains("It had nothing uncommitted when its work was captured at 2026-10-22 09:30 UTC (capture `0123456789ab`)", clean);
        Assert.DoesNotContain("What it has not committed is not in your copy", clean);
    }

    /// <summary>The question reaches the helper as the asker wrote it, quoted line by line and never changed or summarised.</summary>
    [Fact]
    public void The_question_is_quoted_as_the_asker_wrote_it()
    {
        var text = HelperInstruction.Compose(Asked);

        Assert.Contains("> Why does the daily report drop its last day?\n>\n> I read src/report.ts and the window looks exclusive at the end,\n> but the test passes. What am I missing?\n", text);
    }

    /// <summary>
    /// What holds it read-only in words: the copy it reads in, every act it never does, the repository's own instructions to
    /// write set aside, no one to ask, and its one answer through `helper_answer`, never `helper_ask` or `opinion_give`.
    /// </summary>
    [Fact]
    public void It_is_told_it_reads_a_copy_never_writes_and_answers_once_through_helper_answer()
    {
        var text = HelperInstruction.Compose(Asked);

        Assert.Contains("a copy of the repository made for this question", text);
        Assert.Contains("Nothing in the copy is taken back", text);
        foreach (var never in new[]
                 {
                     "edit, write or delete a file", "commit, merge, rebase or push", "deploy or publish",
                     "change a permission rule or propose one", "a process, a port or a server of the person's",
                     "take, close, decline or publish a quest", "ask another agent for help",
                 })
        {
            Assert.Contains(never, text);
        }

        Assert.Contains("here they do not apply", text);
        Assert.Contains("Nobody will answer a question from you", text);
        Assert.Contains("`helper_answer`", text);
        Assert.DoesNotContain("`helper_ask`", text);
        Assert.DoesNotContain("`opinion_give`", text);
        Assert.Contains("at most 15 minutes", text);
        // Its answer is the asking session's claim to check, never the person's words.
        Assert.Contains("another agent's claim", text);
    }
}
