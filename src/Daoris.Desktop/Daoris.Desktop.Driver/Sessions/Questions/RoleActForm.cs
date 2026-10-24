using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Why a role's act or appointment is refused (STEWARDROLE1a-i, D209 §3.4), beside <see cref="SupervisorWhy"/>'s codes, which it shares.</summary>
public static class RoleWhy
{
    /// <summary>The act is not in the role's table: the owner's to do.</summary>
    public const string NotRole = "not-role";

    /// <summary>The act is in the role's table and not in the appointment's <c>acts</c>, or the set-up without the owner's tick.</summary>
    public const string NotGranted = "not-granted";

    /// <summary>The walk to the holder's harness found it started on another configuration, or the verb outside it.</summary>
    public const string HolderConfig = "holder-config";
}

/// <summary>The role's route and its acts (STEWARDROLE1a-i, D209 §3.3): one route, every act a row of its role's table.</summary>
public static class RoleRoutes
{
    /// <summary><c>ROLE_ACT { role, act, generation, form, proof }</c>: <c>Role</c> in the pipe's census.</summary>
    public const string Act = "ROLE_ACT";
}

/// <summary>
/// <c>ROLE_ACT</c>'s exact form (D209 §3.3): <c>{ role, act, generation, form, proof }</c>, every field named once (ignoring case, as the
/// bridge reads names) and nothing else; <c>form</c> the act's own exact form, an object its role's table judges. The proof is the
/// holder's HMAC over the hello's nonce and <see cref="Canonical"/>, every field but <c>proof</c>.
/// </summary>
public sealed record RoleActForm(string Role, string Act, string Generation, JsonElement Form, string Proof)
{
    /// <summary>
    /// The canonical bytes the proof is over: the route's name, then the role, the act and the generation, then the act's form written
    /// again compactly, its fields in the order sent, as one JSON array, UTF-8. One function on both sides.
    /// </summary>
    public byte[] Canonical => Bytes(Role, Act, Generation, Form);

    /// <summary>The canonical bytes for the verb, from the values it sends.</summary>
    public static byte[] Bytes(string role, string act, string generation, JsonElement form)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(RoleRoutes.Act);
            writer.WriteStringValue(role);
            writer.WriteStringValue(act);
            writer.WriteStringValue(generation);
            form.WriteTo(writer);
            writer.WriteEndArray();
        }

        return stream.ToArray();
    }

    /// <summary>The <c>role</c> a payload names, read alone, so the role's own flag is judged before anything else is read; null where it names none.</summary>
    public static string? RoleOf(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return null;
        foreach (var property in body.EnumerateObject())
        {
            if (string.Equals(property.Name, "role", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    /// <summary>The form <paramref name="payload"/> is, or null where it is anything more or less.</summary>
    public static RoleActForm? Read(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in body.EnumerateObject())
        {
            if (!fields.TryAdd(property.Name, property.Value)) return null;
        }

        string[] named = ["role", "act", "generation", "form", "proof"];
        if (fields.Count != named.Length || !named.All(fields.ContainsKey)) return null;
        if (Text(fields["role"], 40) is not { } role || Text(fields["act"], 40) is not { } act || Text(fields["generation"], 128) is not { } generation
            || Text(fields["proof"], 64) is not { } proof || fields["form"].ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RoleActForm(role, act, generation, fields["form"].Clone(), proof);

        static string? Text(JsonElement value, int max) =>
            value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && text.Length <= max
            && text.Trim().Length == text.Length ? text : null;
    }

    /// <summary>The form as the verb sends it: its fields in the form's order.</summary>
    public object Payload() => new { role = Role, act = Act, generation = Generation, form = Form, proof = Proof };
}
