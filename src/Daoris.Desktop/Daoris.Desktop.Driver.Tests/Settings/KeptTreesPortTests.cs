using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Settings' port to the kept slots: unset it throws, and a test that sets another puts it back.</summary>
public sealed class KeptTreesPortTests
{
    [Fact]
    public void An_unset_port_throws_rather_than_answering_nothing()
    {
        var before = KeptTreesPort.IsSet ? KeptTreesPort.Current : null;
        try
        {
            KeptTreesPort.Current = null!;

            Assert.False(KeptTreesPort.IsSet);
            Assert.Throws<InvalidOperationException>(() => KeptTreesPort.Current);
        }
        finally
        {
            KeptTreesPort.Current = before!;
        }
    }

    [Fact]
    public void A_port_a_test_sets_answers_and_is_put_back()
    {
        var before = KeptTreesPort.IsSet ? KeptTreesPort.Current : null;
        try
        {
            KeptTreesPort.Current = new Answer();

            Assert.Equal(2, KeptTreesPort.Current.Retiring("w", "engine", 1));
            Assert.Equal("a submodule", KeptTreesPort.Current.Withheld("w", "engine"));
        }
        finally
        {
            KeptTreesPort.Current = before!;
        }
    }

    private sealed class Answer : IKeptTrees
    {
        public int Retiring(string workspace, string repository, int count) => 2;

        public string? Withheld(string workspace, string repository) => "a submodule";
    }
}
