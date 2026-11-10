namespace Daoris.Knowledge.Mcp;

/// <summary>
/// How many a search's answer found, said by its tool as it answers and kept by the call's noting beside its place
/// (<see cref="CallPlaceFilter"/>), so Ask Daoris's receipt says <i>3 found</i>. A tool that lists says it; one that answers a single
/// thing says nothing, and a call no noting listens to keeps nothing.
/// </summary>
/// <remarks>
/// Held for the flow of work the noting starts, so the tool's own answer stays the words the agent reads and its signature stays the
/// harness's.
/// </remarks>
public sealed class CallFound
{
    private static readonly AsyncLocal<CallFound?> Listening = new();

    /// <summary>The count the call's tool said its answer found, or null where it said none.</summary>
    public int? Count { get; private set; }

    /// <summary>A count heard from the call this flow of work makes from here on.</summary>
    public static CallFound Listen() => Listening.Value = new CallFound();

    /// <summary>The answer's count, kept where the call is listened to.</summary>
    public static void Say(int count)
    {
        if (Listening.Value is { } found) found.Count = count;
    }
}
