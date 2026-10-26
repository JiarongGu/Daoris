using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The workplace's answer to where the <see cref="ChangeTopics.Words"/> topic's files are (FOUNDATION1 mod-vii, the design §14.3): the held
/// words' file under the sessions folder, which another process or a clear may change, its wake naming it by its file name. Installed when
/// the driver's assembly loads, before any watch is built.
/// </summary>
internal static class WordsHomeTopic
{
    [ModuleInitializer]
    internal static void Install() => HomeTopics.Answer(
        ChangeTopics.Words, home => [HomePart.File(HeldWordsFile.PathOf(home))], subject: HeldWordsFile.FileName);
}
