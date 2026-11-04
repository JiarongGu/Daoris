using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The one binder: maps a handler at its record's declared method and path, binds each request into the record, and writes
/// the handler's <see cref="Answer{T}"/>. A request that does not bind is answered 400 with no body, as the framework's
/// binding answered one.
/// </summary>
public sealed class RouteMap(IEndpointRouteBuilder app)
{
    private JsonSerializerOptions Json => app.ServiceProvider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;

    /// <summary>Maps <paramref name="handler"/> for <typeparamref name="TRequest"/>'s route.</summary>
    /// <exception cref="InvalidOperationException">A record whose address carries a value no address can, refused at start.</exception>
    public IEndpointConventionBuilder Map<TRequest, TAnswer>(Func<TRequest, HttpContext, CancellationToken, Task<Answer<TAnswer>>> handler)
        where TRequest : class, IRoute<TAnswer>
    {
        var json = Json;
        return Endpoint<TRequest>(async (request, http) => await (await handler(request, http, http.RequestAborted)).WriteAsync(http, json));
    }

    /// <summary>Maps a route whose answer is a file's bytes: the handler answers the result itself, a refusal in the house's shape.</summary>
    public IEndpointConventionBuilder MapFile<TRequest>(Func<TRequest, HttpContext, CancellationToken, Task<IResult>> handler)
        where TRequest : class, IRoute<QuestFileBytes> =>
        Endpoint<TRequest>(async (request, http) => await (await handler(request, http, http.RequestAborted)).ExecuteAsync(http));

    private IEndpointConventionBuilder Endpoint<TRequest>(Func<TRequest, HttpContext, Task> answered) where TRequest : class
    {
        var route = RouteDeclaration.Of(typeof(TRequest));
        var type = (JsonTypeInfo<TRequest>)Json.GetTypeInfo(typeof(TRequest));
        RouteBinder.Check(route, type);

        RequestDelegate answer = async http =>
        {
            var (request, refused) = await RouteBinder.BindAsync(http, route, type);
            if (request is null)
            {
                http.Response.StatusCode = refused;
                return;
            }

            await answered(request, http);
        };
        // The declaration rides as the endpoint's metadata, so a confirmation reads what the door binds from it (DoorContracts).
        return app.MapMethods(route.Path, [route.Method], answer).WithMetadata(route);
    }
}
