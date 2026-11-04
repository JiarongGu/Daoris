using System.Reflection;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>The route modules, found by reflection over the host's own assembly and mapped in their names' order.</summary>
public static class RouteModules
{
    /// <summary>Every <see cref="IRouteModule"/> <paramref name="assembly"/> holds, by full name.</summary>
    public static IReadOnlyList<Type> In(Assembly assembly) =>
    [
        .. assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IRouteModule).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal),
    ];

    /// <summary>Constructs each module with <paramref name="host"/> and maps its routes on <paramref name="app"/>.</summary>
    /// <exception cref="InvalidOperationException">A module with no constructor taking the wiring, refused at start.</exception>
    public static void MapAll(IEndpointRouteBuilder app, HostWiring host)
    {
        var routes = new RouteMap(app);
        foreach (var type in In(typeof(RouteModules).Assembly))
        {
            if (type.GetConstructor([typeof(HostWiring)]) is not { } made)
            {
                throw new InvalidOperationException($"The route module {type.Name} needs a public constructor that takes the host's {nameof(HostWiring)}.");
            }

            ((IRouteModule)made.Invoke([host])).Map(routes);
        }
    }
}
