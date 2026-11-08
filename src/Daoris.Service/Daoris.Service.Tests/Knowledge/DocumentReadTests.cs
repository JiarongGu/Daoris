using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A document is read as the CLI reads it: CRLF becomes LF, and a character .NET calls a line break but no editor or git does stays.</summary>
public sealed class DocumentReadTests : IDisposable
{
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "document-read-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Only_CRLF_is_a_line_break_in_a_read_document()
    {
        Directory.CreateDirectory(_folder);
        var file = Path.Combine(_folder, "rule.md");
        File.WriteAllText(file, "# One\r\n\r\npasted\u2028here\u0085and\fthere\r\nlast\r\n");

        var (text, first) = Text.ReadDocumentAt(file);

        Assert.Equal("# One\n\npasted\u2028here\u0085and\fthere\nlast", text);
        Assert.Equal(1, first);
    }
}
