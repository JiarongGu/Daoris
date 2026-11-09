using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The order dependencies install in when the found layer names an npm workspace's root and its members in one entry and a later layer
/// decides the root alone: the root installs first, so a member's install never meets a tree without it. Over real git; the scratch
/// repository lives under the gitignored <c>_fixtures/</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class WorkspaceOrderTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workspace-order", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose() => GitSeed.Discard(_scratch);

    private async Task<string> MakeAsync(string rules)
    {
        Directory.CreateDirectory(Path.Combine(Root, "pkgs", "a"));
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "node_modules/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "package.json"), """{ "name": "engine", "version": "1.0.0", "workspaces": ["pkgs/a"] }""");
        await File.WriteAllTextAsync(Path.Combine(Root, "package-lock.json"), """{ "name": "engine", "lockfileVersion": 3, "packages": {} }""");
        await File.WriteAllTextAsync(Path.Combine(Root, "pkgs", "a", "package.json"), """{ "name": "a", "version": "1.0.0" }""");
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.files.json"), rules);
        await GitAsync(Root, "add", ".");
        await GitAsync(Root, "commit", "-q", "-m", "an npm workspace");
        return (await WorkingTree.HeadAsync(Root))!;
    }

    private async Task<IReadOnlyList<string>> OrderAsync(string head) =>
        [.. (await StoreDependencyRules.ReadAsync(Home, Root, "engine", head, CancellationToken.None)).Each.Select(each => each.Path)];

    [Fact]
    public async Task A_repository_entry_deciding_the_root_alone_installs_it_before_the_members_the_found_entry_named_with_it()
    {
        var head = await MakeAsync("""{ "form": 2, "rules": [ { "kind": "dependency", "paths": ["/node_modules/"], "install": "npm ci" } ] }""");

        Assert.Equal(["node_modules/", "pkgs/a/node_modules/"], await OrderAsync(head));
    }

    [Fact]
    public async Task A_member_a_later_entry_decides_itself_keeps_that_entry_s_place()
    {
        var head = await MakeAsync("""
            { "form": 2, "rules": [
              { "kind": "dependency", "paths": ["/node_modules/"], "install": "npm ci" },
              { "kind": "dependency", "paths": ["/pkgs/a/node_modules/"], "install": "npm ci --prefix pkgs/a", "key": ["package-lock.json"] },
              { "kind": "dependency", "paths": ["/node_modules/"], "install": "npm ci --again" } ] }
            """);

        Assert.Equal(["pkgs/a/node_modules/", "node_modules/"], await OrderAsync(head));
    }
}
