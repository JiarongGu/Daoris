using System.Runtime.CompilerServices;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The page's <c>SESSION_ENDED</c> told once (FOUNDATION1 l-ii, the foundation design §4.2): a conversation's end, which this process sees by
/// two paths, the route that ran the conversation, at once, and the journal, which also tells the ends another process drove.
/// </summary>
/// <remarks>
/// Each end is sighted at most once by each path, so whichever sights it second finds the first's mark and tells nothing; an end the route
/// alone ran is told at once and an end only the journal sees is told when it is read. A mark that no second sight claims within
/// <see cref="Window"/> is dropped, so another process's end never silences a later one of this process's.
/// </remarks>
public sealed class ConversationEnds
{
    /// <summary>How long a path's sight waits for the other's.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    private static readonly ConditionalWeakTable<IEventBus, ConversationEnds> Of = new();

    private readonly Lock _gate = new();
    private readonly List<(string Key, bool Journal, long At)> _marks = [];

    /// <summary>The route's end of a conversation, as <see cref="DriverLoop.Ended"/> tells it.</summary>
    public static Task FromRouteAsync(IEventBus bus, string session, string state) =>
        Of.GetValue(bus, _ => new ConversationEnds()).Sight(session, state, journal: false) ? Tell(bus, session, state) : Task.CompletedTask;

    /// <summary>The journal's end of a conversation.</summary>
    public static Task FromJournalAsync(IEventBus bus, string session, string state) =>
        Of.GetValue(bus, _ => new ConversationEnds()).Sight(session, state, journal: true) ? Tell(bus, session, state) : Task.CompletedTask;

    private static Task Tell(IEventBus bus, string session, string state) =>
        bus.EmitAsync("DAORIS", DriverLoop.EndedType, new { Session = session, State = state });

    /// <summary>Whether this is the first sight of the end: false where the other path's mark of it was waiting.</summary>
    private bool Sight(string session, string state, bool journal)
    {
        var now = Environment.TickCount64;
        var key = session + "\n" + state;
        lock (_gate)
        {
            _marks.RemoveAll(mark => now - mark.At > Window.TotalMilliseconds);
            var other = _marks.FindIndex(mark => mark.Key == key && mark.Journal != journal);
            if (other >= 0)
            {
                _marks.RemoveAt(other);
                return false;
            }

            _marks.Add((key, journal, now));
            return true;
        }
    }
}
