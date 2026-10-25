namespace Daoris.Driver;

/// <summary>
/// The sessions running on this home, read right before a version is judged; <see cref="Problem"/> where they could not be read
/// (FOUNDATION1 mod-v: the shape is the service's, which the install's delete of a download and the workplace's clearing both name; its
/// read is the agents', which know the process markers, <see cref="LiveSessionReads"/>).
/// </summary>
public sealed record LiveSessions(IReadOnlyList<LiveSession> Sessions, string? Problem)
{
    /// <summary>None runs.</summary>
    public static LiveSessions None { get; } = new([], null);
}
