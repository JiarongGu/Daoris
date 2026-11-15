namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A class fixture that binds the shipping desktop projects' shared compilation before the class's cases run, so the cost of the
/// binding is the fixture's and never the time of whichever case asks first. Every census class that reads
/// <see cref="DesktopCompilations"/> carries it.
/// </summary>
public sealed class DesktopCompilationsWarm
{
    public DesktopCompilationsWarm() => DesktopCompilations.Each.AsParallel().ForAll(compilation => _ = compilation.GetDiagnostics());
}
