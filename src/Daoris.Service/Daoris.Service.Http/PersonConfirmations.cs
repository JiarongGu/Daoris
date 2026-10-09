using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Daoris.Knowledge;
using Microsoft.AspNetCore.WebUtilities;

namespace Daoris.Knowledge.Http;

/// <summary>Where a confirmation stands (PERSONDOOR1b), as its poll answers it.</summary>
public enum ConfirmationState
{
    /// <summary>Asked, and waiting for the person in the window.</summary>
    Waiting,

    /// <summary>The person confirmed it: its grant may be used once, within its two minutes.</summary>
    Confirmed,

    /// <summary>The person refused it: it grants nothing.</summary>
    Refused,

    /// <summary>Its two minutes passed before it was answered, or before its grant was used: it grants nothing.</summary>
    Expired,

    /// <summary>Its grant was used, once.</summary>
    Used,
}

/// <summary>
/// One request a terminal asked the person to confirm (PERSONDOOR1b): what it is shown by, and what its grant is held to.
/// <see cref="Path"/> is the path and query exactly as asked; the grant is held to the unescaped path, the query as sent,
/// the pattern of the door that answers it, and the body to the byte. The secret's hash is held and never answered.
/// </summary>
public sealed record Confirmation(
    string Id, string Method, string Path, string Pattern, string Act,
    IReadOnlyList<(string Name, string Value)> Values, IReadOnlyList<(string Name, string Value)> Fields, string? Body,
    DateTimeOffset Asked, DateTimeOffset Expires, ConfirmationState State)
{
    internal byte[] Hash { get; init; } = [];

    internal string Unescaped { get; init; } = "";

    internal string Query { get; init; } = "";

    internal byte[] Bytes { get; init; } = [];
}

/// <summary>Why an ask was not taken: none, a host holding no key, a request out of shape, or five already waiting.</summary>
public enum ConfirmationAskRefusal
{
    None,
    NoKey,
    Shape,
    Full,
}

/// <summary>Why an answer was not taken: none, no such confirmation, or one no longer waiting.</summary>
public enum ConfirmationAnswerRefusal
{
    None,
    NotFound,
    NotWaiting,
}

/// <summary>
/// The window's confirmation of what a terminal asked (PERSONDOOR1b, D156 point 4; the person-door design §4.2). A
/// terminal's keyless call to a person's or the driver's door is refused; its client asks here with the exact request
/// (method, path and body) and its own secret's SHA-256; the window lists what waits by the door's name and the request's
/// own fields; the person confirms or refuses with the key; and the client sends the same request again with the secret
/// in <see cref="GrantHeader"/>, which <see cref="PersonGate"/> takes once, for that request alone, within the two
/// minutes of its ask.
/// </summary>
/// <remarks>
/// <para><b>In memory only.</b> Nothing here reaches the store, a file or an answer but what the window shows: a
/// confirmation lives with this start of the host, and none outlives it. A confirmation is forgotten two minutes after its
/// own two minutes end, so the terminal that asked can still read how it ended.</para>
///
/// <para><b>Bounded</b> (§4.2): at most <see cref="Most"/> wait for the person at once; a sixth is refused
/// <c>confirmations-full</c> and asks nothing. One the person answered waits no more, and frees its place.</para>
///
/// <para><b>A session can ask, and cannot answer.</b> The ask is any caller's, as a terminal cannot be told from a
/// session (§4.1); the answers are the key's. What a session asks is a card the person refuses or lets expire. Only a door
/// that wants the key can be asked for, in the very form that wants it, and never the confirmations' own doors.</para>
///
/// <para><b>The secret</b> is made by the client like a key: 32 random bytes in base64url without padding. The host holds
/// only its SHA-256 (lowercase hex as asked), never answers that, and never logs either: a grant whose secret is not in
/// that form grants nothing.</para>
///
/// <para><b>The log</b> (§5.1): one <see cref="Event"/> line as each is asked, confirmed, refused or expires, by the door's
/// method and pattern. Never the id, the secret, its hash, the path's values or the body. A grant used writes nothing; a
/// grant refused, and a sixth ask, are the gate's <c>person.refused</c> lines.</para>
/// </remarks>
public sealed class PersonConfirmations(TimeProvider clock, MachineLog log, bool holds)
{
    /// <summary>The header a client presents its secret in, with the request the person confirmed.</summary>
    public const string GrantHeader = "Daoris-Person-Grant";

