using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Daoris.Knowledge.Http;

/// <summary>One value a confirmation's card shows: its name, its JSON type and its value (PERSONDOOR1b).</summary>
/// <param name="Name">An address's value by its parameter's or its query's name; a body's by its JSON pointer.</param>
/// <param name="Type"><c>string</c>, <c>number</c>, <c>boolean</c>, <c>null</c>, <c>object</c> or <c>array</c>.</param>
/// <param name="Value">A string as it is, and anything else as its JSON.</param>
public sealed record ConfirmationValue(string Name, string Type, string Value);

/// <summary>
/// What a door binds (PERSONDOOR1b, after its review): the body's type as its handler takes it, whether the body may be
/// absent, and the names it reads from the query. Read from the host's own endpoint, the body's type from the metadata the
/// framework records for the parameter it binds and the query's names from the handler's own parameters, so it cannot
/// drift from what the handlers bind.
/// </summary>
public sealed record DoorContract(JsonTypeInfo? Body, bool BodyOptional, IReadOnlySet<string> Query, JsonSerializerOptions Json)
{
    /// <summary>
    /// The values a card shows for <paramref name="query"/> and <paramref name="body"/>, each once and each a name the door
    /// binds, or the sentence the ask is refused with. The card must never show one act while its grant does another: a
    /// name the binder ignores would be shown and never kept, leaving the door's own value unbound (a dismissal naming no
    /// conflict dismisses every one), so it is refused; and a name given twice, as the binder reads names (ignoring
    /// case), is refused, since the binder keeps one. At most <paramref name="limit"/> values, counted as they are read.
    /// </summary>
    public (List<ConfirmationValue> Values, List<ConfirmationValue> Fields, string? Refusal) Read(
        IReadOnlyList<(string Name, string Value)> route, string query, JsonElement? body, int limit)
    {
        var values = route.Select(value => new ConfirmationValue(value.Name, "string", value.Value)).ToList();
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in new QueryStringEnumerable(query))
        {
            var name = pair.DecodeName().ToString();
            if (!Query.Contains(name)) return ([], [], $"The query's `{name}` is not a name this door reads, so it would be shown and never kept.");
            if (!named.Add(name)) return ([], [], $"The query names `{name}` twice, and this door reads one.");
            values.Add(new ConfirmationValue(name, "string", pair.DecodeValue().ToString()));
        }

        var fields = new List<ConfirmationValue>();
        if (body is not { } element) return (values, fields, null);
        if (Body is null) return ([], [], "This door binds no body, so a body would be shown and never kept.");

        var refusal = Walk(Body, element, "", fields, limit);
        return refusal is null ? (values, fields, null) : ([], [], refusal);
    }

    private string? Walk(JsonTypeInfo type, JsonElement element, string pointer, List<ConfirmationValue> into, int limit)
    {
        if (type.Kind is JsonTypeInfoKind.Object or JsonTypeInfoKind.Dictionary && element.ValueKind == JsonValueKind.Object)
        {
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var any = false;
            foreach (var property in element.EnumerateObject())
            {
                any = true;
                var at = $"{pointer}/{Escape(property.Name)}";
                if (!named.Add(property.Name)) return $"`{at}` is named twice, as the door reads names, and it keeps one.";

                JsonTypeInfo? inner;
                if (type.Kind == JsonTypeInfoKind.Dictionary)
                {
                    inner = TypeOf(type.ElementType);
                }
                else
                {
                    var bound = type.Properties.FirstOrDefault(p => string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase));
                    if (bound is null) return $"`{at}` is not a name this door binds, so it would be shown and never kept.";
                    inner = TypeOf(bound.PropertyType);
                }

                var refusal = inner is null ? Leaf(element: property.Value, at, into, limit) : Walk(inner, property.Value, at, into, limit);
                if (refusal is not null) return refusal;
            }

            return any || pointer.Length == 0 ? null : Leaf(element, pointer, into, limit);
        }

        if (type.Kind == JsonTypeInfoKind.Enumerable && element.ValueKind == JsonValueKind.Array && TypeOf(type.ElementType) is { } item)
        {
            var count = 0;
            foreach (var value in element.EnumerateArray())
            {
                var refusal = Walk(item, value, $"{pointer}/{count++}", into, limit);
                if (refusal is not null) return refusal;
            }

            return count > 0 ? null : Leaf(element, pointer, into, limit);
        }

        // A value the door types as a leaf (a string, a number, a flag, or any JSON it keeps as it is), or one whose shape
        // its type does not take, which its binder refuses: shown whole, as it is.
        return Leaf(element, pointer, into, limit);
    }

    private JsonTypeInfo? TypeOf(Type? type) => type is not null && Json.TryGetTypeInfo(type, out var info) ? info : null;

    private static string? Leaf(JsonElement element, string pointer, List<ConfirmationValue> into, int limit)
    {
        if (into.Count >= limit) return $"The body carries more than {limit} values, more than a confirmation shows.";
        into.Add(new ConfirmationValue(
            pointer.Length == 0 ? "/" : pointer,
            element.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                JsonValueKind.Array => "array",
                _ => "object",
            },
            element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText()));
        return null;
    }

    // A JSON pointer's escapes (RFC 6901): `~` as `~0` and `/` as `~1`, so an empty name and a name holding a slash are
    // each their own pointer.
    private static string Escape(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}

/// <summary>Each door's <see cref="DoorContract"/>, read from the host's own endpoints when a confirmation is asked.</summary>
public sealed class DoorContracts(IServiceProvider services)
{
    /// <summary>The contract of the door mapped at <paramref name="method"/> and <paramref name="pattern"/>, or null for none.</summary>
    public DoorContract? For(string method, string pattern)
    {
        var json = services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        foreach (var endpoint in services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            if (!string.Equals(endpoint.RoutePattern.RawText, pattern, StringComparison.Ordinal)) continue;
            if (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            var accepts = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();
            var body = accepts?.RequestType is { } type && json.TryGetTypeInfo(type, out var info) ? info : null;
            var route = endpoint.RoutePattern.Parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // The query's names are the handler's own simple parameters that the route does not fill, as the framework binds
            // them: a string, a number, a flag. A service, the context, a token or the body is none of them.
            var query = (endpoint.Metadata.GetMetadata<MethodInfo>()?.GetParameters() ?? [])
                .Where(parameter => parameter.Name is { } name && !route.Contains(name) && IsSimple(parameter.ParameterType))
                .Select(parameter => parameter.Name!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new DoorContract(body, accepts?.IsOptional ?? true, query, json);
        }

        return null;
    }

    private static bool IsSimple(Type type)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        return plain == typeof(string) || plain.IsPrimitive || plain.IsEnum || plain == typeof(decimal)
            || plain == typeof(DateTimeOffset) || plain == typeof(DateTime) || plain == typeof(Guid);
    }
}
