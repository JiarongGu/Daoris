namespace Daoris.Driver;

/// <summary>
/// Whether the look's landing has settled the deliveries a stopped run left: once for each holder of this memory. The watch holds one for its
/// whole life and hands it to the driver it builds for each look, so the settlement is once per watch and not once per look; a driver handed
/// none has its own, which is once for a one-look run.
/// </summary>
internal sealed class LandingOnce
{
    private int _taken;

    /// <summary>True for the first caller alone, which then settles; every later one finds it settled.</summary>
    public bool Take() => Interlocked.Exchange(ref _taken, 1) == 0;
}
