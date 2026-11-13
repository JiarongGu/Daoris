using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;
using static Daoris.Desktop.Driver.Tests.HistoryStandIn;
using StandIn = Daoris.Desktop.Driver.Tests.HistoryStandIn;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A piece's ref and its box, as the retention tests below make and read them: the piece's commit
/// grown from the lead's, so it holds the lead's history to its base, frozen at <see cref="PieceRefs.Of"/>.
/// </summary>
internal static class PieceRetention
{
    public const string Id = "p1";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-22T10:00:00Z");

    public static async Task<string> FreezeAsync(string root, string from)
    {
        var commit = (await GitAsync(root, "-c", "user.email=partner@example.test", "-c", "user.name=Partner",
            "commit-tree", $"{from}^{{tree}}", "-p", from, "-m", "the piece")).Trim();
        await GitAsync(root, "update-ref", PieceRefs.Of(Id), commit);
        return commit;
    }

    public static async Task<string> RefAsync(string root) =>
        (await GitAsync(root, "rev-parse", "--verify", "--quiet", PieceRefs.Of(Id))).Trim();

    /// <summary>The piece's box, asked by <paramref name="lead"/> and standing at <paramref name="state"/>, read back as each file's text.</summary>
    public static IReadOnlyDictionary<string, string> Box(string home, string lead, string commit, string state)
    {
        var files = new PartnerFiles(home);
        files.WriteAsk(new PartnerAsk(Id, lead, "Make the window seven days.", ["src"], "codex-acp", $"{commit}^", [], PartnerAsk.ByPerson, At));
        files.WriteState(new PartnerState(Id, state, null, At) { Agent = "codex-acp", Commit = commit, CopyRemoved = true });
        return Read(home);
    }

    public static IReadOnlyDictionary<string, string> Read(string home) =>
        Directory.EnumerateFiles(DaorisHome.Entry(home, HomeEntry.Partners))
            .Order(StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path), File.ReadAllText);
}

/// <summary>
/// 🔴 A tidy pass leaves a piece's ref and its box: the look removes a landed branch whose work the line holds, and the piece grown from
/// that branch stands where it froze, its box unchanged. A piece ref is no branch of Daoris's and no holder.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PieceTidyRetentionTests : BranchTidyingFixture
{
    [Fact]
    public async Task A_tidy_pass_leaves_a_pieces_ref_and_its_box()
    {
        var root = await RepositoryAsync("engine");
        var merged = await LandedTreeAsync(new SessionTrees(_home), root);
        var piece = await PieceRetention.FreezeAsync(root, merged.Branch);
        var box = PieceRetention.Box(_home, "s1", piece, PartnerStates.NotTakenIn);
        _ledger.Register("engine", root);
        using var log = Log();

        await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.DoesNotContain(merged.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.Equal(piece, await PieceRetention.RefAsync(root));
        Assert.Equal(box, PieceRetention.Read(_home));
    }
}

/// <summary>
/// 🔴 A sync leaves a piece's ref and its box: bringing the line up to date once origin's line holds the piece's commit fast-forwards
/// the line, and the piece's ref stands where it froze, its box unchanged. A sync's clean-up deletes landed branches, never a piece's ref.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PieceSyncRetentionTests : TreeSyncFixture
{
    [Fact]
    public async Task A_sync_leaves_a_pieces_ref_and_its_box_once_the_line_holds_its_commit()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var piece = await PieceRetention.FreezeAsync(root, "main");
        var box = PieceRetention.Box(Home, "s1", piece, PartnerStates.TakenIn);
        await GitAsync(root, "push", "--quiet", "origin", $"{piece}:refs/heads/main");

        var done = await new SessionTrees(Home).SyncAsync(Repositories(root), Nobody, fetch: true, scope: SyncScope.Everything);

        Assert.True(done.Lines.Single().Moved, done.Lines.Single().Message);
        Assert.Equal(piece, await RevAsync(root, "main"));
        Assert.Equal(piece, await PieceRetention.RefAsync(root));
        Assert.Equal(box, PieceRetention.Read(Home));
    }
}

/// <summary>
/// A clear of the closed quest a piece's lead served: it leaves the piece's ref and its box, since no cleanup
/// mutates a piece's ref; and while the piece still waits on the person (anywhere but taken in, discarded or refused) it is refused,
/// naming the lead, since the clear would take the lead's record the person's <i>Discard…</i> finds the piece's checkout by.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PieceClearRetentionTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "piece-retention", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_scratch, "home");

    private string Sessions => DaorisHome.Entry(Home, HomeEntry.Sessions);

    public PieceClearRetentionTests() => Directory.CreateDirectory(Sessions);

    public void Dispose() => GitSeed.Discard(_scratch);

    private async Task<(string Root, string Piece)> RepositoryAsync()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# engine\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", "first");
        return (root, await PieceRetention.FreezeAsync(root, "main"));
    }

    private HistoryWorld World(StandIn service)
    {
        service.Home = Home;
        return new HistoryWorld(service.Client(), Home, DaorisHome.Entry(Home, HomeEntry.DriverConfig), new SessionProcesses(Sessions));
    }

    /// <summary>Closed quest <c>q1</c>, served by the piece's lead <c>s1</c> alone.</summary>
    private static StandIn ClosedQuest()
    {
        var service = new StandIn { Records = [Record("s1", "q1")], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];
        return service;
    }

    private static readonly HistoryUnitName Q1 = new("quest", "q1");

    [Theory]
    [InlineData(PartnerStates.TakenIn)]
    [InlineData(PartnerStates.Discarded)]
    [InlineData(PartnerStates.Refused)]
    public async Task A_clear_of_its_closed_quest_goes_and_leaves_a_settled_pieces_ref_and_its_box(string state)
    {
        var (root, piece) = await RepositoryAsync();
        var box = PieceRetention.Box(Home, "s1", piece, state);
        var service = ClosedQuest();

        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q1", [Q1], PluginEvents.Screen);

        Assert.Equal(["quest:q1"], service.Pressed);
        Assert.Equal((1, 1), (outcome.Quests, outcome.Sessions));
        Assert.Equal(piece, await PieceRetention.RefAsync(root));
        Assert.Equal(box, PieceRetention.Read(Home));
    }

    /// <summary>🔴 A piece still waiting on the person keeps its lead's closed quest, listed and pressed alike, and the service is not asked.</summary>
    [Theory]
    [InlineData(PartnerStates.NotTakenIn)]
    [InlineData(PartnerStates.Handed)]
    [InlineData(PartnerStates.Answered)]
    [InlineData(PartnerStates.Uncertain)]
    [InlineData(PartnerStates.Started)]
    public async Task A_clear_of_its_closed_quest_is_refused_while_the_piece_waits_on_the_person(string state)
    {
        var (root, piece) = await RepositoryAsync();
        var box = PieceRetention.Box(Home, "s1", piece, state);
        var service = ClosedQuest();

        var listed = Assert.Single((await HistoryClearing.PlanAsync(World(service), HistoryScope.Quest, "q1")).Units).Keep;
        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q1", [Q1], PluginEvents.Screen);

        var keep = Assert.Single(outcome.Changed).Keep!;
        Assert.Equal((HistoryWords.NeedsYou, HistoryContexts.Piece, "s1"), (keep.Word, keep.Context, keep.Session));
        Assert.Equal(keep, listed);
        Assert.Contains($"`{PieceRetention.Id}`", keep.Message, StringComparison.Ordinal);
        Assert.Empty(service.Pressed);
        Assert.Equal(piece, await PieceRetention.RefAsync(root));
        Assert.Equal(box, PieceRetention.Read(Home));
    }
}
