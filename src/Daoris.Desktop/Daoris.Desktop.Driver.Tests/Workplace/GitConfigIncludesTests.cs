using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The includes a configuration file names are read as git reads the file, whose first bytes may be a UTF-8 byte-order mark.</summary>
public sealed class GitConfigIncludesTests
{
    [Fact]
    public void An_include_opening_a_file_that_begins_with_a_byte_order_mark_is_named()
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble().Concat("[include]\r\n\tpath = extra.inc\r\n"u8.ToArray()).ToArray();

        Assert.Equal([new GitConfigInclude("extra.inc", null)], GitConfigIncludes.Parse(bytes));
    }
}
