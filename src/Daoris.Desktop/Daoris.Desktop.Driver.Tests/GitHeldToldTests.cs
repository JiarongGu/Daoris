using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session is told in its instruction not to try a remote Daoris holds (GITAUTH1a, D198 point 7; the git-auth design §5.3): its Git
/// section, the driver's own words, beside the boundary and counted with it, on a first start, a resume and a carry-on alike. Without a
/// hold the instruction reads as it did.
/// </summary>
public sealed class GitHeldToldTests
{
    private static GitHold Held(string holds = GitHolds.All, string failure = "refused") => new(
        "D:/fam/engine", "engine", "default", "origin", "git@ssh.dev.azure.com:v3/org/project/engine", "ssh.dev.azure.com",
        GitHolds.Ambient, holds, failure, "git-a", "git@ssh.dev.azure.com: Permission denied (password,publickey).",
        new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_held_remote_is_said_not_to_be_tried()
    {
        Assert.Equal(
            "**Git.** `origin` (ssh.dev.azure.com) cannot be reached with the sign-in git has on this machine (the host refused it), so "
            + "neither you nor Daoris can reach it now. Do not fetch, pull or push it: work from the line as this checkout has it, and say "
            + "in your closing note that `origin` was not fetched. The person sees it in What needs you, where Try again lifts it.",
            GitHolds.Told([Held()]));
    }

    [Fact]
    public void A_remote_held_for_its_pushes_may_still_be_fetched()
    {
        var told = GitHolds.Told([Held(GitHolds.Writes, "write-refused")])!;

        Assert.Contains("refused a push with the sign-in git has on this machine. You may fetch it", told, StringComparison.Ordinal);
        Assert.DoesNotContain("Do not fetch", told, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_held_is_nothing_told()
    {
        Assert.Null(GitHolds.Told([]));
        Assert.Equal(TargetPrompt.Compose(TargetPromptGoldenTests.Part), TargetPrompt.Compose(TargetPromptGoldenTests.Part with { Git = null }));
    }

    [Fact]
    public void The_section_stands_after_the_boundary_and_is_counted_with_it_on_every_kind_of_start()
    {
        var told = GitHolds.Told([Held()])!;
        foreach (var target in new[]
        {
            TargetPromptGoldenTests.Part with { Git = told },
            TargetPromptGoldenTests.Bare with { CutOff = "it ended before closing the quest.", Git = told },
        })
        {
            var text = TargetPrompt.Compose(target);

            Assert.True(text.IndexOf(told, StringComparison.Ordinal) > text.IndexOf("Never write outside this repository", StringComparison.Ordinal));
            var boundary = TargetPrompt.Composed(target).Account.Sections.Where(section => section.Name == HandedSections.Boundary).Sum(section => section.Chars);
            var without = TargetPrompt.Composed(target with { Git = null }).Account.Sections.Where(section => section.Name == HandedSections.Boundary).Sum(section => section.Chars);
            Assert.Equal(without + told.Length + 2, boundary);
            Assert.Equal(text.Length, TargetPrompt.Composed(target).Account.Sections.Sum(section => section.Chars));
        }
    }
}
