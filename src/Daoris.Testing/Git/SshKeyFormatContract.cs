using Daoris.Ports.Git;
using Xunit;

namespace Daoris.Testing;

public abstract class SshKeyFormatContract
{
    protected abstract SshKeyConverter Convert { get; }

    [Theory]
    [InlineData("")]
    [InlineData("not a key")]
    public void An_invalid_key_refuses_with_a_code_and_no_passphrase(string source)
    {
        var error = Assert.Throws<SshKeyFormatException>(() => Convert(System.Text.Encoding.UTF8.GetBytes(source), "Work", "private passphrase"));
        Assert.Equal("import-format", error.Code);
        Assert.DoesNotContain("private passphrase", error.ToString());
    }
}
