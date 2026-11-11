namespace Daoris.Driver;

/// <summary>
/// A registry row as the settings read one: the department's name, the circle it is wired into, its root on this machine where the
/// service holds one, and its kind (<see cref="PlaceKind"/>). The service's registration answers it.
/// </summary>
public interface IRegistryRow
{
    string Repository { get; }

    string Workspace { get; }

    string? Root { get; }

    string Kind { get; }
}
