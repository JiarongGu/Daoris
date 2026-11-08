using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Whether a park resumes where its line's tree is in a pool: it goes on where a slot can be brought back, and falls back where the tree is gone.</summary>
public sealed class PooledContinuationTests
{
    private static readonly HarnessConversation Kept = new("claude-code-acp", "0b5e7c1a");

    private static ContinueReason? Judged(string tree, string? home) =>
        Continuations.Judge(
            new PriorSession("s1", tree, "awaiting-person", "It stopped to ask you:\n\nWhich port?", "engine")
            {
                Said = PersonWords.Of("Port 8080."),
                Profile = "account-1",
                Adapter = "claude-code-acp",
            },
            "claude-code-acp", doorResumes: true, profile: "account-1", Kept, home: home);

    [Fact]
    public void A_park_whose_line_s_slot_another_line_took_goes_on_where_a_gone_one_falls_back()
    {
        using var lines = new PooledLines();
        lines.Slot(1, "s-aaaa");
        lines.LetGo(1, "s-aaaa");
        lines.Take(1, "s-bbbb");

        Assert.Null(Judged(lines.Tree("s-aaaa"), lines.Home));
        Assert.Equal(ContinueWhy.Tree, Judged(lines.Tree("s-aaaa"), null)?.Code);
        Assert.Equal(ContinueWhy.Tree, Judged(lines.Tree("s-never"), lines.Home)?.Code);
    }
}
