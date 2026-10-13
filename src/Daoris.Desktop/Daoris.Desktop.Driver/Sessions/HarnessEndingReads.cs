namespace Daoris.Driver;

/// <summary>
/// The read a <see cref="HarnessEnding"/> takes from a transcript file (AGT3c).
/// </summary>
/// <remarks>
/// <b>Named where its callers always named it</b> (STRUCT3g-i, D166's note): <c>HarnessEnding.Transcript</c> is a static member
/// of a C# 14 extension block on the Domain's <see cref="HarnessEnding"/>, so the record opens no file.
/// </remarks>
public static class HarnessEndingReads
{
    extension(HarnessEnding)
    {
        /// <summary>
        /// A door that carries only text (AGT3c): its transcript's last lines, both its streams, beside the process's exit. Its
        /// agent's words are among them and cannot be told apart, so only an exit that was not 0 lets them be read. Unreadable
        /// is none.
        /// </summary>
        public static HarnessEnding Transcript(int? exit, string transcript)
        {
            try
            {
                return new(exit, null, [.. File.ReadLines(transcript).TakeLast(HarnessEnding.LinesKept)]);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return new(exit, null, []);
            }
        }
    }
}
