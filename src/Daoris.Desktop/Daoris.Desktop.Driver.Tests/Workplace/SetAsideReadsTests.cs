using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The set-aside walk's nested repositories: a folder holding a <c>.git</c>, and floor 2's bare repository, a folder holding a
/// <c>HEAD</c> file and <c>objects/</c> and <c>refs/</c> folders, whatever git answers of it, none read into.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SetAsideReadsTests : IDisposable
{
    private readonly string _tree = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "set-aside-reads", Guid.NewGuid().ToString("N")[..8]);

    public SetAsideReadsTests() => Directory.CreateDirectory(_tree);

    public void Dispose() => GitSeed.Discard(_tree);

    private string Folder(string relative) => Directory.CreateDirectory(Path.Combine(_tree, relative)).FullName;

    private void Write(string relative, string content = "x")
    {
        var path = Path.Combine(_tree, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static async Task Git(string folder, params string[] arguments)
    {
        var (code, _, stderr) = await WorkingTree.GitAsync(folder, arguments, CancellationToken.None);
        Assert.True(code == 0, $"git {string.Join(' ', arguments)}: {stderr}");
    }

    [Fact]
    public async Task A_bare_repository_is_a_nested_repository_beside_a_git_folder_and_a_gitfile_and_is_not_read_into()
    {
        Folder("cache");
        await Git(_tree, "init", "-q", "--bare", "cache/mirror.git");
        Write("cache/mirror.git/refs/heads/unpushed", "0000000000000000000000000000000000000000\n");
        Write("cache/mirror.git/objects/pack/inner/.git", "gitdir: below\n");
        await Git(_tree, "init", "-q", "work/clone");
        Write("vendor/lib/.git", "gitdir: elsewhere\n");
        Write("plain/notes.txt");

        var (nested, _) = await SetAsideReads.NestedRepositoriesAsync(_tree, CancellationToken.None);

        Assert.Equal(["cache/mirror.git", "vendor/lib", "work/clone"], nested.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_folder_with_a_bare_shape_that_git_cannot_answer_for_is_still_a_nested_repository()
    {
        Write("odd/HEAD", "not a ref\n");
        Folder("odd/objects");
        Folder("odd/refs");

        var (nested, _) = await SetAsideReads.NestedRepositoriesAsync(_tree, CancellationToken.None);

        Assert.Equal(["odd"], nested);
    }

    [Fact]
    public async Task A_folder_missing_a_part_of_the_bare_shape_and_the_tree_s_own_top_are_not_nested_repositories()
    {
        Write("HEAD", "ref: refs/heads/main\n");
        Folder("objects");
        Folder("refs");
        Write("half/HEAD", "ref: refs/heads/main\n");
        Folder("half/objects");

        var (nested, _) = await SetAsideReads.NestedRepositoriesAsync(_tree, CancellationToken.None);

        Assert.Empty(nested);
    }
}
