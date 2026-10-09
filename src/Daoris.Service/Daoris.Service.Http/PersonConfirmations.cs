using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Daoris.Knowledge;
using Microsoft.Extensions.Options;

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
    IReadOnlyList<ConfirmationValue> Values, IReadOnlyList<ConfirmationValue> Fields, string? Body,
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

/// <summary>
/// A grant whose secret and address hold (PERSONDOOR1b, after its review): what the gate reads of the call's body before
/// it is taken, and no more than <see cref="Length"/> bytes and one.
/// </summary>
public sealed record Grantable(string Id, byte[] Hash, int Length);

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
/// <para><b>The card shows only what binds</b> (after the review of PERSONDOOR1b). A body is asked for only with names its
/// door binds, each once, and a query only with names its handler reads (<see cref="DoorContract"/>): a name the binder
/// ignores would be shown and never kept, and could leave the door's own value unbound, so a card would show one act
/// while its grant did another. Each value is named by its JSON pointer and keeps its JSON type.</para>
///
/// <para><b>Bounded before it is read</b>: an ask is read to <see cref="AskLimit"/> bytes, its body to
/// <see cref="BodyLimit"/> and <see cref="FieldLimit"/> values, and while five wait a sixth is refused before its body is
/// parsed. A grant's secret and address are checked before a byte of its call's body is read, and the body is read no
/// further than the confirmed one and a byte.</para>
///
/// <para><b>The secret</b> is made by the client like a key: 32 random bytes in base64url without padding. The host holds
/// only its SHA-256 (lowercase hex as asked), never answers that, and never logs either: a grant whose secret is not in
/// that form grants nothing.</para>
///
/// <para><b>The log</b> (§5.1): one <see cref="Event"/> line as each is asked, confirmed, refused or expires, by the door's
/// method and pattern. Never the id, the secret, its hash, the path's values or the body. A grant used writes nothing; a
/// grant refused, and a sixth ask, are the gate's <c>person.refused</c> lines.</para>
/// </remarks>
public sealed class PersonConfirmations(TimeProvider clock, MachineLog log, bool holds, Func<string, string, DoorContract?> contracts)
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

    /// <summary>The most bytes an asked request's body may be, in UTF-8.</summary>
    public const int BodyLimit = 64 * 1024;

    /// <summary>The most values a card may show: an asked body carrying more is refused before it is shown.</summary>
    public const int FieldLimit = 256;

    /// <summary>The most bytes an ask itself is read to, its asked body escaped inside it.</summary>
    public const int AskLimit = 512 * 1024;

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
        if (PersonDoors.Resolve(verb, unescaped) is not { } resolved || resolved.Door.Class == DoorClass.Shared
            || contracts(verb, resolved.Door.Pattern) is not { } contract)
        {
            return Shape($"`{verb} {path}` is no door of this service, so nothing there waits for a confirmation.");
        }

        var (door, route) = resolved;
        if (ReferenceEquals(door, PersonDoors.AskConfirmation) || ReferenceEquals(door, PersonDoors.Confirm)
            || ReferenceEquals(door, PersonDoors.Refuse))
        {
            return Shape("A confirmation is answered in Daoris's window, with the person's key; it is never itself confirmed.");
        }

        var text = string.IsNullOrEmpty(body) ? null : body;
        if (text is not null && Encoding.UTF8.GetByteCount(text) > BodyLimit)
        {
            return Shape($"`body` is over {BodyLimit / 1024} KB, more than a confirmation holds.");
        }

        var hash = (secretSha256 ?? "").Trim().ToLowerInvariant();
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
        {
            return Shape("`secretSha256` is the SHA-256 of a secret the client made, 64 hexadecimal characters; the secret itself is never sent until it is used.");
        }

        // The places first, before anything is parsed: while five wait, a sixth costs nothing to refuse.
        var hashBytes = Encoding.ASCII.GetBytes(hash);
        if (Admits(hashBytes) is { } closed) return closed;

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

        var (values, fields, refusal) = contract.Read(route, query, parsed, FieldLimit);
        if (refusal is not null) return Shape(refusal);

        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (Admits(hashBytes) is { } taken) return taken;

            var confirmation = new Confirmation(
                Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)), verb, path, door.Pattern, act, values, fields, text,
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

    // Whether an ask with this hash may wait now: a hash asked already, or five waiting, refuse it. Judged once before the
    // body is parsed and again as it is kept, since another ask may have taken the place between.
    private (Confirmation?, ConfirmationAskRefusal, string)? Admits(byte[] hash)
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            if (_kept.Any(c => CryptographicOperations.FixedTimeEquals(c.Hash, hash)))
            {
                return Shape("This secret's hash was asked already: a client makes a new secret for each ask.");
            }

            return _kept.Count(c => c.State == ConfirmationState.Waiting) >= Most
                ? (null, ConfirmationAskRefusal.Full, PersonDoors.FullSentence)
                : null;
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
    /// The grant <paramref name="secret"/> holds for this call's address, before anything of its body is read: one the
    /// person confirmed, within its two minutes, never used, for this method, this door, this unescaped path and this
    /// query; null for none, so the gate refuses the call unread. Uses nothing up.
    /// </summary>
    public Grantable? Expecting(string secret, string method, string pattern, string unescaped, string query)
    {
        // A secret is made as a key is; one in any other form was never a client's, and grants nothing.
        if (secret.Length != 43 || PersonKey.Parse(secret) is null) return null;
        var hash = Encoding.ASCII.GetBytes(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret))));

        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            var found = _kept.Find(c => c.State == ConfirmationState.Confirmed && CryptographicOperations.FixedTimeEquals(c.Hash, hash));
            return found is not null
                && string.Equals(found.Method, method, StringComparison.OrdinalIgnoreCase)
                && string.Equals(found.Pattern, pattern, StringComparison.Ordinal)
                && string.Equals(found.Unescaped, unescaped, StringComparison.Ordinal)
                && string.Equals(found.Query, query, StringComparison.Ordinal)
                    ? new Grantable(found.Id, hash, found.Bytes.Length)
                    : null;
        }
    }

    /// <summary>
    /// Whether <paramref name="grant"/> covers a call whose body is <paramref name="body"/>, which uses it if it does: still
    /// confirmed, in time and unused as it is taken, and its bytes the confirmed body's to the byte. A call that differs uses
    /// nothing up, so the exact request still may.
    /// </summary>
    public bool Take(Grantable grant, ReadOnlySpan<byte> body)
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            var index = _kept.FindIndex(c => c.Id == grant.Id);
            if (index < 0) return false;

            var found = _kept[index];
            if (found.State != ConfirmationState.Confirmed || !CryptographicOperations.FixedTimeEquals(found.Hash, grant.Hash)
                || !found.Bytes.AsSpan().SequenceEqual(body))
            {
                return false;
            }

            _kept[index] = found with { State = ConfirmationState.Used };
            return true;
        }
    }

    /// <summary>
    /// An ask, read here rather than bound so it is read no further than <see cref="AskLimit"/> and a byte: a JSON body,
    /// which a page on another origin sends only with CORS's leave (design §4.2), parsed by the host's own options. The ask,
    /// or the refusal to answer instead.
    /// </summary>
    public static async Task<(ConfirmationAskRequest? Ask, IResult? Refusal)> ReadAskAsync(HttpContext http, CancellationToken ct)
    {
        if (!Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(http.Request.ContentType, out var type)
            || !type.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return (null, Results.Json(
                new ErrorResponse("An ask for a confirmation is a JSON body: `method`, `path`, `body` and `secretSha256`. Nothing was asked."),
                statusCode: StatusCodes.Status415UnsupportedMediaType));
        }

        if (await ReadAtMostAsync(http.Request.Body, http.Request.ContentLength, AskLimit, ct) is not { } read)
        {
            return (null, Results.Json(
                new ErrorResponse($"An ask for a confirmation is at most {AskLimit / 1024} KB. Nothing was asked."),
                statusCode: StatusCodes.Status413PayloadTooLarge));
        }

        var json = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        ConfirmationAskRequest? ask = null;
        try
        {
            ask = JsonSerializer.Deserialize(read, (JsonTypeInfo<ConfirmationAskRequest>)json.GetTypeInfo(typeof(ConfirmationAskRequest)));
        }
        catch (JsonException)
        {
        }

        return ask is null
            ? (null, Results.BadRequest(new ErrorResponse(
                "An ask for a confirmation is a JSON object: `method`, `path`, `body` and `secretSha256`. Nothing was asked.")))
            : (ask, null);
    }

    /// <summary>
    /// A body read whole when it is at most <paramref name="most"/> bytes, and null when it is longer, read no further than
    /// one byte past it; a declared length past it is refused unread.
    /// </summary>
    public static async Task<byte[]?> ReadAtMostAsync(Stream body, long? declared, int most, CancellationToken ct)
    {
        if (declared > most) return null;
        var buffer = new byte[(declared ?? most) + 1];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await body.ReadAsync(buffer.AsMemory(filled), ct);
            if (read == 0) break;
            filled += read;
        }

        return filled > most || (declared is { } length && filled != length) ? null : buffer[..filled];
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

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static (Confirmation?, ConfirmationAskRefusal, string) Shape(string sentence) =>
        (null, ConfirmationAskRefusal.Shape, sentence + " Nothing was asked.");

    /// <summary>The wire a confirmation is answered in: never its secret's hash.</summary>
    public static ConfirmationResponse Wire(Confirmation c) => new(
        c.Id, c.State.ToString().ToLowerInvariant(), c.Method, c.Path, c.Pattern, c.Act,
        [.. c.Values.Select(v => new ConfirmationFieldResponse(v.Name, v.Type, v.Value))],
        [.. c.Fields.Select(f => new ConfirmationFieldResponse(f.Name, f.Type, f.Value))],
        c.Body, c.Asked, c.Expires);
}
