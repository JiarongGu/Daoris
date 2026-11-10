using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The supervisor's routes, as the bridge's driver module names them.</summary>
public static class SupervisorRoutes
{
    /// <summary>The holder's decision on a subject of any kind: <c>Role</c> in the pipe's census, judged by the judge's role method.</summary>
    public const string Decide = "SUPERVISOR_DECIDE";

    /// <summary>The holder's leaving a subject of any kind to the owner: <c>Role</c>, judged as the decide is.</summary>
    public const string Leave = "SUPERVISOR_LEAVE";

    /// <summary>The role's acts, each judged by the judge's role method and nothing else.</summary>
    public static readonly IReadOnlyList<string> Acts = [Decide, Leave];

    /// <summary>What waits for the holder: <c>Open</c>.</summary>
    public const string Waiting = "SUPERVISOR_WAITING";
}

/// <summary>The holder's proof, made as <see cref="KeyedProof"/> makes one, over the form's canonical bytes.</summary>
public static class SupervisorProof
{
    /// <summary>A fresh nonce for one connection's hello, in hex, of <see cref="KeyedProof.NonceBytes"/> bytes.</summary>
    public static string Nonce() => KeyedProof.Nonce();

    /// <summary>
    /// <paramref name="route"/>'s canonical bytes, as one compact JSON array, UTF-8: its name first, so a proof over one act is never
    /// another's; then the form's fields but <c>proof</c> in its order, the subject whole (its kind, session and id), <paramref name="chosen"/>
    /// the decide's <c>do</c> or the leave's <c>bound</c>, and the <paramref name="choice"/> or null. One function on both sides, so the
    /// verb and the judge read the same bytes from the same values, and a word about one subject is never another's.
    /// </summary>
    public static byte[] Canonical(
        string route, SupervisorSubject subject, string generation, string chosen, string? choice, IReadOnlyList<SupervisorReason> reasons)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(route);
            writer.WriteStringValue(subject.Kind);
            writer.WriteStringValue(subject.Session);
            writer.WriteStringValue(subject.Id);
            if (subject.Held is not null) writer.WriteStringValue(subject.Held);
            if (subject.Act is { } met)
            {
                writer.WriteStringValue(met.Start);
                writer.WriteStringValue(met.What);
            }
            writer.WriteStringValue(generation);
            writer.WriteStringValue(chosen);
            if (choice is null) writer.WriteNullValue();
            else writer.WriteStringValue(choice);
            writer.WriteStartArray();
            foreach (var reason in reasons)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(reason.Says);
                writer.WriteStringValue(reason.Source);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteEndArray();
        }

        return stream.ToArray();
    }

    /// <summary>The proof, in hex, from the key's hash, the nonce (hex) and the canonical form (<see cref="KeyedProof.Prove(byte[], string, byte[])"/>).</summary>
    public static string? Prove(byte[] hash, string nonce, byte[] form) => KeyedProof.Prove(hash, nonce, form);

    /// <summary>The proof the verb sends, from the key itself; null where the key or the nonce is not what it should be.</summary>
    public static string? Prove(string key, string nonce, byte[] form) => KeyedProof.Prove(key, nonce, form);

    /// <summary>Whether <paramref name="proof"/> is the proof over <paramref name="nonce"/> and <paramref name="form"/> by the key whose hash is <paramref name="hash"/>.</summary>
    public static bool Verify(byte[] hash, string nonce, byte[] form, string? proof) => KeyedProof.Verify(hash, nonce, form, proof);
}
