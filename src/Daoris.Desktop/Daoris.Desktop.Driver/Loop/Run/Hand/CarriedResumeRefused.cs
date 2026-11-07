namespace Daoris.Driver;

/// <summary>A carried conversation's resume the door would not take, before any prompt: the start goes on in the brief instead.</summary>
internal sealed class CarriedResumeRefused(ContinueReason why) : Exception("the carried conversation could not be resumed")
{
    public ContinueReason Why => why;

    /// <summary>
    /// Whether the resume ended refused with nothing prompted: the door said so, or a native harness that never named its conversation
    /// exited on a failure, as <see cref="ResumeConclusion"/> reads it.
    /// </summary>
    public static ContinueReason? Of(ResumeAsk ask, AgentDoor door, int? exit)
    {
        if (ask.Prompted) return null;
        if (ask.Refused is { } refused) return refused;
        return door.Can.ResumesByRestart && !ask.Named && exit is int failed && failed != 0 ? ContinueWhy.Of(ContinueWhy.Exited) : null;
    }
}
