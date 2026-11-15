namespace Daoris.Driver;

/// <summary>
/// When an active <c>push</c> is pushed (again). A landing not recorded as pushed at its tip is pushed; one recorded as pushed that the
/// remote's ref does not hold, which the push's reading said on its node (<see cref="PushReading.NotOnRemote"/>), is pushed again: the attempt
/// that recorded it is lost, and the remote is what says. A branch the line holds is passed over before this is asked (<see cref="NothingToDeliver"/>).
/// </summary>
internal static class PushAttempts
{
    /// <summary>Whether the push of <paramref name="landed"/>, active as <paramref name="status"/>, is to be started on its door.</summary>
    public static bool Due(LandedBranch landed, NodeStatus status) =>
        !(landed.Pushed && landed.PushedTip == landed.Tip) || string.Equals(status.Holds, PushReading.NotOnRemote, StringComparison.Ordinal);
}
