using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>An object's bytes read as text the way git's text output is: UTF-8, its own byte-order mark dropped and no other taken as one.</summary>
public sealed class GitObjectTextTests
{
    [Fact]
    public void A_text_read_takes_a_utf_8_mark_and_no_other_as_a_mark()
    {
        byte[] utf8 = [0xEF, 0xBB, 0xBF, (byte)'a'];
        byte[] utf16 = [0xFF, 0xFE, (byte)'a', 0];

        Assert.Equal("a", GitObjectReads.Text(utf8, new GitObjectAnswer(false, "blob", 4, 0, 4)));
        Assert.Equal("��a\0", GitObjectReads.Text(utf16, new GitObjectAnswer(false, "blob", 4, 0, 4)));
    }
}
