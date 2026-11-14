using Daoris.Driver;
using Daoris.Ports.Repositories;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

public sealed class HandbackReaderTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private readonly string _tree = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "handback-reader-memory", "tree");
    private readonly MemoryRepository _repository;
    private readonly IDisposable _chosen;

    public HandbackReaderTests()
    {
        _repository = new MemoryRepository(_tree, "line");
        _chosen = FlowRepository.Use(_repository);
    }

    public void Dispose() => _chosen.Dispose();

    private async Task CommitAsync(string path, string text)
    {
        _repository.Write(_tree, path, text);
        Assert.True((await _repository.CommitAllAsync(_tree, path, CancellationToken.None)).Ok);
    }

    private async Task SwitchAsync(GitSwitch to) => Assert.True((await _repository.SwitchAsync(_tree, to, CancellationToken.None)).Ok);

    [Fact]
    public async Task A_tree_ahead_of_the_line_reads_its_base_its_touched_paths_and_the_gates_they_reach()
    {
        await CommitAsync("README.md", "one");
        var baseCommit = (await _repository.HeadAsync(_tree, CancellationToken.None))!;
        await SwitchAsync(new GitSwitch("work", Creates: true));
        await CommitAsync("src/Daoris.Web/a.ts", "a");
        var tip = (await _repository.HeadAsync(_tree, CancellationToken.None))!;
        IReadOnlyList<DeclaredGate> gates = [new("web", "npm test", null, false) { Paths = ["src/Daoris.Web/"] }, new("driver", "dotnet test", null, true) { Paths = ["src/Daoris.Desktop/"] }];
        var asked = (new CheckAsk("a1", "s1", "web", null, CheckAsk.ByAgent, At), (CheckState?)new CheckState("a1", CheckStates.Reached, At) { Tip = tip });

        var seen = await HandbackReader.ReadAsync(_tree, "line", gates, [asked], [], CancellationToken.None);

        Assert.Equal((tip, baseCommit, 0), (seen!.Tip, seen.Base, seen.Behind));
        Assert.Equal(["src/Daoris.Web/a.ts"], seen.Touched);
        Assert.Equal([("web", CheckVerdicts.Reached)], seen.Checks.Select(check => (check.Gate, check.Verdict)));
        Assert.True(seen.Ready);

        await SwitchAsync(new GitSwitch("line"));
        await CommitAsync("later.md", "moved");
        await SwitchAsync(new GitSwitch("work"));
        var behind = await HandbackReader.ReadAsync(_tree, "line", gates, [asked], [], CancellationToken.None);
        Assert.Equal(1, behind!.Behind);
        Assert.False(behind.Ready);
        Assert.Null(await HandbackReader.ReadAsync(_tree, "no-such-line", gates, [], [], CancellationToken.None));
    }

    [Fact]
    public void A_commit_without_its_makers_trailer_is_read_and_said_as_the_merge_check_refuses_it()
    {
        const string log =
            "a1\u001fp0\u001fsigned by Claude\u001fClaude Sonnet 5.5 <noreply@anthropic.com>\n\u001e\n"
            + "a2\u001fa1\u001fsigned by nobody\u001f\u001e\n"
            + "a3\u001fa2\u001fsigned by a person\u001fA Person <person@example.invalid>\n\u001e\n"
            + "a4\u001fa3 b9\u001fMerge branch 'line' into work\u001f\u001e\n";

        var seen = HandbackObservation.Judge("a4", "p0", 0, [], [], [], [], CommitAttribution.Unsigned(log));

        Assert.Equal(["signed by nobody", "signed by a person"], seen.Unsigned.Select(commit => commit.Subject));
        Assert.Contains("signed by nobody", seen.Words());
        Assert.Contains("Co-Authored-By", seen.Words());
        Assert.False(seen.Ready);
        Assert.Null(seen.Unsigned[0].Trailer);
        Assert.Equal("A Person <person@example.invalid>", seen.Unsigned[1].Trailer);
    }
}
