using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The holder key (SUPERVISOR1a-ii, D208 point 2; the supervisor design §2.2): 32 random bytes the window draws at an appointment and
/// puts in the environment of the one terminal it starts for the holder, as <see cref="Variable"/>, in hex. Daoris keeps only its
/// SHA-256, in memory alone; no file, page or pipe ever carries the key.
/// </summary>
/// <remarks>
/// <para><b>Stripped everywhere else</b>: every Daoris executable removes the variable from its own environment at its entry
/// (<see cref="Strip"/>), before anything else, so no start it builds can copy it; and every start's environment drops it
/// (<see cref="GitWiring.Removed"/>). The verb is the one reader: <c>daoris-driver</c> takes it into memory first and then removes it
/// (<see cref="Take"/>), and only <c>sessions supervise</c> reads what it took.</para>
/// </remarks>
public static class HolderKey
{
    /// <summary>The variable the holder's terminal carries the key in, and the only place the verb reads it from.</summary>
    public const string Variable = "DAORIS_SUPERVISOR_KEY";

    /// <summary>The key's length in bytes, and so its hex text's in characters twice that.</summary>
    public const int Bytes = 32;

    private static string? _taken;

    /// <summary>A new key, in hex: drawn at each appointment and never reused.</summary>
    public static string Draw() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(Bytes));

    /// <summary>The key's SHA-256, which the instance keeps in memory and the proof is keyed by; null for a text that is no key.</summary>
    public static byte[]? Hash(string? key) => Read(key) is { } bytes ? SHA256.HashData(bytes) : null;

    /// <summary>The key's bytes, or null where <paramref name="key"/> is not 64 hex characters.</summary>
    public static byte[]? Read(string? key)
    {
        if (key is not { Length: Bytes * 2 } || !key.All(Uri.IsHexDigit)) return null;
        return Convert.FromHexString(key);
    }

    /// <summary>
    /// A Daoris executable's first act (§2.2): the variable taken out of this process's environment, so no start it builds afterwards
    /// copies it, whatever that start does with its own block.
    /// </summary>
    public static void Strip() => Environment.SetEnvironmentVariable(Variable, null);

    /// <summary>
    /// The verb's first act: the variable read into memory, then removed from the environment as <see cref="Strip"/> removes it. Only
    /// <c>sessions supervise</c> asks for what was taken (<see cref="Taken"/>).
    /// </summary>
    public static void Take()
    {
        _taken = Environment.GetEnvironmentVariable(Variable);
        Strip();
    }

    /// <summary>What <see cref="Take"/> read, or null where the variable was not set.</summary>
    public static string? Taken => _taken;
}

/// <summary>The supervisor's routes (SUPERVISOR1a-ii; the supervisor design §3.1, §2.3), as the bridge's driver module names them.</summary>
public static class SupervisorRoutes
{
    /// <summary>The holder's answer: <c>Role</c> in the pipe's census, judged by the judge's role method.</summary>
    public const string Answer = "SUPERVISOR_ANSWER";

    /// <summary>What waits for the holder: <c>Open</c>.</summary>
    public const string Waiting = "SUPERVISOR_WAITING";

    /// <summary>The owner's appointment, made and turned off in the window alone: <c>Window</c>.</summary>
    public const string Appoint = "SET_APPOINTMENT";
}

