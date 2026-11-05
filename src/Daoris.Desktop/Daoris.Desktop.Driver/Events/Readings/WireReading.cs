using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Who says a reading is so (the agent-provider design §3.1): the agent on its wire, the transport, or Daoris reading either.</summary>
public enum ReadingBy
{
    /// <summary>The agent said it on its wire.</summary>
    Reported,

    /// <summary>The transport saw it: a process's line, its exit.</summary>
    Observed,

    /// <summary>Counted from what was reported or observed.</summary>
    Computed,

    /// <summary>Concluded without anyone saying it: a child closed because its parent ended.</summary>
    Inferred,
}

/// <summary>
/// What a run's door said happened, as one closed set of readings beside <see cref="SessionEvent"/>, which <see cref="Said"/> carries
/// (the agent-provider design §3.1, row l). A transport reads its wire and writes each reading to its run's <see cref="IReadingSink"/>, composed
/// where the run's executor is built of its owners' parts: Context keeps the event file and the console, Participants the binding's
/// conversation, Agents the account's windows. Every payload is Events' own or lower, so a reading names no Agents type; the provider mints
/// nothing, and what it does not say is never a reading.
/// </summary>
/// <remarks>
/// Closed: only the records declared here are readings. Each says who said it (<see cref="By"/>) and whether a resume's history replayed it
/// (<see cref="Replayed"/>), so a replay is never new activity.
/// </remarks>
public abstract record WireReading
{
    private WireReading()
    {
    }

    /// <summary>Who says it.</summary>
    public ReadingBy By { get; init; } = ReadingBy.Reported;

    /// <summary>Whether a resume's history replayed it rather than the run saying it now.</summary>
    public bool Replayed { get; init; }

    /// <summary>The session the run says it for; null before its run stamped it.</summary>
    public string? Run { get; init; }

    /// <summary>The process of the run that said it: 1 for its first, each retry's or go-on's the next; 0 before its run stamped it.</summary>
    public int Generation { get; init; }

    /// <summary>Its place in its run's order, from 1; 0 before its run stamped it.</summary>
    public long Sequence { get; init; }

    /// <summary>An event the session's record keeps, of today's kinds: the person's words, the agent's, a tool, a turn, usage, a note.</summary>
    public sealed record Said(SessionEvent Event) : WireReading;

    /// <summary>A line the person reads on the run's console, as its transcript says it.</summary>
    public sealed record Shown(string Text) : WireReading;

    /// <summary>The agent named the conversation it keeps, the id a resume asks for.</summary>
    public sealed record ConversationNamed(string Conversation) : WireReading;

    /// <summary>The account's windows as the agent reported them, unread: the provider's window words read them above.</summary>
    public sealed record WindowsSaid(JsonElement Windows) : WireReading;

    /// <summary>A stream the agent runs beside itself opened: a subagent or a background task, its own console (CONSOLE3c).</summary>
    public sealed record StreamOpened(SessionStream Stream) : WireReading;

    /// <summary>A line of a stream beside the run.</summary>
    public sealed record StreamSaid(string Stream, string Text) : WireReading;

    /// <summary>A stream beside the run ended, with the state its agent gave it, or none.</summary>
    public sealed record StreamEnded(string Stream, string? State) : WireReading;

    /// <summary>
    /// A turn or an open ended on its door's failure: the one reading every failure is, on either door, which the account's reader reads
    /// (<c>AgentErrors.Read</c>).
    /// </summary>
    public sealed record Failed(DoorFailure Failure) : WireReading;

    /// <summary>
    /// The agent refused the model its actor asked at the open, before any prompt: the account's refusal of that model, which the offers
    /// store keeps before the open's re-ask walks on (D205 §3.3).
    /// </summary>
    public sealed record ModelRefused(ModelRefusal Refusal) : WireReading;
}
