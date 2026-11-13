using Daoris.Ports.Processes;
using Xunit;

namespace Daoris.Testing;

/// <summary>
/// The cases every <see cref="SecretSeal"/> must pass, written once: the scripted seal and the machine's seal each hand themselves and
/// a value another user sealed, so what a unit test keeps sealed is kept as the machine keeps it.
/// </summary>
public abstract class SealContract
{
    private const string Secret = "sk-seal-contract-0000-wxyz";

    /// <summary>The seal under test.</summary>
    protected abstract SecretSeal Backend { get; }

    /// <summary>A value sealed by someone this backend cannot open for.</summary>
    protected abstract string Foreign { get; }

    [Fact]
    public void A_sealed_secret_holds_none_of_it_and_opens_whole()
    {
        var kept = Backend.Seal(Secret);

        Assert.NotNull(kept);
        Assert.DoesNotContain("wxyz", kept);
        Assert.Equal(Secret, Backend.Open(kept));
    }

    [Fact]
    public void A_value_already_sealed_is_kept_as_it_is()
    {
        var kept = Backend.Seal(Secret)!;

        Assert.Equal(kept, Backend.Seal(kept));
    }

    [Fact]
    public void A_value_kept_before_any_seal_opens_as_written()
    {
        Assert.Equal("sk-legacy-1111", Backend.Open("sk-legacy-1111"));
    }

    [Fact]
    public void A_value_another_user_sealed_opens_nothing()
    {
        Assert.Null(Backend.Open(Foreign));
    }
}
