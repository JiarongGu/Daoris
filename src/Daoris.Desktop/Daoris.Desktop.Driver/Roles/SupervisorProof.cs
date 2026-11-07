using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The holder key: 32 random bytes the window draws at an appointment and
/// puts in the environment of the one terminal it starts for the holder, as <see cref="Variable"/>, in hex. Daoris keeps only its
/// SHA-256, in memory alone; no file, page or pipe ever carries the key.
/// </summary>
/// <remarks>
/// <para><b>Stripped everywhere else</b>: every Daoris executable removes the variable from its own environment at its entry
/// (<see cref="Strip"/>), before anything else, so no start it builds can copy it; and every start's environment drops it
/// (<see cref="GitWiring.Removed"/>). The verb is the one reader: <c>daoris</c> takes it into memory first and then removes it
/// (<see cref="Take"/>), and only <c>sessions supervise</c> reads what it took.</para>
/// </remarks>
public static class HolderKey
{
    /// <summary>
    /// The variable the holder's terminal carries the key in, and the only place the verb reads it from: <c>DAORIS_ROLE_KEY</c>,
    /// since a shared holder's key is no one role's.
    /// </summary>
    public const string Variable = ChildMarks.RoleKeyVariable;

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
    /// A Daoris executable's first act: the variable taken out of this process's environment, so no start it builds afterwards
    /// copies it, whatever that start does with its own block. The inbox's two messaging variables leave with it
    /// (<see cref="ChildMarks.MessagingVariables"/>).
    /// </summary>
    public static void Strip()
    {
        Environment.SetEnvironmentVariable(Variable, null);
        foreach (var name in ChildMarks.MessagingVariables) Environment.SetEnvironmentVariable(name, null);
    }

    /// <summary>
    /// The verb's first act: the variable read into memory, then removed from the environment as <see cref="Strip"/> removes it. Only
    /// <c>sessions supervise</c> asks for what is taken (<see cref="Taken"/>).
    /// </summary>
    public static void Take()
    {
        _taken = Environment.GetEnvironmentVariable(Variable);
        // Present at the entry, whatever its value and whether or not it is a key.
        TerminalRole.Held = _taken is not null;
        Strip();
    }

    /// <summary>
    /// What <see cref="Take"/> read, let go by a verb that holds no key for its life (the events relay); the entry's
    /// marker stays, so a role's terminal is still known as one.
    /// </summary>
    public static void Drop() => _taken = null;

    /// <summary>What <see cref="Take"/> read, or null where the variable is not set.</summary>
    public static string? Taken => _taken;

    /// <summary>
    /// The entry's marker: that the variable is present when <see cref="Take"/> runs, whatever its
    /// value, so this process started in a role's terminal. Every guard reads this, never the environment after the entry, which
    /// <see cref="Take"/> has already emptied.
    /// </summary>
    public static bool RoleTerminal => TerminalRole.Held;

    /// <summary>A test's own entry undone, so the marker does not outlive the case that took it.</summary>
    internal static void Forget()
    {
        _taken = null;
        TerminalRole.Held = false;
    }
}

/// <summary>
/// A role's terminal never acts as the owner nor alone: in a process whose entry found
/// <see cref="HolderKey.Variable"/> present, every verb that would send a person's press refuses <see cref="Code"/> before it connects,
/// and every verb that would act from its own process refuses it instead, exit 2.
/// </summary>
public static class RoleTerminal
{
    /// <summary>The refusal's code, which the instance's door says it by.</summary>
    public const string Code = TerminalRole.Code;

    /// <summary>What the refusal says.</summary>
    public const string Sentence = TerminalRole.Sentence;

    /// <summary>
    /// The verbs a role's terminal runs at all (each by its exact prefix): the roles' verbs and the open reads the
    /// holder needs. Any other verb is refused at the entry; these refuse their own alone paths (<see cref="InstanceDoor"/>).
    /// </summary>
    public static bool Runs(IReadOnlyList<string> args) => args switch
    {
        ["roles", "brief"] => true,
        ["sessions", "supervise", ..] => true,
        ["steward", ..] => true,
        ["sessions", "--json", ..] => true,
        ["notices", ..] => true,
        ["history", ..] => true,
        _ => false,
    };

    /// <summary>The refusal as a verb says it, naming its code.</summary>
    public static string Refusal(string verb) => TerminalRole.Refusal(verb);

    /// <summary>
    /// The host's entry, after <see cref="HolderKey.Take"/>: in a role's terminal, a verb it does not run is refused here, said on
    /// <paramref name="error"/>, exit 2; null lets the verb run.
    /// </summary>
    public static int? AtEntry(IReadOnlyList<string> args, TextWriter error)
    {
        if (!HolderKey.RoleTerminal || Runs(args)) return null;
        error.WriteLine(Refusal("daoris"));
        return 2;
    }
}

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

/// <summary>
/// The holder's proof: an HMAC-SHA256 keyed by the key's SHA-256 over the
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
