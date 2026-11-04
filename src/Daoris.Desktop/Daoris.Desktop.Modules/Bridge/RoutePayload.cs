using System.Reflection;
using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>How one field of a route's payload record is read, by its parameter's type.</summary>
public enum PayloadRead
{
    /// <summary>A text, a flag or a number with no <c>?</c>: refused as the kit refuses a value missing or of another kind.</summary>
    Required,

    /// <summary><c>string?</c>: the text, or null where it is absent, blank or no text.</summary>
    Text,

    /// <summary><c>bool</c> defaulting to false: true only where the page says true.</summary>
    Flag,

    /// <summary><c>bool?</c>: true or false as said, null otherwise.</summary>
    Choice,

    /// <summary><c>long?</c>: a whole number, null where it is absent or none.</summary>
    Number,

    /// <summary><c>IReadOnlyList&lt;string&gt;?</c>: the list's texts, an item of another kind left out; null where no list is sent.</summary>
    Texts,

    /// <summary>
    /// <c>JsonElement?</c>: the value as sent, a JSON null kept as one; null only where the field is absent, so it also says whether
    /// the field was sent at all. For a shape another reader owns: a graph, a rule's object.
    /// </summary>
    Json,

    /// <summary>
    /// <c>IReadOnlyList&lt;TRecord&gt;?</c>: each object of the list read as its record by these same rules, an item of another kind
    /// left out; null where no list is sent.
    /// </summary>
    Records,

    /// <summary><see cref="WholePayload"/>: the payload itself, whole, the record's only field.</summary>
    Whole,
}

/// <summary>One field of a payload record: its name on the wire, its type, and how it is read.</summary>
public sealed record PayloadField(string Name, Type Type, PayloadRead Read)
{
    /// <summary>The record each item of a <see cref="PayloadRead.Records"/> field is read as.</summary>
    public Type Item => Type.GetGenericArguments()[0];
}

/// <summary>
/// A route's payload record, read off the request: each parameter of the record's one public constructor by its camel-cased
/// name, the way <see cref="DriverArguments"/> and the kit's <see cref="PayloadHelper"/> read a field by hand, so a handler that
/// takes its record answers and refuses exactly as its hand-written reads did. The parameter's type says how it is read
/// (<see cref="PayloadRead"/>); a type it does not read fails the module's first use.
/// </summary>
public static class RoutePayload
{
    /// <summary>The fields of <paramref name="record"/>, in its constructor's order.</summary>
    public static IReadOnlyList<PayloadField> Fields(Type record)
    {
        if (record.GetConstructors() is not [var constructor])
        {
            throw new InvalidOperationException($"{record.Name} is a route's payload: a record with one public constructor, its fields.");
        }

        var nullability = new NullabilityInfoContext();
        return [.. constructor.GetParameters().Select(parameter =>
            new PayloadField(JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!), parameter.ParameterType, ReadOf(record, parameter, nullability)))];
    }

    /// <summary>The reader of <typeparamref name="T"/> off a request, its fields found once.</summary>
    public static Func<IpcRequest, T> Reader<T>()
    {
        var read = ObjectReader<T>();
        return request => read(request.Payload);
    }

    /// <summary>The reader of <typeparamref name="T"/> off one JSON object: the request's payload, or an item of a list of records.</summary>
    private static Func<JsonElement?, T> ObjectReader<T>()
    {
        var constructor = typeof(T).GetConstructors().Single();
        var reads = Fields(typeof(T)).Select(Read).ToArray();
        return payload =>
        {
            var values = new object?[reads.Length];
            for (var at = 0; at < reads.Length; at++) values[at] = reads[at](payload);
            return (T)constructor.Invoke(values);
        };
    }

    private static PayloadRead ReadOf(Type record, ParameterInfo parameter, NullabilityInfoContext nullability)
    {
        var type = parameter.ParameterType;
        var nullable = !type.IsValueType && nullability.Create(parameter).WriteState == NullabilityState.Nullable;
        if (type == typeof(bool)) return parameter is { HasDefaultValue: true, DefaultValue: false } ? PayloadRead.Flag : PayloadRead.Required;
        if (type == typeof(bool?)) return PayloadRead.Choice;
        if (type == typeof(long?)) return PayloadRead.Number;
        if (type == typeof(JsonElement?)) return PayloadRead.Json;
        if (type == typeof(WholePayload)) return PayloadRead.Whole;
        if (type == typeof(string)) return nullable ? PayloadRead.Text : PayloadRead.Required;
        if (type == typeof(IReadOnlyList<string>) && nullable) return PayloadRead.Texts;
        if (nullable && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) && type.GetGenericArguments()[0].IsClass)
        {
            return PayloadRead.Records;
        }

        if (type == typeof(long) || type == typeof(int) || type == typeof(double)) return PayloadRead.Required;
        throw new InvalidOperationException(
            $"{record.Name}.{parameter.Name} is a {type.Name}, and a payload reads text, a flag, a yes or no, a whole number, a list of text or of records, or a JSON value.");
    }

    private static Func<JsonElement?, object?> Read(PayloadField field) => field.Read switch
    {
        PayloadRead.Required => Generic(nameof(RequiredOf), field.Type, field.Name),
        PayloadRead.Text => payload => DriverArguments.Optional(payload, field.Name),
        PayloadRead.Flag => payload => DriverArguments.Flag(payload, field.Name),
        PayloadRead.Choice => payload => DriverArguments.Choice(payload, field.Name),
        PayloadRead.Number => payload => DriverArguments.Number(payload, field.Name),
        PayloadRead.Json => payload => DriverArguments.Sent(payload, field.Name),
        PayloadRead.Records => Generic(nameof(RecordsOf), field.Item, field.Name),
        PayloadRead.Whole => payload => new WholePayload(payload),
        _ => payload => DriverArguments.Listed(payload, field.Name),
    };

    private static Func<JsonElement?, object?> Generic(string reader, Type type, string name) =>
        (Func<JsonElement?, object?>)typeof(RoutePayload).GetMethod(reader, BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type).Invoke(null, [name])!;

    /// <summary>The kit's own required read, so a value missing or of another kind is refused in the kit's code and words.</summary>
    private static Func<JsonElement?, object?> RequiredOf<TValue>(string name) => payload => PayloadHelper.GetRequiredValue<TValue>(payload, name);

    private static Func<JsonElement?, object?> RecordsOf<TItem>(string name)
    {
        var read = ObjectReader<TItem>();
        return payload => DriverArguments.Sent(payload, name) is { ValueKind: JsonValueKind.Array } items
            ? (IReadOnlyList<TItem>)[.. items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(item => read(item))]
            : null;
    }
}
