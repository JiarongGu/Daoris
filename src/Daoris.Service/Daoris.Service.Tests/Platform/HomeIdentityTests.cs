using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A home's identity (HOMEID1, the terminal RPC design §3.1): minted once as a new file only, read alike by every path to the
/// folder, never changed in place, and a file that does not read said and left as it is. The driver's <c>HomeIdentityTests</c>
/// hold the same rows for its twin.
/// </summary>
public sealed class HomeIdentityTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-home-id-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_home_with_none_is_minted_one_once()
    {
        Assert.Null(HomeIdentity.Read(_home, out var none));
        Assert.Null(none);

        var id = HomeIdentity.Ensure(_home, out var unread);

        Assert.Null(unread);
        Assert.True(HomeIdentity.IsId(id), id);
        Assert.Equal(id, HomeIdentity.Ensure(_home, out _));
        Assert.Equal(id, HomeIdentity.Read(_home, out _));
        Assert.Equal([HomeIdentity.FileName], Directory.GetFiles(_home).Select(Path.GetFileName));
        var text = File.ReadAllText(HomeIdentity.PathIn(_home));
        Assert.DoesNotContain('\r', text);
        Assert.Contains("\"minted\"", text);
    }

    /// <summary>Of many minting at once, one file stands and every one of them reads it.</summary>
    [Fact]
    public async Task Of_two_minting_at_once_one_stands_and_both_read_it()
    {
        var ids = await Task.WhenAll(Enumerable.Range(0, 8).Select(n => Task.Run(() => HomeIdentity.Ensure(_home, out _))));

        Assert.Single(ids.Distinct());
        Assert.Equal(ids[0], HomeIdentity.Read(_home, out _));
    }

    /// <summary>Another spelling of the same folder reads the same id: the id is the folder's, never a path.</summary>
    [Fact]
    public void Another_path_to_the_folder_reads_the_same_id()
    {
        var id = HomeIdentity.Ensure(_home, out _);
        Directory.CreateDirectory(Path.Combine(_home, "sub"));

        Assert.Equal(id, HomeIdentity.Read(Path.Combine(_home, "sub", ".."), out _));
        if (OperatingSystem.IsWindows()) Assert.Equal(id, HomeIdentity.Read(_home.ToUpperInvariant(), out _));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{ \"id\": \"0123\" }")]
    [InlineData("{ \"id\": \"0123456789ABCDEF0123456789ABCDEF\" }")]
    public void A_file_that_does_not_read_is_said_and_never_replaced(string text)
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(HomeIdentity.PathIn(_home), text);

        Assert.Null(HomeIdentity.Ensure(_home, out var unread));

        Assert.Contains(HomeIdentity.PathIn(_home), unread);
        Assert.Contains("left as it is", unread);
        Assert.Equal(text, File.ReadAllText(HomeIdentity.PathIn(_home)));
    }
}
