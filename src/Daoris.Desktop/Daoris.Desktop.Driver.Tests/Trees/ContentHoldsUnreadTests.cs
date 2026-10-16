using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PROOF-UNREAD1 (the fabric events design §B7, "absent is never zero", D57): a content proof whose git read failed says it is
/// unread, never "not held" and never held. Git's answers are scripted through <see cref="WorkingTree.Answered"/>, so no process
/// starts and each failed read (git missing, a ref that cannot be read, a timeout) is one fast case.
/// </summary>
public sealed class ContentHoldsUnreadTests
{
    private static readonly IReadOnlySet<string> NoHolders = new HashSet<string>();

    /// <summary>A repository whose line is <c>main</c>, one file changed on the branch that the line reads differently; <paramref name="fail"/> breaks one git ask.</summary>
    private static (int, string, string)? Git(IReadOnlyList<string> args, Func<IReadOnlyList<string>, (int, string, string)?> fail)
    {
        if (fail(args) is { } failed) return failed;
        var all = string.Join(' ', args);
        return args[0] switch
        {
            "rev-parse" when all.Contains("refs/remotes/origin/main") => (1, "", ""),
            "rev-parse" when all.Contains("refs/heads/main") && !all.Contains("^{") => (0, "m\n", ""),
            "rev-parse" when all.Contains("^{commit}") => (0, "c1\n", ""),
            "rev-parse" when all.Contains("^{tree}") => (0, "t0\n", ""),
            "merge-base" => (0, "base\n", ""),
            "diff" => (0, "a.txt\0", ""),
            "log" => (0, "t1\n", ""),
            "for-each-ref" => (0, "", ""),
            _ => (1, "", ""),
        };
    }

    private static async Task<ContentProof> AskAsync(Func<IReadOnlyList<string>, (int, string, string)?> fail)
    {
        WorkingTree.Answered.Value = args => Git(args, fail);
        try
        {
            return await ContentHolds.HeldByContentAsync("repo", "tip", "main", NoHolders, CancellationToken.None);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }

    [Fact]
    public async Task A_read_that_succeeds_and_finds_nothing_is_not_held()
    {
        var proof = await AskAsync(_ => null);

        Assert.Null(proof.Hold);
        Assert.Null(proof.Unread);
    }

    [Fact]
    public async Task Git_missing_reads_as_unread()
    {
        var proof = await AskAsync(_ => (-1, "", "git is not installed"));

        Assert.Null(proof.Hold);
        Assert.Contains("git is not installed", proof.Unread);
    }

    [Theory]
    [InlineData("rev-parse", "^{tree}")]
    [InlineData("rev-parse", "^{commit}")]
    [InlineData("merge-base", "")]
    [InlineData("diff", "")]
    [InlineData("log", "")]
    [InlineData("for-each-ref", "")]
    public async Task A_ref_git_cannot_read_reads_as_unread(string verb, string mark)
    {
        var proof = await AskAsync(args => args[0] == verb && string.Join(' ', args).Contains(mark) && (verb != "rev-parse" || mark.Length > 0)
            ? (128, "", "fatal: bad object tip")
            : null);

        Assert.Null(proof.Hold);
        Assert.NotNull(proof.Unread);
    }

    [Fact]
    public async Task A_timeout_reads_as_unread_and_says_so()
    {
        var proof = await AskAsync(args => args[0] == "merge-base" ? (-1, "", "git did not answer within 30 seconds, and was stopped.") : null);

        Assert.Null(proof.Hold);
        Assert.Contains("did not answer within 30 seconds", proof.Unread);
    }

    [Fact]
    public async Task No_common_ancestor_is_an_answer_not_a_failed_read()
    {
        var proof = await AskAsync(args => args[0] == "merge-base" ? (1, "", "") : null);

        Assert.Null(proof.Hold);
        Assert.Null(proof.Unread);
    }

    /// <summary><see cref="LandedProofs.ProveAsync"/> shares no gap: a failed read ends as unknown, never as on the line or merged.</summary>
    [Theory]
    [InlineData("merge-base")]
    [InlineData("diff")]
    public async Task A_landed_proof_whose_read_failed_is_unknown(string failing)
    {
        WorkingTree.Answered.Value = args => args[0] == failing ? (-1, "", "git did not answer within 30 seconds, and was stopped.")
            : args[0] == "rev-list" ? (0, "1\n", "") : (1, "", "");
        try
        {
            var proof = await LandedProofs.ProveAsync("repo", "tip", "main", [new LandedProofs.LineForm("main", "refs/heads/main")], CancellationToken.None);

            Assert.Equal(LandedKind.Unknown, proof.Kind);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }
}
