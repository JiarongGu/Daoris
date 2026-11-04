using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

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
            var travels = route.PathNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase) || !route.TakesBody;
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

    /// <summary>The bound request, or null where a value did not bind.</summary>
    public static async Task<TRequest?> BindAsync<TRequest>(HttpContext http, RouteDeclaration route, JsonTypeInfo<TRequest> type)
        where TRequest : class
    {
        var values = route.TakesBody ? await BodyAsync(http) : new JsonObject();
        if (values is null) return null;

        foreach (var property in type.Properties)
        {
            var fromPath = route.PathNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase);
            string? raw;
            if (fromPath)
            {
                raw = http.Request.RouteValues[property.Name]?.ToString();
                foreach (var named in values.Select(pair => pair.Key).Where(key => string.Equals(key, property.Name, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    values.Remove(named);
                }
            }
            else if (!route.TakesBody)
            {
                raw = http.Request.Query.TryGetValue(property.Name, out var given) ? given.ToString() : null;
            }
            else
            {
                continue;
            }

            if (raw is null) continue;
            var (read, node) = Value(property.PropertyType, raw);
            if (!read) return null;
            if (node is not null) values[property.Name] = node;
        }

        foreach (var property in type.Properties)
        {
            if (property.AssociatedParameter is { HasDefaultValue: false, IsNullable: false }
                && !values.Any(pair => string.Equals(pair.Key, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }
        }

        try
        {
            return values.Deserialize(type);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A body's object; an empty body binds nothing, and a body that is not an object does not bind.</summary>
    private static async Task<JsonObject?> BodyAsync(HttpContext http)
    {
        if (http.Request.ContentLength == 0) return new JsonObject();
        try
        {
            var node = await JsonNode.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
            return node switch
            {
                null => new JsonObject(),
                JsonObject body => body,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
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
