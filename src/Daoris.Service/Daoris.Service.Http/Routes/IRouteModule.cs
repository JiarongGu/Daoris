namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// One domain's routes. Found at start by reflection over the host's assembly, constructed with the host's
/// <see cref="HostWiring"/>, and mapped once, so <c>Program</c> holds the middleware and its order and no route.
/// </summary>
public interface IRouteModule
{
    /// <summary>Maps each of the domain's handlers through <paramref name="routes"/>, the one binder.</summary>
    void Map(RouteMap routes);
}
