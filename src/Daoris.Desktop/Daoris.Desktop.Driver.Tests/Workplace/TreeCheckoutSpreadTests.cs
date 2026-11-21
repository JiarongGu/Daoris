using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A tree made with git's parallel checkout holds every file the line holds, as one made without it: over real git, past the count git spreads at.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeCheckoutSpreadTests : IDisposable
{
    private const int Files = 300;
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "checkout-spread", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_scratch);

    [Fact]
    public async Task A_tree_made_with_the_spread_checkout_holds_each_file_the_line_holds_with_its_content()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "-q", "-b", "main");
        await GitAsync(root, "config", "user.email", "test@example.com");
        await GitAsync(root, "config", "user.name", "Test");
        await GitAsync(root, "config", "core.autocrlf", "false");
        for (var each = 0; each < Files; each++)
        {
            var file = Path.Combine(root, $"unit{each / 20}", $"File{each % 20}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, $"content {each}\n");
        }

        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-q", "-m", "first");
        var tree = Path.Combine(_scratch, "tree");

        await GitAsync(root, [.. TreeOpenings.CheckoutSpread, "worktree", "add", "-q", "-b", "daoris/one", tree, "main"]);

        Assert.Equal(Files, Directory.EnumerateFiles(tree, "*.txt", SearchOption.AllDirectories).Count());
        Assert.Equal("content 137\n", await File.ReadAllTextAsync(Path.Combine(tree, "unit6", "File17.txt")));
        Assert.Equal("", (await RunWithAsync(tree, null, "", "status", "--porcelain")).Stdout);
    }
}
