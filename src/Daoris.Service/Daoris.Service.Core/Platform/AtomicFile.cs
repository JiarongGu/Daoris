using System.Text;

namespace Daoris.Knowledge;

/// <summary>
/// The one way the service core writes a file that something else may be reading: beside, then renamed, so a reader never
/// meets half of it (DEDUP9a).
/// </summary>
/// <remarks>
/// <para><b>The beside file is named per write, and the rename waits out a busy target.</b> Four writers here each wrote
/// beside by hand, three under a fixed <c>.tmp</c> name and one without a retry or a cleanup. Two keeps of one attachment
/// then shared the name: one's write met the other's open file, or its move found the file already moved. Even with names of
/// their own, two renames over one file at once are refused on Windows, so the rename is tried again, briefly and a bounded
/// number of times. A missing folder or file is not busy and throws at once.</para>
///
/// <para>It does not make the folder, and it does not fix line endings: the caller owns both. Text is written as BOM-less
/// UTF-8. Reflection-free, like <see cref="JsonFields"/>: nothing may stop working under AOT. The driver keeps a helper of
/// its own; the two projects do not reference each other (D166).</para>
/// </remarks>
internal static class AtomicFile
{
    private const int Tries = 40;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Write <paramref name="text"/> to <paramref name="path"/> whole, or not at all; an existing file is replaced.</summary>
    public static void WriteText(string path, string text) =>
        Write(path, beside => File.WriteAllText(beside, text, Utf8));

    /// <summary>Write <paramref name="bytes"/> to <paramref name="path"/> whole, or not at all; an existing file is replaced.</summary>
    public static void WriteBytes(string path, byte[] bytes) =>
        Write(path, beside => File.WriteAllBytes(beside, bytes));

    /// <summary>As <see cref="WriteBytes"/>, without holding a thread through the write or the wait.</summary>
    public static async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken ct = default)
    {
        var beside = BesideOf(path);
        try
        {
            await File.WriteAllBytesAsync(beside, bytes, ct).ConfigureAwait(false);
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(beside, path, overwrite: true);
                    return;
                }
                catch (Exception busy) when (attempt < Tries && IsBusy(busy))
                {
                    await Task.Delay(5 + attempt * 5, ct).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            Discard(beside);
        }
    }

    private static string BesideOf(string path) => $"{path}.{Guid.NewGuid():N}.writing";

    private static bool IsBusy(Exception exception) =>
        exception is UnauthorizedAccessException
        || exception is IOException and not FileNotFoundException and not DirectoryNotFoundException;

    private static void Write(string path, Action<string> write)
    {
        var beside = BesideOf(path);
        try
        {
            write(beside);
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    // Measured in the driver's twin (REV3): a rename over a file another rename is replacing is refused
                    // with "access denied" on Windows, and clears in milliseconds.
                    File.Move(beside, path, overwrite: true);
                    return;
                }
                catch (Exception busy) when (attempt < Tries && IsBusy(busy))
                {
                    Thread.Sleep(5 + attempt * 5);
                }
            }
        }
        finally
        {
            Discard(beside);
        }
    }

    // Only a write that failed leaves one behind, and leaves it for nobody.
    private static void Discard(string beside)
    {
        if (!File.Exists(beside)) return;
        try { File.Delete(beside); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
