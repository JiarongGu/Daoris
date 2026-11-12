using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Another Daoris home on this machine does not see this home's ledger, so a git place's first in-place start writes a marker in
/// its common git directory naming its home, and every other home's in-place start there is refused; a home that is gone gives it up.
/// A folder that is no git checkout takes none.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PlaceHomeMarkerTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("place-home");
    private string Place => Path.Combine(_scratch, "reports");
    private string First => Path.Combine(_scratch, "first-home");
    private string Second => Path.Combine(_scratch, "second-home");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    [Fact]
    public async Task A_second_home_is_refused_an_in_place_start_on_a_checkout_the_first_works_in()
    {
        Directory.CreateDirectory(Place);
        Directory.CreateDirectory(First);
        Directory.CreateDirectory(Second);
        await GitFixture.RunAsync(Place, "init", "-q", "-b", "main");

        Assert.Null(PlaceHomeMarker.Claim(First, Place, "reports"));
        Assert.Null(PlaceHomeMarker.Claim(First, Place, "reports"));

        var refused = PlaceHomeMarker.Claim(Second, Place, "reports");
        Assert.NotNull(refused);
        Assert.Contains("another Daoris home on this machine", refused);
        Assert.DoesNotContain(First, refused);

        // The first home gone, its marker holds nothing.
        Directory.Delete(First, recursive: true);
        Assert.Null(PlaceHomeMarker.Claim(Second, Place, "reports"));
        Assert.NotNull(PlaceHomeMarker.Claim(First, Place, "reports"));
    }

    [Fact]
    public void A_folder_that_is_no_git_checkout_takes_no_marker()
    {
        Directory.CreateDirectory(Place);

        Assert.Null(PlaceHomeMarker.Claim(First, Place, "reports"));
        Assert.Null(PlaceHomeMarker.Claim(Second, Place, "reports"));
    }

    [Fact]
    public async Task A_conversation_in_the_checkout_is_asked_the_marker_and_one_in_a_tree_of_its_own_is_not()
    {
        Directory.CreateDirectory(Place);
        Directory.CreateDirectory(First);
        Directory.CreateDirectory(Second);
        await GitFixture.RunAsync(Place, "init", "-q", "-b", "main");
        Assert.Null(ChatStarts.PlaceHeld(First, Place, "reports", isolated: false, tree: null));

        var refused = ChatStarts.PlaceHeld(Second, Place, "reports", isolated: false, tree: null);

        Assert.Contains("another Daoris home on this machine", refused);
        Assert.Null(ChatStarts.PlaceHeld(Second, Place, "reports", isolated: true, tree: null));
        Assert.Null(ChatStarts.PlaceHeld(Second, Place, "reports", isolated: false, tree: Path.Combine(_scratch, "tree")));
    }
}

