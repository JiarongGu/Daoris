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

    /// <summary>The start of the standing block a conversation's first prompt ends with (CONTEXTONE1c), its routing standing among it.</summary>
    public const string BlockLead = "Daoris's standing block for this session: ";

    /// <summary>
    /// A prompt as a stand-in logged it, without the standing block Daoris appends after the person's words and its own lines: the
    /// person's words and Daoris's other lines, exactly. A prompt carrying no block is returned as it was.
    /// </summary>
    public static string WithoutBlock(string prompt)
    {
        var at = prompt.IndexOf(BlockLead, StringComparison.Ordinal);
        if (at < 0) return prompt;
        var before = prompt[..at];
        foreach (var joint in new[] { " | ", "\\n\\n", "\n\n" })
        {
            if (before.EndsWith(joint, StringComparison.Ordinal)) return before[..^joint.Length];
        }

        return before;
    }

    /// <summary>
    /// The standings (<c>not driven</c>, <c>open</c>, ...) a stand-in set apart from the prompts it logged, one per prompt whose standing
    /// block carried the routing, in the order heard: its <c>&lt;heard&gt;.routing</c> file.
    /// </summary>
    public static string[] Routings(string heardPath) => Lines(heardPath + ".routing");

    /// <summary>The file's lines, split as <c>File.ReadAllLines</c> splits them.</summary>
    public static string[] Lines(string path)
    {
        var lines = new List<string>();
        using var reader = new StringReader(Text(path));
        while (reader.ReadLine() is { } line) lines.Add(line);
        return [.. lines];
    }
}
