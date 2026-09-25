using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The one way this library writes a file that something else may be reading: beside, then renamed,
/// so a reader never meets half of it.
/// </summary>
/// <remarks>
/// <para><b>The beside file is named per write, and the rename waits out a busy target.</b> Fifteen
/// writers here each wrote beside under a fixed name (<c>path + ".tmp"</c>, <c>".writing"</c>). Two
/// writers of one file at once — two sessions installing the tree guard in one tick, two drivers on
/// one home recording usage — then shared that name: one's write met the other's open file, or its
/// move found the file already moved. Even with names of their own, two renames over one file at
/// once are refused on Windows. Either threw out of a session that had finished its work, and turned
/// it <c>failed</c> (REV3).</para>
///
/// <para>Text is written as BOM-less UTF-8, and what it says (LF, a final newline) is the caller's.</para>
/// </remarks>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Write <paramref name="text"/> to <paramref name="path"/> whole, or not at all.</summary>
    public static void WriteText(string path, string text) =>
        Write(path, beside => File.WriteAllText(beside, text, Utf8));

    /// <summary>Write <paramref name="bytes"/> to <paramref name="path"/> whole, or not at all.</summary>
    public static void WriteBytes(string path, byte[] bytes) =>
        Write(path, beside => File.WriteAllBytes(beside, bytes));

    private static void Write(string path, Action<string> write)
    {
        var beside = $"{path}.{Guid.NewGuid():N}.writing";
        try
        {
            write(beside);
            Replace(beside, path);
        }
        finally
        {
            // Only a write that failed leaves one behind — and leaves it for nobody.
            if (File.Exists(beside))
            {
                try { File.Delete(beside); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>
    /// The rename, retried while the target is busy — briefly, and a bounded number of times.
    /// </summary>
    /// <remarks>
    /// 🔴 Measured (REV3): on Windows a rename over a file that another rename is replacing at that
    /// moment is refused with <i>access denied</i>, and so is one over a file a reader holds open
    /// without delete sharing. Both clear in milliseconds. A missing file is not busy, and is not
    /// retried.
    /// </remarks>
    private static void Replace(string beside, string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(beside, path, overwrite: true);
                return;
            }
            catch (Exception busy) when (attempt < 40
                                          && (busy is UnauthorizedAccessException
                                              || busy is IOException and not FileNotFoundException and not DirectoryNotFoundException))
            {
                Thread.Sleep(5 + attempt * 5);
            }
        }
    }
}
