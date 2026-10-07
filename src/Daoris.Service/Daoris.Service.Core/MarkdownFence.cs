namespace Daoris.Knowledge;

/// <summary>
/// Which lines of a markdown file a fenced code block holds, read a line at a time as CommonMark reads a fence
/// (ORIENT2h3): its text is the fence's, never a heading, a table or a list.
/// </summary>
/// <remarks>
/// <para>A fence opens on a run of three or more backticks or tildes, and closes only on a run of the same character
/// at least as long with nothing after it but spaces. 🔴 Every reader used to toggle on any line opening with three
/// of either, so a four-backtick fence quoting a three-backtick example closed on the example's first fence, and the
/// heading and the table inside the example became entries of their own. A backtick run with a backtick after it on
/// its line is code inline, not a fence, as CommonMark has it. A fence never closed holds the rest of the file.</para>
///
/// <para>After any indent, where CommonMark allows three spaces: the readers do not read the list item a fence may
/// sit in, and a fence inside an item is indented by it. A line four spaces in that opens with backticks is read
/// as a fence, as every reader read it before.</para>
///
/// <para>Each reader keeps its own policy for what it does with a fenced line, its sections and its tables; only
/// where a fence starts and stops is shared, so a fence is one fence to every reader.</para>
/// </remarks>
internal sealed class MarkdownFence
{
    private char _marker;
    private int _length;

    /// <summary>
    /// Reads the next line: whether a fence holds it, its opening and closing lines included. Called once for every
    /// line of the file, in order, since what closes a fence depends on what opened it.
    /// </summary>
    public bool Holds(string line)
    {
        var text = line.TrimStart();
        var (marker, length) = Run(text);

        if (_marker == '\0')
        {
            if (length < 3) return false;
            if (marker == '`' && text.IndexOf('`', length) >= 0) return false;
            _marker = marker;
            _length = length;
            return true;
        }

        if (marker == _marker && length >= _length && text[length..].Trim().Length == 0) _marker = '\0';
        return true;
    }

    /// <summary>The backticks or tildes a line opens with, and how many; none for a line that opens with neither.</summary>
    private static (char Marker, int Length) Run(string text)
    {
        if (text.Length == 0 || text[0] is not ('`' or '~')) return ('\0', 0);
        var length = 1;
        while (length < text.Length && text[length] == text[0]) length++;
        return (text[0], length);
    }
}
