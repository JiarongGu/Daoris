using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A merge driver defined in the configuration's system, global or command scope refuses a
/// history edit before any <c>merge-tree</c> starts, as one in the checkout's own scopes does (<see cref="GitHistoryEditTests"/>).
/// </summary>
/// <remarks>
/// Alone: those scopes are named by the process's environment (<c>GIT_CONFIG_SYSTEM</c>, <c>GIT_CONFIG_GLOBAL</c>,
/// <c>GIT_CONFIG_COUNT</c>), which every class beside it would share. Each is set only around the edit, after the repository is made, so
/// no seed is built under it.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Collection(Alone.Name)]
public sealed class GitHistoryEditScopesTests : TreeSyncFixture
{
    [Theory]
    [InlineData("system")]
    [InlineData("global")]
    [InlineData("command")]
    public async Task A_merge_driver_in_each_scope_the_environment_names_is_refused_before_merge_tree_starts(string scope)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "topic", "main");
        await CommitOnAsync(root, "topic", "t.txt", "t\n", "topic one");
        await CommitAsync(root, "m.txt", "m\n", "main one");
        var tip = await RevAsync(root, "topic");
        var file = Path.Combine(Scratch, $"{scope}.gitconfig");
        await File.WriteAllTextAsync(file, "[merge \"ours\"]\n\tdriver = true\n");
        var named = scope switch
        {
            "system" => new Dictionary<string, string?> { ["GIT_CONFIG_SYSTEM"] = file, ["GIT_CONFIG_NOSYSTEM"] = null },
            "global" => new Dictionary<string, string?> { ["GIT_CONFIG_GLOBAL"] = file },
            _ => new Dictionary<string, string?> { ["GIT_CONFIG_COUNT"] = "1", ["GIT_CONFIG_KEY_0"] = "merge.ours.driver", ["GIT_CONFIG_VALUE_0"] = "true" },
        };
        var was = named.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        var merges = 0;
        HistoryEditRun run;
        try
        {
            foreach (var (name, value) in named) Environment.SetEnvironmentVariable(name, value);
            WorkingTree.Started.Value = arguments => { if (arguments.Contains("merge-tree")) Interlocked.Increment(ref merges); };
            var runs = new ActRuns(new ActJournal(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell)) { ExitBound = TimeSpan.FromSeconds(2) };
            run = await GitHistoryEdits.EditAsync(
                runs, new HistoryEditAsk(Home, "engine", root, "rebase", "topic", "main") { Line = "main" }, "window", "window", CancellationToken.None);
        }
        finally
        {
            WorkingTree.Started.Value = null;
            foreach (var (name, value) in was) Environment.SetEnvironmentVariable(name, value);
        }

        Assert.True(run.Run.Refused, run.Run.Said);
        Assert.Contains("merge.ours.driver", run.Run.Said);
        Assert.Contains(scope, run.Run.Said);
        Assert.Equal(0, merges);
        Assert.Equal(tip, await RevAsync(root, "topic"));
    }
}
