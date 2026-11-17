namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A class fixture that makes the shipping desktop projects' compilations before the class's cases run and, in a run with no scope,
/// binds them whole, so the cost of the binding is the fixture's and never the time of whichever case asks first. The binding is made
/// once a run, whichever class's fixture asks first, and the others wait for it: a second binding of the same compilations repeats every
/// method body's, and three side by side held every core while the run's other cases waited. Under a scope it makes the compilations
/// of the projects the scope reaches and binds nothing: each census binds the trees its scope reaches (<see cref="CensusReach"/>). Every
/// census class that reads <see cref="DesktopCompilations"/> carries it.
/// </summary>
public sealed class DesktopCompilationsWarm
{
    private static readonly Lazy<bool> Bound = new(() =>
    {
        if (CensusScope.Paths is { } scope) _ = DesktopCompilations.Judged(scope);
        else DesktopCompilations.Each.AsParallel().ForAll(compilation => _ = compilation.GetDiagnostics());
        return true;
    });

    public DesktopCompilationsWarm() => Bind();

    /// <summary>Binds once a run; the run's start begins it (<see cref="CensusWarm"/>) and each fixture waits for it.</summary>
    internal static void Bind() => _ = Bound.Value;
}
