using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The ends this process read from the service's journal, told on its bus (FOUNDATION1 l-i, the foundation design §4.2): <c>part.ended</c> as
/// the shell's one event for an end, whoever drove it, so a slice that wakes on an end hears a driven one at once (<see cref="DriverLoop.PartEndedType"/>).
/// </summary>
public static class PartEndRelay
{
    /// <summary>Every end in <paramref name="batch"/>, in the journal's order, as <see cref="DriverLoop.PartEndedType"/>.</summary>
    public static async Task TellAsync(IEventBus bus, JournalBatch batch)
    {
        foreach (var row in batch.Rows)
        {
            if (PartEnds.Judge(row, batch.Store) is not { } ended) continue;
            await bus.EmitAsync("DAORIS", DriverLoop.PartEndedType, new PartEndedTold(
                ended.Session, ended.State, ended.Reason, ended.Class, PartEnds.Causes[ended.Class], ended.Position, ended.Store)).ConfigureAwait(false);
            if (EndReaders.Takes(EndReader.SessionEnded, ended)) await ConversationEnds.FromJournalAsync(bus, ended.Session, ended.State).ConfigureAwait(false);
        }
    }
}

/// <summary>The payload of <see cref="DriverLoop.PartEndedType"/>: ids, codes and the occurrence's key, never a sentence.</summary>
public sealed record PartEndedTold(string Session, string State, string? Reason, string Class, string Cause, long Position, string Store);
