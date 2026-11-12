using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landed file previewed in a partial clone, on real git: the git in use proved to honour <c>GIT_NO_LAZY_FETCH</c>, the filtered
/// blob answered missing inside an ordinary place and asked once more as a start allowed to fetch, its size the object's header's (a
/// listing with sizes prints <c>BAD</c> there and exits 0), and said unfetched, never not on the branch, once its promisor is gone.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PartialClonePreviewTests : LandedFixture
{
    [Fact]
    public async Task A_filtered_blob_is_read_from_its_header_once_fetched_and_said_unfetched_when_its_promisor_is_gone()
    {
        var source = await RepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(source, "other.txt"), "left behind\n");
        await GitAsync(source, "add", "other.txt");
        await GitAsync(source, "commit", "-q", "-m", "other");
        await GitAsync(source, "config", "uploadpack.allowFilter", "true");
        var clone = Path.Combine(Scratch, "clone");
        await GitAsync(Scratch, "clone", "-q", "--filter=blob:none", "--no-checkout", new Uri(source).AbsoluteUri, clone);
        await GitAsync(clone, "branch", "landed", "origin/main");
        var tip = (await GitAsync(clone, "rev-parse", "landed")).Trim();
        var entry = new LandedBranch(clone, "engine", "landed", "main", tip, "s1a2b3c4", null, null, DateTimeOffset.UtcNow);

        Assert.True(await GitLazyFetch.HonouredAsync("git", clone, CancellationToken.None));
        var read = await FilePreview.ReadLandedAsync(clone, entry, "shared.txt");

        Assert.Equal(FilePreviewRefusal.None, read.Refusal);
        Assert.Equal("one\n", read.File!.Text);
        Assert.Equal(4L, read.File.Size);

        Directory.Move(source, source + "-gone");
        var gone = await FilePreview.ReadLandedAsync(clone, entry, "other.txt");

        Assert.Equal(FilePreviewRefusal.Unfetched, gone.Refusal);
    }
}
