namespace Daoris.Driver.Tests;

/// <summary>
/// INTEGRATE1l (the integration design §9 points 1 and 2): the look reads what each running unit changed, one git read per running session
/// of this machine, and two running units whose branches change one path are each told once, as an ordinary word.
/// </summary>
public sealed class WorkOverlapsTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "_overlaps-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkOverlapsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static SessionView Running(string id, string quest) =>
        new(id, "Game", "running") { Quest = quest, Tree = $"D:/trees/{id}", BaseCommit = new string('a', 40) };

    private static Snapshot Two(IReadOnlyList<string> one, IReadOnlyList<string> other) =>
        new Snapshot([], [], [Running("s1", "q1"), Running("s2", "q2")])
        {
            Changed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["q1"] = one, ["q2"] = other },
        };

    [Fact]
    public async Task The_look_reads_one_diff_per_running_session_of_this_machine_and_names_what_is_integrating()
    {
        var asked = new List<string>();
        var snapshot = new Snapshot([], [], [Running("s1", "q1"), Running("s2", "q2"), Running("origin/s3", "q3"), new SessionView("s4", "Game")]);

        var looked = await WorkOverlaps.LookAsync(snapshot, new HashSet<string> { "q9" }, (tree, _, _) =>
        {
            asked.Add(tree);
            return Task.FromResult<IReadOnlyList<string>?>([$"{tree[^2..]}/file.cs"]);
        });

        Assert.Equal(["D:/trees/s1", "D:/trees/s2"], asked);
        Assert.Equal(["s1/file.cs"], looked.Changed["q1"]);
        Assert.Contains("q9", looked.Integrating);
    }

    [Fact]
    public async Task Two_running_units_whose_branches_change_one_path_are_each_told_once()
    {
        var said = new List<(string Session, string Text, string Key)>();
        Task Say(string session, string text, string key, CancellationToken ct)
        {
            said.Add((session, text, key));
            return Task.CompletedTask;
        }

        var snapshot = Two(["src/a.cs", "src/b.cs"], ["src/b.cs", "src/c.cs"]);
        await WorkOverlaps.TellAsync(_home, snapshot, Say);
        await WorkOverlaps.TellAsync(_home, snapshot, Say);

        Assert.Equal(2, said.Count);
        Assert.Contains(said, word => word.Session == "s1" && word.Text.Contains("overlaps `#q2`: `src/b.cs`", StringComparison.Ordinal));
        Assert.Contains(said, word => word.Session == "s2" && word.Text.Contains("overlaps `#q1`: `src/b.cs`", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Units_changing_no_shared_path_are_told_nothing()
    {
        var said = 0;
        await WorkOverlaps.TellAsync(_home, Two(["src/a.cs"], ["src/c.cs"]), (_, _, _, _) =>
        {
            said++;
            return Task.CompletedTask;
        });

        Assert.Equal(0, said);
    }
}
