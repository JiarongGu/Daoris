using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END7 over real git: each release of a due entry lands it on its own wake, with no whole look, and the git each wake starts
/// is counted (<see cref="ProcessStarts.Within"/>, the PROCSTART2 seam). A wake that concerns no due entry starts none; a whole look
/// reads every waiting tree again, as every look did before.
/// </summary>
/// <remarks>Real git, as <see cref="AutoLandingTests"/>, and a stand-in for the service's quests and records.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AutoLandingWakesTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly World _world = new();
    private readonly ITestOutputHelper _output;

    public AutoLandingWakesTests(ITestOutputHelper output)
    {
        _output = output;
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "auto-landing-wakes", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    /// <summary>
    /// A held done and a tree with uncommitted work wait. A ref move no due tree made and a session's record moving start no git; the
    /// hold's release lands its entry on the quests family's wake without reading the other tree; the landing's own branch wakes
    /// nothing; the uncommitted work committed wakes the refs, which land the second.
    /// </summary>
    [Fact]
    public async Task Each_release_lands_on_its_wake_and_a_wake_that_concerns_nothing_starts_no_git()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1", held: true);
        var tree = await DoneSessionAsync(root, "s2", "q2");
        await File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "not committed\n");

        // The whole look, the deadline: each entry read and kept once.
        var (_, first) = await CountedAsync(() => LookAsync(woke: null));
        Assert.Equal(2, first.Count);
        var (whole, again) = await CountedAsync(() => LookAsync(woke: null));
        Assert.Empty(again);

        // A ref move no due tree made (the person's branch), and a session's record moving: nothing is woken, no git starts.
        await GitAsync(root, "branch", "elsewhere");
        var woken = new List<(string Topic, string Subject)>();
        void Wake(string topic, string subject) => woken.Add((topic, subject));
        var (moved, _) = await CountedAsync(() =>
        {
            LandingWakes.Moved(_home, "engine", Wake);
            return Task.FromResult<IReadOnlyList<string>>([]);
        });
        Assert.Empty(woken);
        var (nothing, said) = await CountedAsync(() => LookAsync([new(ChangeTopics.Ledger, "sessions"), new(ChangeTopics.Refs, "engine")]));
        Assert.Empty(said);

        // The hold released (the quests family moved): its entry lands, and the waiting tree is not read.
        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false };
        var (released, landed) = await CountedAsync(() => LookAsync([new(ChangeTopics.Ledger, "quests")]));
        Assert.StartsWith("landing  session s1", Assert.Single(landed));
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Single(new AutoLandings(_home).Of("s2")!.Tries);

        // The landing's own branch moved the repository's refs, and no due tree's tip: its move wakes nothing.
        LandingWakes.Moved(_home, "engine", Wake);
        Assert.Empty(woken);

        // The uncommitted work committed: the tree's own ref move wakes the landing, which lands it.
        await CommitAsync(tree, "loose.txt", "now committed", "commit the loose file");
        LandingWakes.Moved(_home, "engine", Wake);
        Assert.Equal([(ChangeTopics.Refs, "engine")], woken);
        var (committed, second) = await CountedAsync(() => LookAsync([new(ChangeTopics.Refs, "engine")]));
        Assert.StartsWith("landing  session s2", Assert.Single(second));
        Assert.True(await BranchAsync(root, "feature/q2-fix-the-gap"));

        var report = $"whole look {whole.Count}: {string.Join("; ", whole)}\nreleased {released.Count}: {string.Join("; ", released)}\n"
                     + $"committed {committed.Count}: {string.Join("; ", committed)}";
        _output.WriteLine(report);
        // Counted down: the whole look reads the waiting tree again though nothing moved (two starts), as each look did before; a wake
        // that concerns nothing starts none; the hold's landing never reads the other tree.
        Assert.True(whole.Count == 2 && whole.All(start => start.EndsWith(tree, StringComparison.OrdinalIgnoreCase)), report);
        Assert.Empty(moved);
        Assert.Empty(nothing);
        Assert.DoesNotContain(released, start => start.EndsWith(tree, StringComparison.OrdinalIgnoreCase));
        Assert.True(released.Count > 0 && committed.Count > 0, report);
    }

    // ——— counting, the world and the fixtures

    /// <summary>Each git start the act makes through the gate, as <c>git &lt;arguments&gt; @ &lt;folder&gt;</c>.</summary>
    private static async Task<(List<string> Starts, IReadOnlyList<string> Said)> CountedAsync(Func<Task<IReadOnlyList<string>>> act)
    {
        var starts = new List<string>();
        IReadOnlyList<string> said;
        using (ProcessStarts.Within(info =>
        {
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase))
            {
                var arguments = info.ArgumentList.SkipWhile(each => each is "-c" or "core.longpaths=true");
                var folder = info.WorkingDirectory is { Length: > 0 } cwd ? Path.GetFullPath(cwd) : "";
                lock (starts) starts.Add($"git {string.Join(' ', arguments)} @ {folder}");
            }

            return Process.Start(info);
        }))
        {
            said = await act();
        }

        return (starts, said);
    }

    /// <summary>The service's quests and records, standing in.</summary>
    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(Records.Where(r => r.Live && r.Tree is not null).Select(r => r.Tree!).ToHashSet());

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    private AutoLander Lander() => new(_home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)));

    /// <summary>One look of the wakes given (null for the whole look): what it chose, then what landing those beside it said.</summary>
    private async Task<IReadOnlyList<string>> LookAsync(IReadOnlyCollection<LandingWake>? woke)
    {
        var lander = Lander();
        var (chosen, said) = await lander.ChooseAsync(Config(), CancellationToken.None, woke);
        return [.. said, .. await lander.LandAsync(chosen)];
    }

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>A session that worked in its own tree and concluded on its quest's done, made due as the driver's conclusion makes it.</summary>
    private async Task<string> DoneSessionAsync(string root, string session, string quest, bool held = false)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        _world.Quests[quest] = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", "Done") { Held = held };
        _world.Records.Add(new SessionRecord(session, "engine", "completed") { Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        var verdict = AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session, _world.Quests[quest],
            "Done", "completed", opened.Path, "aurora");
        Assert.Equal(AutoConcluded.Due, verdict);
        return Path.GetFullPath(opened.Path);
    }

    private void Rule(LandingRule rule) =>
        DriverConfig.Empty.WithLanding("engine", rule).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
        return root;
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        // Both streams read at once, before the wait (FIX-LOG 2026-10-04).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
