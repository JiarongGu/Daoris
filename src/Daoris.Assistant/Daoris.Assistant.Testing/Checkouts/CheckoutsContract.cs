using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="ICheckouts"/> passes: <see cref="MemoryCheckouts"/> in the assistant's suite, the desktop adapter over
/// the machine's checkouts and the service's exchange in its own.
/// </summary>
public abstract class CheckoutsContract
{
    private static readonly CancellationToken None = CancellationToken.None;

    /// <summary>The checkouts under test, holding <see cref="Checkout"/>.</summary>
    protected abstract ICheckouts Backend { get; }

    /// <summary>The name of a checkout the backend holds, empty of what the cases plant.</summary>
    protected abstract string Checkout { get; }

    /// <summary>Puts a file at <paramref name="path"/> in <see cref="Checkout"/>.</summary>
    protected abstract Task PlantAsync(string path, string text);

    [Fact]
    public async Task The_checkout_is_listed_with_where_it_is_by_name()
    {
        var listed = await Backend.ListAsync(None);

        Assert.Contains(listed, each => each.Name == Checkout && !string.IsNullOrWhiteSpace(each.Place));
        Assert.Equal(listed.Select(each => each.Name).Order(StringComparer.Ordinal), listed.Select(each => each.Name));
    }

    [Fact]
    public async Task A_file_there_reads_answered_with_its_text()
    {
        await PlantAsync("docs/areas/map.md", "The map is drawn in tiles.");

        var read = await Backend.ReadAsync(Checkout, "docs/areas/map.md", None);

        Assert.Equal((Checkout, "docs/areas/map.md", CheckoutReadState.Answered), (read.Checkout, read.Path, read.State));
        Assert.Equal("The map is drawn in tiles.", read.Text);
    }

    [Fact]
    public async Task A_file_not_there_reads_missing_and_an_unknown_checkout_unknown()
    {
        var missing = await Backend.ReadAsync(Checkout, "docs/none.md", None);
        var unknown = await Backend.ReadAsync("no-such-checkout", "README.md", None);

        Assert.Equal((CheckoutReadState.Missing, (string?)null), (missing.State, missing.Text));
        Assert.Equal((CheckoutReadState.UnknownCheckout, (string?)null), (unknown.State, unknown.Text));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("docs/../../outside.md")]
    [InlineData("/outside.md")]
    [InlineData("C:/outside.md")]
    [InlineData("")]
    public async Task A_path_that_is_blank_rooted_or_climbs_out_is_refused(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Backend.ReadAsync(Checkout, path, None));
    }
}
