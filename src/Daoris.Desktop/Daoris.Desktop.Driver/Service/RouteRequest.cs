using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Contracts;

namespace Daoris.Driver;

/// <summary>
/// A declared route's request as the host binds it: the record's path values in its path, the rest in the query of a GET or
/// DELETE or in the body otherwise, but for a <see cref="FromQueryAttribute"/> value, which travels in the query. A value left null is not sent, so the host reads its default.
/// </summary>
internal static class RouteRequest
{
    public static (ServiceVerb Verb, string Path, string? Json) Of<TAnswer>(IRoute<TAnswer> request)
    {
        var route = RouteDeclaration.Of(request.GetType());
        var verb = route.Method switch
        {
            "GET" => ServiceVerb.Get,
            "POST" => ServiceVerb.Post,
            "DELETE" => ServiceVerb.Delete,
            _ => throw new NotSupportedException($"{route.Request.Name} is declared {route.Method}, a method the service's doors do not take."),
        };
        var type = ContractJson.Options.GetTypeInfo(request.GetType());
        var values = type.Properties.ToDictionary(property => property.Name, property => property.Get?.Invoke(request), StringComparer.OrdinalIgnoreCase);
        var path = route.PathWith(name => Text(values.GetValueOrDefault(name)));
        var query = string.Join('&', type.Properties
            .Where(property => values[property.Name] is not null && route.Travels(property.Name) == RouteTravel.Query)
            .Select(property => $"{property.Name}={Uri.EscapeDataString(Text(values[property.Name]))}"));
        var address = query.Length == 0 ? path : $"{path}?{query}";
        if (!route.TakesBody) return (verb, address, null);

        var body = JsonSerializer.SerializeToNode(request, type) as JsonObject ?? new JsonObject();
        foreach (var named in body.Select(pair => pair.Key).Where(name => route.Travels(name) != RouteTravel.Body).ToList()) body.Remove(named);
        return (verb, address, body.ToJsonString(ContractJson.Options));
    }

    private static string Text(object? value) => value switch
    {
        null => "",
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
