using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The one way this library writes a file that something else may be reading: beside, then renamed,
/// so a reader never meets half of it; and the way such a file is read beside its writer.
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
///
/// <para>A file these writes keep is read beside its writer through <see cref="ReadText"/> (BINDRACE1) or <see cref="ReadBytes"/>, never
/// plainly: <c>tools/read-shares.mjs</c> finds a plain read of one (READSHARE1).</para>
/// </remarks>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>A file these writes keep, read whole, sharing its writing and deletion with a writer.</summary>
    /// <remarks>
    /// 🔴 Measured (BINDRACE1): Windows' move opens the file it moves for deletion and closes it only once the file is in place, so for a
    /// moment the file is there, whole, and still held. A reader that does not share deletion, as <c>File.ReadAllText</c> does not, is
    /// refused in that moment with a sharing violation: 478 reads of 700 that read a file as it appeared under load, and none of 700 that
    /// shared deletion. A start that lost the race for its run's binding read the winner's as unread, and held.
    /// </remarks>
    /// <exception cref="FileNotFoundException">No file is there.</exception>
    /// <exception cref="IOException">It could not be read.</exception>
    public static string ReadText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Utf8);
        return reader.ReadToEnd();
    }

    /// <summary>A file these writes keep, read whole as bytes, sharing as <see cref="ReadText"/> does (READSHARE1).</summary>
    /// <exception cref="FileNotFoundException">No file is there.</exception>
    /// <exception cref="IOException">It could not be read.</exception>
    public static byte[] ReadBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>A file these writes keep, opened to read as a stream, sharing as <see cref="ReadText"/> does (READSHARE1).</summary>
    /// <exception cref="FileNotFoundException">No file is there.</exception>
    /// <exception cref="IOException">It could not be opened.</exception>
    public static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>Write <paramref name="text"/> to <paramref name="path"/> whole, or not at all.</summary>
    public static void WriteText(string path, string text) =>
        Write(path, beside => File.WriteAllText(beside, text, Utf8));

    /// <summary>Write <paramref name="bytes"/> to <paramref name="path"/> whole, or not at all.</summary>
    public static void WriteBytes(string path, byte[] bytes) =>
        Write(path, beside => File.WriteAllBytes(beside, bytes));

    /// <summary>
    /// Write <paramref name="text"/> to <paramref name="path"/> whole where no file is there, never over one (FABRIC-CUTOVER1g): written
    /// beside, then moved into place without overwriting, so of writers racing for one path exactly one lands and each other is told so.
    /// </summary>
    /// <returns>Whether this write landed: false where a file was there already, or another writer's landed first.</returns>
    /// <exception cref="IOException">The write failed and no file is there: not another's, so nothing to read instead.</exception>
    public static bool TryCreateText(string path, string text)
    {
        if (File.Exists(path)) return false;
        var beside = $"{path}.{Guid.NewGuid():N}.writing";
        try
        {
            File.WriteAllText(beside, text, Utf8);
            File.Move(beside, path, overwrite: false);
            return true;
        }
        catch (Exception error) when ((error is IOException or UnauthorizedAccessException) && File.Exists(path))
        {
            // Almost always: another writer's file landed first, and it is the one kept.
            return false;
        }
        finally
        {
            if (File.Exists(beside))
            {
                try { File.Delete(beside); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>
    /// A folder moved into place, tried again while something holds a file in it (FIX-LOG 2026-10-07): on Windows a folder
    /// holding an executable written a moment ago cannot move while the virus scanner has it open, and that gives way within
    /// seconds. A missing folder is not held, and throws at once; the last refusal throws after the tries.
    /// </summary>
    /// <param name="move">The move itself; a test hands in one that refuses.</param>
    public static void MoveFolder(string from, string to, int tries = 50, int waitMs = 200, Action<string, string>? move = null)
    {
        move ??= Directory.Move;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                move(from, to);
                return;
            }
            catch (Exception held) when (attempt < tries
                                         && (held is UnauthorizedAccessException
                                             || held is IOException and not FileNotFoundException and not DirectoryNotFoundException))
            {
                Thread.Sleep(waitMs);
            }
        }
    }

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
