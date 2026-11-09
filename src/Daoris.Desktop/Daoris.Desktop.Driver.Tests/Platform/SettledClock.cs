using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The volume's clock read a minute ahead for the flow that holds it, until disposed: a fixture's files are seconds old where a person's
/// are settled, so an answer a test sees refused is refused by its stats, never by its files' age.
/// </summary>
internal sealed class SettledClock : IDisposable
{
    private SettledClock() => VolumeClock.Seam.Value = _ => DateTime.UtcNow + TimeSpan.FromMinutes(1);

    internal static IDisposable Ahead() => new SettledClock();

    public void Dispose() => VolumeClock.Seam.Value = null;
}