/// <summary>
/// <c>SUPERVISOR_ANSWER</c>'s exact form (§3.1): <c>{ id, question, generation, proof, choice, reasons }</c>, every field named once
/// (ignoring case, as the bridge reads names), of its type and bound, and nothing else, as D201's forms are; <c>reasons</c> 1 to 5,
/// each exactly <c>{ says: 1-300, source: 1-200 }</c>.
/// </summary>
public sealed record SupervisorForm(
    string Id, string Question, string Generation, string Proof, string Choice, IReadOnlyList<SupervisorReason> Reasons)
{
    /// <summary>The form's canonical bytes, which the proof is over.</summary>
    public byte[] Canonical => SupervisorProof.Canonical(Id, Question, Generation, Choice, Reasons);

    /// <summary>The form <paramref name="payload"/> is, or null where it is anything more or less.</summary>
    public static SupervisorForm? Read(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in body.EnumerateObject())
        {
            if (!fields.TryAdd(property.Name, property.Value)) return null;
        }

        string[] named = ["id", "question", "generation", "proof", "choice", "reasons"];
        if (fields.Count != named.Length || !named.All(fields.ContainsKey)) return null;
        if (Text(fields["id"], 128) is not { } id || Text(fields["question"], 128) is not { } question
            || Text(fields["generation"], 128) is not { } generation || Text(fields["proof"], 64) is not { } proof
            || Text(fields["choice"], 80) is not { } choice || fields["reasons"].ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var reasons = new List<SupervisorReason>();
        foreach (var reason in fields["reasons"].EnumerateArray())
        {
            if (reason.ValueKind != JsonValueKind.Object) return null;
            var parts = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in reason.EnumerateObject())
            {
                if (!parts.TryAdd(property.Name, property.Value)) return null;
            }

            if (parts.Count != 2 || !parts.TryGetValue("says", out var says) || !parts.TryGetValue("source", out var source)) return null;
            if (Text(says, QuestionSupervisorDecisions.MaxSays) is not { } said || Text(source, QuestionSupervisorDecisions.MaxSource) is not { } where)
            {
                return null;
            }

            reasons.Add(new SupervisorReason(said, where));
        }

        return reasons.Count is 0 or > QuestionSupervisorDecisions.MostReasons
            ? null
            : new SupervisorForm(id, question, generation, proof, choice, reasons);

        // A string of 1 to max characters, not blank. Its words are kept as written: the reasons are the owner's to read.
        static string? Text(JsonElement value, int max) =>
            value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && text.Length <= max
            && text.Trim().Length > 0 ? text : null;
    }

    /// <summary>The form as the verb sends it: its fields in the form's order.</summary>
    public object Payload() => new
    {
        id = Id, question = Question, generation = Generation, proof = Proof, choice = Choice,
        reasons = Reasons.Select(reason => new { says = reason.Says, source = reason.Source }).ToArray(),
    };
}

/// <summary>
/// The holder's proof (SUPERVISOR1a-ii, D208 point 2; the supervisor design §2.2): an HMAC-SHA256 keyed by the key's SHA-256 over the
/// hello's nonce and the form's canonical bytes. The verb makes it from the key in its environment; the judge checks it against the hash
/// in memory and the connection's nonce. The key never crosses the pipe.
/// </summary>
public static class SupervisorProof
{
    /// <summary>The nonce's length in bytes, drawn for each connection's hello.</summary>
    public const int NonceBytes = 32;

    /// <summary>A fresh nonce for one connection's hello, in hex.</summary>
    public static string Nonce() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(NonceBytes));

    /// <summary>
    /// The form's canonical bytes: every field of <c>SUPERVISOR_ANSWER</c> but <c>proof</c>, in the form's order, as one compact JSON
    /// array, UTF-8. One function on both sides, so the verb and the judge read the same bytes from the same values.
    /// </summary>
    public static byte[] Canonical(string id, string question, string generation, string choice, IReadOnlyList<SupervisorReason> reasons)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteStringValue("SUPERVISOR_ANSWER");
            writer.WriteStringValue(id);
            writer.WriteStringValue(question);
            writer.WriteStringValue(generation);
            writer.WriteStringValue(choice);
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

    /// <summary>The proof, in hex, from the key's hash, the nonce (hex) and the canonical form; null where the nonce is not hex.</summary>
    public static string? Prove(byte[] hash, string nonce, byte[] form)
    {
        if (Bytes(nonce) is not { } drawn) return null;
        byte[] message = [.. drawn, .. form];
        return Convert.ToHexStringLower(HMACSHA256.HashData(hash, message));
    }

    /// <summary>The proof the verb sends, from the key itself; null where the key or the nonce is not what it should be.</summary>
    public static string? Prove(string key, string nonce, byte[] form) =>
        HolderKey.Hash(key) is { } hash ? Prove(hash, nonce, form) : null;

    /// <summary>Whether <paramref name="proof"/> is the proof over <paramref name="nonce"/> and <paramref name="form"/> by the key whose hash is <paramref name="hash"/>, compared in constant time.</summary>
    public static bool Verify(byte[] hash, string nonce, byte[] form, string? proof)
    {
        if (Prove(hash, nonce, form) is not { } expected || proof is not { Length: 64 } || !proof.All(Uri.IsHexDigit)) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(proof));
    }

    private static byte[]? Bytes(string? hex) =>
        hex is { Length: NonceBytes * 2 } && hex.All(Uri.IsHexDigit) ? Convert.FromHexString(hex) : null;
}
