using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.Features;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// Binds a request into its route's record: the path's values, then the query of a GET or DELETE or the body otherwise,
/// read through the record's own <see cref="JsonTypeInfo"/>.
/// </summary>
/// <remarks>
/// It replaces the framework's binding of each handler's parameters, so it binds as that did: names matched ignoring case,
/// a flag by <see cref="bool.TryParse(string?, out bool)"/>, a number by the invariant culture, an empty value of a nullable
/// number as absent, and a value that does not read or a required value left out as null, which the route answers 400 with
/// no body. A shared host serves drivers of older builds, so a difference here is a regression on the wire.
/// </remarks>
internal static class RouteBinder
{
    private static readonly Type[] Scalars = [typeof(string), typeof(bool), typeof(int), typeof(long), typeof(double)];

    /// <summary>The refusal at start of a record whose query or path carries a value no query can.</summary>
    public static void Check(RouteDeclaration route, JsonTypeInfo type)
    {
        foreach (var property in type.Properties)
        {
            var travels = route.Travels(property.Name) != RouteTravel.Body;
            if (travels && !Scalars.Contains(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType))
            {
                throw new InvalidOperationException(
                    $"{route.Request.Name}'s `{property.Name}` travels in the address, which carries a string, a flag or a number, and it is a {property.PropertyType.Name}.");
            }
        }

        foreach (var name in route.PathNames)
        {
            if (!type.Properties.Any(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"{route.Request.Name} has no property for its path's `{{{name}}}`.");
            }
        }
    }

    /// <summary>The bound request, or null with the status a request that did not bind is answered: 400, or 415 for a body that is not JSON.</summary>
    public static async Task<(TRequest? Request, int Refused)> BindAsync<TRequest>(HttpContext http, RouteDeclaration route, JsonTypeInfo<TRequest> type)
        where TRequest : class
    {
        // As the framework bound a body parameter: only a record that has body values reads one; a body whose content type is not
        // JSON is 415 unread; a body that is absent, or JSON's null, binds as naming nothing where the body is optional and does not
        // bind where it is required. Its values are never required one by one: the serializer leaves one out as its default.
        var values = new JsonObject();
        if (type.Properties.Any(property => route.Travels(property.Name) == RouteTravel.Body))
        {
            var (body, refused) = await BodyAsync(http);
            if (refused != 0) return (null, refused);
            if (body is null && !route.BodyOptional) return (null, StatusCodes.Status400BadRequest);
            values = body ?? values;
        }

        foreach (var property in type.Properties)
        {
            var travels = route.Travels(property.Name);
            if (travels == RouteTravel.Body) continue;

            // A body never fills an address's value; the address's own is bound.
            foreach (var named in values.Select(pair => pair.Key).Where(key => string.Equals(key, property.Name, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                values.Remove(named);
            }

            var raw = travels == RouteTravel.Path
                ? http.Request.RouteValues[property.Name]?.ToString()
                : http.Request.Query.TryGetValue(property.Name, out var given) ? given.ToString() : null;
            if (raw is null)
            {
                if (property.AssociatedParameter is { HasDefaultValue: false, IsNullable: false }) return (null, StatusCodes.Status400BadRequest);
                continue;
            }

            var (read, node) = Value(property.PropertyType, raw);
            if (!read) return (null, StatusCodes.Status400BadRequest);
            if (node is not null) values[property.Name] = node;
        }

        try
        {
            return values.Deserialize(type) is { } request ? (request, 0) : (null, StatusCodes.Status400BadRequest);
        }
        catch (JsonException)
        {
            return (null, StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>A body's object; null for none, and a refusal's status for one that is not JSON or not an object.</summary>
    private static async Task<(JsonObject? Body, int Refused)> BodyAsync(HttpContext http)
    {
        if (http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody != true) return (null, 0);
        if (!http.Request.HasJsonContentType()) return (null, StatusCodes.Status415UnsupportedMediaType);
        try
        {
            return await JsonNode.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted) switch
            {
                null => (null, 0),
                JsonObject body => (body, 0),
                _ => (null, StatusCodes.Status400BadRequest),
            };
        }
        catch (JsonException)
        {
            return (null, StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>A query's or path's text as the property's JSON value: false where it does not read; no node where it is absent.</summary>
    private static (bool Read, JsonNode? Node) Value(Type type, string raw)
    {
        var plain = Nullable.GetUnderlyingType(type);
        if (plain is not null && raw.Length == 0) return (true, null);
        plain ??= type;
        if (plain == typeof(string)) return (true, JsonValue.Create(raw));
        if (plain == typeof(bool)) return bool.TryParse(raw, out var flag) ? (true, JsonValue.Create(flag)) : (false, null);
        if (plain == typeof(int)) return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) ? (true, JsonValue.Create(whole)) : (false, null);
        if (plain == typeof(long)) return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var big) ? (true, JsonValue.Create(big)) : (false, null);
        return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number)
            ? (true, JsonValue.Create(number))
            : (false, null);
    }
}
