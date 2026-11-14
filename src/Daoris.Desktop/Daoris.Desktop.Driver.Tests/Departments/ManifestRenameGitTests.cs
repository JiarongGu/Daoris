using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A place's manifest where git's own rename listing is the point: a staged rename names the old path beside the new.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ManifestRenameGitTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("manifest-rename");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    [Fact]
    public async Task A_renamed_path_lists_the_old_name_as_gone_beside_the_new()
    {
        var root = Path.Combine(_scratch, "reports");
        Directory.CreateDirectory(root);
        await GitFixture.RunAsync(root, "init", "-q", "-b", "main");
        File.WriteAllText(Path.Combine(root, "notes.md"), "first\n");
        await GitFixture.RunAsync(root, "add", "-A");
        await GitFixture.RunAsync(root, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "one");
        var store = new ManifestStore(Path.Combine(_scratch, "home"), "default", "reports");
        await GitFixture.RunAsync(root, "mv", "notes.md", "renamed.md");

        var manifest = await ManifestReader.OfPlaceAsync(root, store.Content, CancellationToken.None);

        Assert.Null(manifest.Listed("notes.md")!.Sha256);
        Assert.NotNull(manifest.Listed("renamed.md")!.Sha256);
    }
}
