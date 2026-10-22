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
    public void A_remote_held_as_own_input_names_the_setting_and_tells_the_session_to_leave_it()
    {
        var told = GitHolds.Told([Held(failure: "own-input")])!;

        Assert.Contains("this repository names its own ssh command (`core.sshCommand`)", told, StringComparison.Ordinal);
        Assert.Contains("Do not fetch, pull or push it, and do not change that setting", told, StringComparison.Ordinal);
        Assert.StartsWith("`origin` is held: this repository names its own ssh command", GitHolds.Said(Held(failure: "own-input")), StringComparison.Ordinal);
        Assert.EndsWith("until that setting is removed from this repository", GitHolds.Said(Held(failure: "own-input")), StringComparison.Ordinal);
    }

    [Fact]
    public void Under_the_rewrite_a_session_is_told_what_git_remote_shows_and_to_name_its_tools_repository()
    {
        var told = GitHolds.Told([], rewritten: true, origin: "https://someone:s3cret@github.com/org-a/engine.git")!;

        Assert.StartsWith("**Git.** Daoris's git door stands in front of this repository's HTTP and HTTPS remotes, so `git remote -v` shows each as "
            + "`daoris::` followed by its address. `origin` shows as `daoris::https://github.com/org-a/engine.git`, and its address is "
            + "`https://github.com/org-a/engine.git`.", told, StringComparison.Ordinal);
        Assert.Contains("`gh --repo <owner>/<name>`", told, StringComparison.Ordinal);
        Assert.Contains("`--detect false`", told, StringComparison.Ordinal);
        Assert.Contains("`gh` and `az` here act as the person's own sign-ins", told, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", told, StringComparison.Ordinal);

        // An SSH origin is shown as it is: no example, the door's sentence alone.
        var ssh = GitHolds.Told([], rewritten: true, origin: "git@github.com:org-a/engine.git")!;
        Assert.DoesNotContain("`origin` shows as", ssh, StringComparison.Ordinal);
        Assert.Contains("`git remote -v` shows each as `daoris::`", ssh, StringComparison.Ordinal);

        // With a hold, the rewrite's words come first and the hold's after them, in one section.
        var both = GitHolds.Told([Held()], rewritten: true, origin: null)!;
        Assert.StartsWith("**Git.** Daoris's git door", both, StringComparison.Ordinal);
        Assert.Contains("`origin` (ssh.dev.azure.com) cannot be reached", both, StringComparison.Ordinal);
        Assert.Single(both.Split("**Git.**")[1..]);

        // Not rewritten (no remote helper laid out): as before.
        Assert.Null(GitHolds.Told([], rewritten: false, origin: "https://github.com/org-a/engine.git"));
        Assert.Equal(GitHolds.Told([Held()]), GitHolds.Told([Held()], rewritten: false, origin: "https://github.com/org-a/engine.git"));
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
