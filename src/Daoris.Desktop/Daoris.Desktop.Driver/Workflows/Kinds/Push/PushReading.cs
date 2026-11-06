namespace Daoris.Driver;

/// <summary>
/// A <c>push</c>'s reading (G8): Daoris's push (the push act) wrote the commit it pushed on the landing, and the node is reached when git's
/// remote-tracking ref holds it. Unread it is unknown; nothing pushed, or a ref elsewhere, is not yet.
/// </summary>
internal static class PushReading
{
    public static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.OfBranch(reading) is { } refused) return refused;
        var landing = NodeObserving.LandingOf(reading.Unit);
        if (landing is not { Pushed: true, PushedTip: { } pushed }) return NodeObserving.Seen(reading, null, "the branch was not pushed yet");
        if (reading.Git?.RemoteTip is not { } remoteTip)
        {
            return NodeObserving.Seen(reading, NodeStates.Unknown, $"{TextBounds.ShortCommit(pushed, 8)} was pushed, and git's remote-tracking ref was not read");
        }

        return remoteTip(landing) switch
        {
            null => NodeObserving.Seen(reading, NodeStates.Unknown, "git could not read the remote-tracking ref"),
            var remote when string.Equals(remote, pushed, StringComparison.Ordinal) => NodeObserving.Reached(reading, NodeSource.Git, pushed,
                LandedBranches.FileName, landing.Branch, $"pushed at {TextBounds.ShortCommit(pushed, 8)}, and the remote-tracking ref holds it"),
            _ => NodeObserving.Seen(reading, null, "the remote-tracking ref does not hold the pushed commit"),
        };
    }
}
