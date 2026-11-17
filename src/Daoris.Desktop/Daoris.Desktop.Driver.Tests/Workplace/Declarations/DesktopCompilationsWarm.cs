namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A class fixture that makes the shipping desktop projects' compilations before the class's cases run and, in a run with no scope,
/// binds them whole, so the cost of the binding is the fixture's and never the time of whichever case asks first. Under a scope it
/// makes the compilations of the projects the scope reaches and binds nothing: each census binds the trees its scope reaches
/// (<see cref="CensusReach"/>). Every census class that reads <see cref="DesktopCompilations"/> carries it.
/// </summary>
public sealed class DesktopCompilationsWarm
{
    public DesktopCompilationsWarm()
    {
        if (CensusScope.Paths is { } scope) _ = DesktopCompilations.Judged(scope);
        else DesktopCompilations.Each.AsParallel().ForAll(compilation => _ = compilation.GetDiagnostics());
    }
}
