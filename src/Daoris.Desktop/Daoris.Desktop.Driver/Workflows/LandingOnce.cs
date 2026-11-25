namespace Daoris.Driver;

/// <summary>
/// Whether the look's landing has settled the deliveries a stopped run left: once for each holder of this memory, and not at all again
/// once a settlement ended clean. The watch holds one for its whole life and hands it to the driver it builds for each look, so the
/// settlement is once per watch and not once per look; a driver handed none has its own, which is once for a one-look run. A settlement
/// that left a delivery for the next look, or failed, gives its turn back (<see cref="Release"/>), so that look asks again.
/// </summary>
internal sealed class LandingOnce
{
    private int _taken;

    /// <summary>True for the first caller alone, which then settles; every later one finds it settled, until the turn is given back.</summary>
    public bool Take() => Interlocked.Exchange(ref _taken, 1) == 0;

    /// <summary>The turn given back by a settlement that did not end clean: the next look takes it.</summary>
    public void Release() => Volatile.Write(ref _taken, 0);
}
