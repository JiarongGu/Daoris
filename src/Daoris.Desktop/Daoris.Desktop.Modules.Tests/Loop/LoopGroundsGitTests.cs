using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The loop's readers over a real repository in the test's scratch home (CONFIRMWAIT1, the confirm-wait design §15.1 items 1 and 2): a
/// discarded tree named by its path is bound by its head and its index, so a clean new commit and staged bytes changed under the same
/// working file each move it; and every branch's full tip is read at once.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LoopGroundsGitTests : DriverModuleBridge
{
    private (LoopGrounds Grounds, ServiceProvider Services, InstancePipe Pipe) Composed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Shenora.Core.Events.IEventBus>(Bus);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home);
        return (new LoopGrounds(Loop(), pipe), provider, pipe);
    }

    private static JsonElement Payload(object payload) => JsonSerializer.SerializeToElement(payload);

    [Fact]
    public async Task A_tree_named_by_its_path_is_bound_by_its_head_and_its_index()
    {
        var (grounds, services, pipe) = Composed();
        using var _ = services;
        using var __ = pipe;
        var tree = await InitAsync();
        var plain = Payload(new { path = tree });
        var forced = Payload(new { path = tree, force = true });

        // A clean new commit moves it, unforced and forced: the uncommitted paths are the same, none.
        var then = await grounds.FingerprintAsync("tree", plain, CancellationToken.None);
        var thenForced = await grounds.FingerprintAsync("tree", forced, CancellationToken.None);
        Assert.Equal(then, await grounds.FingerprintAsync("tree", plain, CancellationToken.None));
        await CommitAsync(tree, "b.txt", "b");
        Assert.NotEqual(then, await grounds.FingerprintAsync("tree", plain, CancellationToken.None));
        Assert.NotEqual(thenForced, await grounds.FingerprintAsync("tree", forced, CancellationToken.None));

        // Staged bytes changed while the working file stays the same move it.
        var file = Path.Combine(tree, "README.md");
        await File.WriteAllTextAsync(file, "staged one\n");
        await GitAsync(tree, "add", "README.md");
        await File.WriteAllTextAsync(file, "working\n");
        var staged = await grounds.FingerprintAsync("tree", forced, CancellationToken.None);
        await File.WriteAllTextAsync(file, "staged two\n");
        await GitAsync(tree, "add", "README.md");
        await File.WriteAllTextAsync(file, "working\n");
        Assert.NotEqual(staged, await grounds.FingerprintAsync("tree", forced, CancellationToken.None));
    }

    [Fact]
    public async Task Every_branch_s_full_tip_is_read_at_once()
    {
        var tree = await InitAsync();
        await GitAsync(tree, "branch", "quest/x");

        var tips = await WorkingTree.TipsAsync(tree);

        var head = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        Assert.NotNull(tips);
        Assert.Equal(head, tips["main"]);
        Assert.Equal(head, tips["quest/x"]);
        Assert.Equal(40, tips["main"].Length);
    }

    private async Task<string> InitAsync()
    {
        var root = DaorisHome.Entry(Home, HomeEntry.Trees, "games", "Lyntai", "t1");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await CommitAsync(root, "README.md", "Fixture\n");
        return root;
    }

    private static async Task CommitAsync(string root, string name, string text)
    {
        await File.WriteAllTextAsync(Path.Combine(root, name), text);
        await GitAsync(root, "add", name);
        await GitAsync(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--quiet", "-m", name);
    }

    private static async Task<string> GitAsync(string root, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(output, error);
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }
}