    /// <summary>The machine log's event for a confirmation asked, confirmed, refused or expired.</summary>
    public const string Event = "person.confirmation";

    /// <summary>How many may wait for the person at once.</summary>
    public const int Most = 5;

    /// <summary>How long a confirmation lasts from its ask: the wait for the person, and its grant's use, both within it.</summary>
    public static readonly TimeSpan Lasts = TimeSpan.FromMinutes(2);

    /// <summary>How long one that ended is still answered to the terminal that polls it.</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromMinutes(2);

    /// <summary>What a client says while it waits (design §4.2 step 3), answered with an ask so both clients say it alike.</summary>
    public const string WaitingSentence = "Waiting for you to confirm this in Daoris's window…";

    /// <summary>The answer to the person's confirmation.</summary>
    public const string ConfirmedSentence =
        "Confirmed. The terminal that asked may send this once, as it was asked, within its two minutes; nothing else is granted.";

    /// <summary>The answer to the person's refusal.</summary>
    public const string RefusedAnswer = "Refused. Nothing was kept, and the terminal that asked is told so.";

    /// <summary>An ask on a host handed no key, whose doors want none (design §2.1).</summary>
    public const string NoKeySentence =
        "This service was started without a person key, so none of its doors waits for a confirmation: send the request "
        + "again as it was. Nothing was asked.";

    /// <summary>An answer to a confirmation that was confirmed already.</summary>
    public const string ConfirmedAlreadySentence = "This act was confirmed already.";

    /// <summary>An answer to a confirmation the person refused.</summary>
    public const string RefusedSentence = "This act was refused already, and grants nothing.";

    /// <summary>An answer to a confirmation past its two minutes.</summary>
    public const string ExpiredSentence =
        "This act's two minutes passed before it was answered, so it grants nothing. The terminal asks again if it still "
        + "means it.";

    private readonly object _gate = new();
    private readonly List<Confirmation> _kept = [];

    /// <summary>The sentence for a confirmation this host holds no more, or never held.</summary>
    public static string NotFoundSentence(string id) =>
        $"No confirmation `{id}` is held here: one is forgotten two minutes after its own two minutes end, and none outlives "
        + "a start of this service.";

