using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A file <see cref="AtomicFile"/>'s writes keep, read beside its writer: the reads the home's own files make (the table's file, the home's
/// identity, the lease), declared where the home is so that Platform names nothing of Store (FOUNDATION1 mod-ii). <see cref="AtomicFile.ReadText"/>,
/// <see cref="AtomicFile.ReadBytes"/> and <see cref="AtomicFile.OpenRead"/> are these reads, spelled where every other reader of such a file already finds them.
/// </summary>
/// <remarks>
/// 🔴 Measured (BINDRACE1): Windows' move opens the file it moves for deletion and closes it only once the file is in place, so for a
/// moment the file is there, whole, and still held. A reader that does not share deletion, as <c>File.ReadAllText</c> does not, is
/// refused in that moment with a sharing violation: 478 reads of 700 that read a file as it appeared under load, and none of 700 that
/// shared deletion. A start that lost the race for its run's binding read the winner's as unread, and held.
/// </remarks>
public static class BesideReads
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The file read whole, sharing its writing and deletion with a writer.</summary>
    /// <exception cref="FileNotFoundException">No file is there.</exception>
    /// <exception cref="IOException">It could not be read.</exception>
    public static string Text(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Utf8);
        return reader.ReadToEnd();
    }

    /// <summary>The file read whole as bytes, sharing as <see cref="Text"/> does (READSHARE1).</summary>
    /// <exception cref="FileNotFoundException">No file is there.</exception>
    /// <exception cref="IOException">It could not be read.</exception>
    public static byte[] Bytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>The file opened to read as a stream, sharing as <see cref="Text"/> does (READSHARE1).</summary>
    /// <exception cref="FileNotFoundException">No file is there.</exception>
    /// <exception cref="IOException">It could not be opened.</exception>
    public static FileStream Open(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
