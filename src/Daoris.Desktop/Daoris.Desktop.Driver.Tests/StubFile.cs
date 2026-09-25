namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Reading a file a stub agent may still be writing — what every test that polls a stub's "heard"
/// file does.
/// </summary>
/// <remarks>
/// <c>File.ReadAllText</c> and <c>File.ReadAllLines</c> open with <c>FileShare.Read</c>, which Windows
/// refuses while another process holds the file open to write. A poll that landed inside one of the
/// stub's appends threw a sharing violation and failed a passing test (REV3; <c>StubFileTests</c>).
/// Opened here sharing read and write, as the writer does.
/// </remarks>
internal static class StubFile
{
    /// <summary>The file's text, or empty when nothing has written it yet.</summary>
    public static string Text(string path)
    {
        if (!File.Exists(path)) return "";
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The file's lines, split as <c>File.ReadAllLines</c> splits them.</summary>
    public static string[] Lines(string path)
    {
        var lines = new List<string>();
        using var reader = new StringReader(Text(path));
        while (reader.ReadLine() is { } line) lines.Add(line);
        return [.. lines];
    }
}
