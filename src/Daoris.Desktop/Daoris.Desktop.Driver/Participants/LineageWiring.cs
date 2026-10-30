using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>Participants retains the actual watcher and its per-run state.</summary>
internal sealed class ParticipantLineageStreams : ILineageStreams
{
    [ModuleInitializer]
    internal static void Install() => LineageStreams.Current = new ParticipantLineageStreams();

    public ILineageWriter Writer(string home, string sessionId, string? proc = null) => ChildLineage.Writer(home, sessionId, proc);
}
