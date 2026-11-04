namespace Daoris.Driver;

/// <summary>
/// How long a verb has waited, read off <see cref="SessionsWorld.Clock"/>: the machine's, or a test's that counts from the moment the
/// loop took the words rather than from a loaded machine's late start.
/// </summary>
internal readonly record struct Waited(TimeProvider Clock, long Since)
{
    public static Waited From(SessionsWorld world) => new(world.Clock, world.Clock.GetTimestamp());

    public TimeSpan Elapsed => Clock.GetElapsedTime(Since);
}
