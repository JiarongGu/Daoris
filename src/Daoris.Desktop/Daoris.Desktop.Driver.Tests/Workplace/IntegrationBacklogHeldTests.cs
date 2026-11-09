using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git, an integration member's merge that git makes clean is held to the backlog's rows as the landing's replay is: a row
/// one member moved while the next archived it leaves, said, and one the first moved and changed while the next archived it is that
/// member's conflict, handed back with both texts. The rows' rules are <c>BacklogMergeTests</c>' table.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationBacklogHeldTests : LandedFixture
{
    private const string Sections = "# Tasks\n\n- [ ] **A1** one.\n- [ ] **A2** two.\n\n## Later\n";

    private IntegrationStore Store() => new(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);

    private IntegrationReplays Replays(IntegrationStore store) => new(store, DaorisHome.Entry(Home, HomeEntry.Trees));

    [Fact]
    public async Task A_row_one_member_moved_while_the_next_archived_it_leaves_said()
    {
        var (store, candidate) = await RunAsync(Sections.Replace("- [ ] **A2** two.\n", "") + "- [ ] **A2** two.\n");

        Assert.Equal(IntegrationMemberStates.Replayed, Member(store, candidate, "rb").State);
        Assert.Equal(Sections.Replace("- [ ] **A2** two.\n", ""), await GitAsync(Root, "show", $"{Member(store, candidate, "rb").Replayed}:TASKS.md"));
        var step = store.Steps(candidate.Id).Single(each => each is { Step: IntegrationStepNames.Replay, Request: "rb" });
        Assert.Contains("`TASKS.md`'s row `A2` left by its rows, where git had kept it", step.Said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_row_one_member_moved_and_changed_while_the_next_archived_it_is_the_next_ones_conflict_with_both_texts()
    {
        var (store, candidate) = await RunAsync(Sections.Replace("- [ ] **A2** two.\n", "") + "- [ ] **A2** two, then B.\n");

        // With no session to tell, a conflict is set aside for the steward's act.
        var second = Member(store, candidate, "rb");
        Assert.Equal(IntegrationMemberStates.Held, second.State);
        Assert.Contains("`TASKS.md`'s row `A2` changed on both sides and is left for a person: the line has “- [ ] **A2** two, then B.”", second.Said, StringComparison.Ordinal);
    }

    private string Root { get; set; } = "";

    /// <summary>Member a moves the backlog to <paramref name="moved"/>, member b archives A2 beside a file of its own; both queued and replayed.</summary>
    private async Task<(IntegrationStore Store, IntegrationCandidate Candidate)> RunAsync(string moved)
    {
        Root = await RepositoryAsync("engine");
        await CommitAsync(Root, "TASKS.md", Sections, "the backlog");
        var a = await BranchAsync(Root, "feature/a", ("TASKS.md", moved));
        var b = await BranchAsync(Root, "feature/b", ("b.txt", "b\n"), ("TASKS.md", Sections.Replace("- [ ] **A2** two.\n", "")));
        var store = Store();
        Queue(store, "ra", "feature/a", a, 1);
        Queue(store, "rb", "feature/b", b, 2);

        var run = await Replays(store).RunAsync(new IntegrationReplayAsk("engine", "aurora", Root, "main"));

        return (store, store.Candidate(run.Candidate!.Id)!);
    }

    private static async Task<string> BranchAsync(string root, string name, params (string File, string Content)[] commits)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name, "main");
        foreach (var (file, content) in commits) await CommitAsync(root, file, content, $"work on {name}: {file}");
        var tip = (await GitAsync(root, "rev-parse", name)).Trim();
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    private static void Queue(IntegrationStore store, string id, string branch, string tip, int order) =>
        Assert.Null(store.Add(new IntegrationRequest(
            id, "engine", "aurora", branch, tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate(Batch: 8).Resolved(), new DateTimeOffset(2026, 10, 30, 9, 0, order, TimeSpan.Zero))));

    private static IntegrationMember Member(IntegrationStore store, IntegrationCandidate candidate, string request) =>
        store.Members(candidate.Id).Single(each => each.Request == request);
}