    /// <summary>What waits for the person, oldest first.</summary>
    public IReadOnlyList<Confirmation> Waiting()
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            return [.. _kept.Where(c => c.State == ConfirmationState.Waiting)];
        }
    }

    /// <summary>One confirmation, whatever its state, or null for one not held.</summary>
    public Confirmation? Find(string id)
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            return _kept.Find(c => c.Id == id);
        }
    }

    /// <summary>
    /// A terminal's ask: the request it was refused, exactly, and its secret's SHA-256. Taken only on a host that holds a
    /// key, for a door that wants it in the form the body makes, and while fewer than <see cref="Most"/> wait.
    /// </summary>
    public (Confirmation? Confirmation, ConfirmationAskRefusal Refusal, string Message) Ask(
        string? method, string? path, string? body, string? secretSha256)
    {
        if (!holds) return (null, ConfirmationAskRefusal.NoKey, NoKeySentence);

        var verb = (method ?? "").Trim().ToUpperInvariant();
        if (verb.Length == 0 || !BrowserOrigins.IsWrite(verb))
        {
            return Shape("`method` names the refused request's method, a write: a read is answered to anyone, and needs no confirmation.");
        }

        if (string.IsNullOrEmpty(path) || !path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
        {
            return Shape("`path` names the refused request's path and query on this service, as it was sent, starting with `/`.");
        }

        var question = path.IndexOf('?');
        var (address, query) = question < 0 ? (path, "") : (path[..question], path[question..]);
        var unescaped = PathString.FromUriComponent(address).Value ?? "";
        // A shared host's doors are in the table and never mapped here (design §6).
        if (PersonDoors.Resolve(verb, unescaped) is not { } resolved || resolved.Door.Class == DoorClass.Shared)
        {
            return Shape($"`{verb} {path}` is no door of this service, so nothing there waits for a confirmation.");
        }

        var (door, values) = resolved;
        if (ReferenceEquals(door, PersonDoors.AskConfirmation) || ReferenceEquals(door, PersonDoors.Confirm)
            || ReferenceEquals(door, PersonDoors.Refuse))
        {
            return Shape("A confirmation is answered in Daoris's window, with the person's key; it is never itself confirmed.");
        }

        var text = string.IsNullOrEmpty(body) ? null : body;
        JsonElement? parsed = null;
        if (text is not null)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                parsed = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return Shape("`body` is the refused request's body as it was sent, a JSON object, or absent for none.");
            }

            if (parsed is not { ValueKind: JsonValueKind.Object })
            {
                return Shape("`body` is the refused request's body as it was sent, a JSON object, or absent for none.");
            }
        }

        if (PersonDoors.Wanted(door, parsed) is not { } act)
        {
            return Shape($"{Capitalized(door.Act ?? "This door")} is answered without the person's key, so it needs no confirmation: send it as it was.");
        }

        var hash = (secretSha256 ?? "").Trim().ToLowerInvariant();
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
        {
            return Shape("`secretSha256` is the SHA-256 of a secret the client made, 64 hexadecimal characters; the secret itself is never sent until it is used.");
        }

        var hashBytes = Encoding.ASCII.GetBytes(hash);
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            Sweep(now);
            if (_kept.Any(c => CryptographicOperations.FixedTimeEquals(c.Hash, hashBytes)))
            {
                return Shape("This secret's hash was asked already: a client makes a new secret for each ask.");
            }

            if (_kept.Count(c => c.State == ConfirmationState.Waiting) >= Most)
            {
                return (null, ConfirmationAskRefusal.Full, PersonDoors.FullSentence);
            }

            var queried = new List<(string Name, string Value)>(values);
            foreach (var pair in new QueryStringEnumerable(query)) queried.Add((pair.DecodeName().ToString(), pair.DecodeValue().ToString()));

            var fields = new List<(string Name, string Value)>();
            if (parsed is { } element) Flatten(element, "", fields);

            var confirmation = new Confirmation(
                Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)), verb, path, door.Pattern, act, queried, fields, text,
                now, now + Lasts, ConfirmationState.Waiting)
            {
                Hash = hashBytes,
                Unescaped = unescaped,
                Query = query,
                Bytes = text is null ? [] : Encoding.UTF8.GetBytes(text),
            };
            _kept.Add(confirmation);
            Write("asked", confirmation);
            return (confirmation, ConfirmationAskRefusal.None, WaitingSentence);
        }
    }

    /// <summary>The person's answer, with the key: confirmed or refused, only while it waits.</summary>
    public (Confirmation? Confirmation, ConfirmationAnswerRefusal Refusal, string Message) Answer(string id, bool confirm)
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            var index = _kept.FindIndex(c => c.Id == id);
            if (index < 0) return (null, ConfirmationAnswerRefusal.NotFound, NotFoundSentence(id));

            var found = _kept[index];
            if (found.State != ConfirmationState.Waiting)
            {
                return (found, ConfirmationAnswerRefusal.NotWaiting, found.State switch
                {
                    ConfirmationState.Refused => RefusedSentence,
                    ConfirmationState.Expired => ExpiredSentence,
                    _ => ConfirmedAlreadySentence,
                });
            }

            var answered = found with { State = confirm ? ConfirmationState.Confirmed : ConfirmationState.Refused };
            _kept[index] = answered;
            Write(confirm ? "confirmed" : "refused", answered);
            return (answered, ConfirmationAnswerRefusal.None, confirm ? ConfirmedSentence : RefusedAnswer);
        }
    }

    /// <summary>
    /// Whether <paramref name="secret"/> grants this call, which uses its grant if it does: one the person confirmed, within
    /// its two minutes, never used, for this method, this door, this unescaped path, this query and these bytes. A call
    /// that differs uses nothing up, so the exact request still may.
    /// </summary>
    public bool Take(string secret, string method, string pattern, string unescaped, string query, byte[] body)
    {
        // A secret is made as a key is; one in any other form was never a client's, and grants nothing.
        if (secret.Length != 43 || PersonKey.Parse(secret) is null) return false;
        var hash = Encoding.ASCII.GetBytes(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret))));

        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            var index = _kept.FindIndex(c => c.State == ConfirmationState.Confirmed && CryptographicOperations.FixedTimeEquals(c.Hash, hash));
            if (index < 0) return false;

            var found = _kept[index];
            if (!string.Equals(found.Method, method, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(found.Pattern, pattern, StringComparison.Ordinal)
                || !string.Equals(found.Unescaped, unescaped, StringComparison.Ordinal)
                || !string.Equals(found.Query, query, StringComparison.Ordinal)
                || !found.Bytes.AsSpan().SequenceEqual(body))
            {
                return false;
            }

            _kept[index] = found with { State = ConfirmationState.Used };
            return true;
        }
    }

    // Expires what its two minutes ended for, once, saying so; forgets what ended two minutes before. Under the lock.
    private void Sweep(DateTimeOffset now)
    {
        for (var i = 0; i < _kept.Count; i++)
        {
            if (_kept[i] is { State: ConfirmationState.Waiting or ConfirmationState.Confirmed } open && now >= open.Expires)
            {
                _kept[i] = open with { State = ConfirmationState.Expired };
                Write("expired", _kept[i]);
            }
        }

        _kept.RemoveAll(c => now >= c.Expires + Kept);
    }

    // Codes only (design §5.1): what happened, and the door's method and pattern.
    private void Write(string state, Confirmation confirmation) =>
        log.Info(Event, ("state", state), ("method", confirmation.Method), ("route", confirmation.Pattern));

    /// <summary>
    /// Every value a body carries, so nothing a grant would carry is out of the person's sight: a nested one named by its
    /// path (<c>setUp.machine</c>, <c>units[0].id</c>), a string as it is, anything else as its JSON, and an empty object
    /// or list as itself.
    /// </summary>
    private static void Flatten(JsonElement element, string name, List<(string Name, string Value)> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var any = false;
                foreach (var property in element.EnumerateObject())
                {
                    any = true;
                    Flatten(property.Value, name.Length == 0 ? property.Name : $"{name}.{property.Name}", into);
                }

                if (!any && name.Length > 0) into.Add((name, "{}"));
                break;
            case JsonValueKind.Array:
                var count = 0;
                foreach (var item in element.EnumerateArray()) Flatten(item, $"{name}[{count++}]", into);
                if (count == 0) into.Add((name, "[]"));
                break;
            case JsonValueKind.String:
                into.Add((name, element.GetString()!));
                break;
            default:
                into.Add((name, element.GetRawText()));
                break;
        }
    }

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static (Confirmation?, ConfirmationAskRefusal, string) Shape(string sentence) =>
        (null, ConfirmationAskRefusal.Shape, sentence + " Nothing was asked.");

    /// <summary>The wire a confirmation is answered in: never its secret's hash.</summary>
    public static ConfirmationResponse Wire(Confirmation c) => new(
        c.Id, c.State.ToString().ToLowerInvariant(), c.Method, c.Path, c.Pattern, c.Act,
        [.. c.Values.Select(v => new ConfirmationFieldResponse(v.Name, v.Value))],
        [.. c.Fields.Select(f => new ConfirmationFieldResponse(f.Name, f.Value))],
        c.Body, c.Asked, c.Expires);
}
