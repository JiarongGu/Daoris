using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's answer to where the <see cref="ChangeTopics.Help"/> topic's files are: its proposals
/// folder, whose records' state the watch reads. Installed when the driver's assembly loads, before any watch is built.
/// </summary>
internal static class HelpHomeTopic
{
    [ModuleInitializer]
    internal static void Install() => HomeTopics.Answer(ChangeTopics.Help, home => [HomePart.Named(HelpProposals.FolderOf(home))]);
}
