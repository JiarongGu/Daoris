using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Daoris.Contracts.Confirmations;
using Daoris.Knowledge;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Daoris.Knowledge.Http;

/// <summary>Where a confirmation stands (PERSONDOOR1b; ONEWAY1n-i), as its poll answers it.</summary>
public enum ConfirmationState
{
    /// <summary>Asked, and waiting for the person in the window, however long they take.</summary>
    Waiting,

    /// <summary>The person confirmed it, and the host is making the request now.</summary>
    Making,

    /// <summary>Made at the Confirm, and its door took it.</summary>
    Made,

    /// <summary>The person refused it: nothing was made.</summary>
    Refused,

    /// <summary>Its asker withdrew it: nothing was made.</summary>
    Withdrawn,

    /// <summary>Made at the Confirm, and its door refused it: what it rested on moved while it waited.</summary>
    Stale,

    /// <summary>Made at the Confirm, and its door failed.</summary>
    Failed,
}

/// <summary>
/// One request an asker asked the person to confirm (PERSONDOOR1b; ONEWAY1n-i): what it is shown by, and the request as asked, which
/// the host makes at the Confirm. <see cref="Path"/> is the path and query exactly as asked, made with the path unescaped as its door
/// was found, the query as sent and the body to the byte. <see cref="Session"/> is the session the ask named, or null for a terminal's.
/// Once it ends, <see cref="Ended"/>, and for one made, the door's <see cref="Status"/>, its <see cref="Answer"/> and, where it refused,
/// its <see cref="Reason"/>. The asker's token is held by its hash and never answered.
/// </summary>
public sealed record Confirmation(
    string Id, string Method, string Path, string Pattern, string Act,
    IReadOnlyList<ConfirmationValue> Values, IReadOnlyList<ConfirmationValue> Fields, string? Body,
    DateTimeOffset Asked, string? Session, ConfirmationState State)
{
    public DateTimeOffset? Ended { get; init; }

    public int? Status { get; init; }

    public string? Answer { get; init; }

    public string? Reason { get; init; }

    internal byte[] Token { get; init; } = [];

    internal string Unescaped { get; init; } = "";

    internal string Query { get; init; } = "";

    /// <summary>The process a nameless ask came from, where the host could read it.</summary>
    internal int? Process { get; init; }

    internal byte[] Bytes { get; init; } = [];
}

/// <summary>What a request made at the Confirm was answered: its status and its body, as its door wrote them.</summary>
public sealed record MadeRequest(int Status, byte[] Body);

/// <summary>Why an ask was not taken: none, a host holding no key, a request out of shape, every place taken, or its asker's bound.</summary>
public enum ConfirmationAskRefusal
{
    None,
    NoKey,
    Shape,
    Full,
    Bound,
}

/// <summary>
/// Why an answer was not taken, or how a Confirm's request went: none, no such confirmation, one of an earlier start, one no longer
/// waiting, a request its door did not take, or a withdrawal without its asker's token.
/// </summary>
public enum ConfirmationAnswerRefusal
{
    None,
    NotFound,
    Restarted,
    NotWaiting,
    NotMade,
    NotAsker,
}

/// <summary>
/// The service's confirmation queue (PERSONDOOR1b, D156 point 4; ONEWAY1n-i, the oneway design §4 M4; the person-door design §4.2). A
/// keyless call to a person's or the driver's door is refused; its asker asks here with the exact request (method, path and body); the
/// window lists what waits by the door's name and the request's own fields; and the person confirms or refuses it with the key. It waits
/// with no clock until then, or until its asker withdraws it. At the Confirm the host makes the request itself, as asked, through its own
/// pipeline with its own key (<see cref="MakeAsync"/>), so every gate and handler judges it as it judges any call: a refusal it meets
/// there is its reason, read back to the asker, which polls its id.
/// </summary>
/// <remarks>
/// <para><b>In memory only.</b> Nothing here reaches the store, a file or an answer but what the window shows and the asker polls: a
/// restart ends what waits, and an asker polling an id of an earlier start reads <see cref="RestartedSentence"/>. One that ended is
/// forgotten <see cref="Kept"/> after its end, the oldest first past <see cref="MostEnded"/>.</para>
///
/// <para><b>Bounded</b> (PERSONDOOR1j): the host cannot tell the person's terminal from a session (the person-door design §4.1), so
/// an ask is placed by what it says of itself, as a body may narrow who acted and never widen it (§3.1). Asks naming a session fill at
/// most <see cref="Most"/> places; an ask naming none, the terminal's, may take one more, so sessions' asks never fill the last. Each
/// asker, a session or the terminal, holds at most <see cref="PerAsker"/> at once. Past either, the ask is refused, naming the asker's own
/// requests waiting, so it can withdraw one; one answered or withdrawn frees its place. A nameless ask is bound by its process
/// (<see cref="AskerProcess"/>): one process holds <see cref="PerProcess"/> of the terminal's places, so an agent leaving its session off
/// cannot hold both; where the process cannot be read, the nameless asks are the one terminal asker.</para>
///
/// <para><b>A session can ask, and cannot answer.</b> The ask is any caller's; the answers are the key's, and a withdrawal its
/// asker's alone, by the token its ask was answered with. Only a door that wants the key can be asked for, in the very form that wants
/// it, and never the confirmations' own doors.</para>
///
/// <para><b>The card shows only what binds</b> (after the review of PERSONDOOR1b). A body is asked for only with names its door binds,
/// each once, and a query only with names its handler reads (<see cref="DoorContract"/>): a name the binder ignores would be shown and
/// never kept, so a card would show one act while the request made did another. Each value is named by its JSON pointer and keeps its
/// JSON type.</para>
///
/// <para><b>Bounded before it is read</b>: an ask is read to <see cref="AskLimit"/> bytes, its body to <see cref="BodyLimit"/> and
/// <see cref="FieldLimit"/> values, and with its asker's places taken it is refused before its body is parsed.</para>
///
/// <para><b>The log</b> (the person-door design §5.1): one <see cref="Event"/> line as each is asked, made, found stale or failed,
/// refused or withdrawn, by the door's method and pattern. Never the id, the token, the path's values or the body. A refused ask is the
/// gate's <c>person.refused</c> line.</para>
///
/// <para><b>What waits says its change</b> (NOTIFY2j, D196 point 4, the notification design §2.5): a wait on <see cref="WaitAsync"/>
/// is answered as soon as what waits moves, or at its bound with what waits as it is, so the desktop's host holds one wait and tells
/// every window, and no window polls. The cursor is <c>&lt;epoch&gt;.&lt;n&gt;</c>, the epoch new at each start of the host; any other
/// cursor is answered at once.</para>
/// </remarks>
public sealed class PersonConfirmations(
    TimeProvider clock, MachineLog log, bool holds, Func<string, string, DoorContract?> contracts, Func<Confirmation, Task<MadeRequest>> make)
{
    /// <summary>The machine log's event for a confirmation asked or ended.</summary>
    public const string Event = "person.confirmation";

    /// <summary>How many asks naming a session may wait for the person at once.</summary>
    public const int Most = 5;

    /// <summary>How many more an ask naming no session, the terminal's, may take beyond <see cref="Most"/>.</summary>
    public const int Reserved = 1;

    /// <summary>How many one asker, a session or the terminal, may hold waiting at once.</summary>
    public const int PerAsker = 2;

    /// <summary>How many one process asking with no session may hold: one of the terminal's two, so it cannot hold both.</summary>
    public const int PerProcess = 1;

    /// <summary>How long one that ended is still answered to the asker that polls it.</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromMinutes(10);

    /// <summary>The most ended confirmations kept at once, the oldest forgotten first.</summary>
    public const int MostEnded = 64;

    /// <summary>The most bytes an asked request's body may be, in UTF-8; a made request's answer longer than it is kept unread.</summary>
    public const int BodyLimit = 64 * 1024;

    /// <summary>The most values a card may show: an asked body carrying more is refused before it is shown.</summary>
    public const int FieldLimit = 256;

    /// <summary>The most bytes an ask itself is read to, its asked body escaped inside it.</summary>
    public const int AskLimit = 512 * 1024;

    /// <summary>The most characters a session's name may be.</summary>
    public const int SessionLimit = 128;

    /// <summary>The error code of a request its door did not take at the Confirm.</summary>
    public const string NotMade = "not-made";

    /// <summary>The error code of a confirmation an earlier start of this host held.</summary>
    public const string Restarted = "restarted";

    /// <summary>The error code of a withdrawal without its asker's token.</summary>
    public const string NotAsker = "not-asker";

    /// <summary>What an asker says while it waits (design §4.2 step 3), answered with an ask so every client says it alike.</summary>
    public const string WaitingSentence = "Waiting for you to confirm this in Daoris's window…";

    /// <summary>The answer to the person's Confirm of a request its door took.</summary>
    public const string MadeAnswer = "Confirmed and done, as it was asked; its asker reads how it went.";

    /// <summary>The answer to the person's refusal.</summary>
    public const string RefusedAnswer = "Refused. Nothing was done, and its asker reads that you refused it.";

    /// <summary>The answer to an asker's withdrawal.</summary>
    public const string WithdrawnAnswer = "Withdrawn. Nothing was done, and it no longer waits for the person.";

    /// <summary>An ask on a host handed no key, whose doors want none (design §2.1).</summary>
    public const string NoKeySentence =
        "This service was started without a person key, so none of its doors waits for a confirmation: send the request "
        + "again as it was. Nothing was asked.";

    /// <summary>An answer to a confirmation the person confirmed already.</summary>
    public const string ConfirmedAlreadySentence = "This act was confirmed already; its asker reads how it went.";

    /// <summary>An answer to a confirmation being made now.</summary>
    public const string MakingSentence = "This act was confirmed already, and is being done now.";

    /// <summary>An answer to a confirmation the person refused.</summary>
    public const string RefusedSentence = "This act was refused already, and nothing was done.";

    /// <summary>An answer to a confirmation its asker withdrew.</summary>
    public const string WithdrawnSentence = "Its asker withdrew this act, and nothing was done.";

    /// <summary>A withdrawal without the token the ask was answered with.</summary>
    public const string NotAskerSentence =
        "Only the asker withdraws its ask, with the token its ask was answered with, and this call carried none of it. "
        + "Nothing was withdrawn.";

    /// <summary>What an asker polling an earlier start's confirmation reads (§4 M4).</summary>
    public const string RestartedSentence =
        "Not confirmed: Daoris restarted, and what waited for the person's confirmation ended with it. Ask again if it is still meant.";

    /// <summary>The most waits on what waits held at once; a ninth is answered at once.</summary>
    public const int MostWaits = 8;

    /// <summary>
    /// The door a wait on what waits is held at (NOTIFY2j): its time is its wait, as the change stream's is, so the request log never
    /// writes it as slow (STARTFAST1b).
    /// </summary>
    public const string WaitingRoute = "/api/confirmations/waiting";

    /// <summary>The longest a wait on what waits is held.</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly List<Confirmation> _kept = [];
    private readonly string _epoch = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
    private long _moves;
    private int _waits;
    private TaskCompletionSource _moved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The sentence for a confirmation this host holds no more, or never held.</summary>
    public static string NotFoundSentence(string id) =>
        $"No confirmation `{id}` is held here: one is forgotten ten minutes after it ends, and none outlives a start of this service.";

    /// <summary>The sentence the Confirm of a request its door did not take is answered with: the door's own words.</summary>
    public static string NotDoneSentence(string reason) => $"Not done: {reason}";

    /// <summary>
    /// The refusal of an ask while <paramref name="waiting"/> wait, as many as its asker may fill (<see cref="Most"/> for a session's,
    /// one more for the terminal's), naming the asker's own requests waiting.
    /// </summary>
    public static string FullSentence(int waiting, bool session, IReadOnlyList<Confirmation> yours) =>
        $"{waiting} acts already wait for the person's confirmation, as many as {(session ? "sessions' asks may fill" : "may")}, so "
        + "this one was not asked." + Yours(yours) + " Nothing was kept.";

    /// <summary>The refusal of an ask from an asker holding <see cref="PerAsker"/>, each of them named.</summary>
    public static string BoundSentence(IReadOnlyList<Confirmation> yours) =>
        $"This asker already has {yours.Count} acts waiting for the person's confirmation, as many as one asker may: {Named(yours)}. "
        + "Withdraw one, or wait for the person's answer, then ask again. Nothing was asked.";

    private static string Yours(IReadOnlyList<Confirmation> yours) =>
        yours.Count == 0 ? "" : $" This asker's own waiting: {Named(yours)}.";

    private static string Named(IReadOnlyList<Confirmation> each) => string.Join(", ", each.Select(c => $"`{c.Id}` ({c.Act})"));

    /// <summary>What waits for the person, oldest first.</summary>
    public IReadOnlyList<Confirmation> Waiting()
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            return [.. _kept.Where(c => c.State == ConfirmationState.Waiting)];
        }
    }

    /// <summary>
    /// What waits after <paramref name="since"/>, answered with the cursor to ask from next: at once where the cursor is not the
    /// current one (none, malformed, another start's, or behind), else as soon as what waits moves, or at <paramref name="wait"/> (at
    /// most <see cref="LongestWait"/>) as it is. A cancelled wait answers what waits then; a wait past <see cref="MostWaits"/> is
    /// answered at once.
    /// </summary>
    public async Task<(string Since, IReadOnlyList<Confirmation> Waiting)> WaitAsync(string? since, TimeSpan wait, CancellationToken ct = default)
    {
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        if (wait > LongestWait) wait = LongestWait;
        var arrived = clock.GetTimestamp();
        lock (_gate)
        {
            if (_waits >= MostWaits) return Told(out _);
            _waits++;
        }

        try
        {
            while (true)
            {
                var read = clock.GetUtcNow();
                var told = Told(out var moved);
                var left = wait - clock.GetElapsedTime(arrived);
                if (told.Since != since || left <= TimeSpan.Zero || ct.IsCancellationRequested) return told;

                // Its own timer, stopped before the answer: a wait answered holds nothing on the clock.
                var woken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var timer = clock.CreateTimer(_ => woken.TrySetResult(), null, left, Timeout.InfiniteTimeSpan);
                using var cancelled = ct.Register(() => woken.TrySetResult());
                // The clock may have passed the bound between its read and the timer's making, which then counts from later.
                if (clock.GetUtcNow() >= read + left) continue;
                await Task.WhenAny(moved, woken.Task).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_gate) _waits--;
        }
    }

    // What waits now and its cursor, swept first; and what the next move completes.
    private (string Since, IReadOnlyList<Confirmation> Waiting) Told(out Task moved)
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            moved = _moved.Task;
            return (_epoch + "." + _moves.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [.. _kept.Where(c => c.State == ConfirmationState.Waiting)]);
        }
    }

    // What waits moved: the cursor goes on, and every held wait reads it again. Under the lock.
    private void Moved()
    {
        _moves++;
        Interlocked.Exchange(ref _moved, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
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
    /// Why <paramref name="id"/> is not held: an id of an earlier start's form, which a restart ended (§4 M4), or one this host never
    /// held or forgot.
    /// </summary>
    public (Confirmation? Confirmation, ConfirmationAnswerRefusal Refusal, string Message) Missing(string id) =>
        id.Length == _epoch.Length + 16 && id.All(Uri.IsHexDigit) && !id.StartsWith(_epoch, StringComparison.OrdinalIgnoreCase)
            ? (null, ConfirmationAnswerRefusal.Restarted, RestartedSentence)
            : (null, ConfirmationAnswerRefusal.NotFound, NotFoundSentence(id));

    /// <summary>
    /// An ask: the request it was refused, exactly, and the session asking, if a session asks. Taken only on a host that holds a key,
    /// for a door that wants it in the form the body makes, and within the asker's places. Answered with the token that withdraws it,
    /// the only time it is answered.
    /// </summary>
    public (Confirmation? Confirmation, string? Token, ConfirmationAskRefusal Refusal, string Message) Ask(
        string? method, string? path, string? body, string? session, int? process = null)
    {
        if (!holds) return (null, null, ConfirmationAskRefusal.NoKey, NoKeySentence);

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
            || ReferenceEquals(door, PersonDoors.Refuse) || ReferenceEquals(door, PersonDoors.Withdraw))
        {
            return Shape("A confirmation is answered in Daoris's window, with the person's key; it is never itself confirmed.");
        }

        var text = string.IsNullOrEmpty(body) ? null : body;
        if (text is not null && Encoding.UTF8.GetByteCount(text) > BodyLimit)
        {
            return Shape($"`body` is over {BodyLimit / 1024} KB, more than a confirmation holds.");
        }

        var asker = string.IsNullOrWhiteSpace(session) ? null : session.Trim();
        if (asker is not null && (asker.Length > SessionLimit || asker.Any(char.IsControl)))
        {
            return Shape($"`session` names the session asking, as its record names it, at most {SessionLimit} characters; a terminal names none.");
        }

        // The places first, before anything is parsed: with its asker's places taken, an ask costs nothing to refuse.
        if (Admits(asker, process) is { } closed) return closed;

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

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        lock (_gate)
        {
            if (Admits(asker, process) is { } taken) return taken;

            var confirmation = new Confirmation(
                _epoch + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)), verb, path, door.Pattern, act, values, fields, text,
                clock.GetUtcNow(), asker, ConfirmationState.Waiting)
            {
                Token = SHA256.HashData(Encoding.ASCII.GetBytes(token)),
                Unescaped = unescaped,
                Query = query,
                Process = asker is null ? process : null,
                Bytes = text is null ? [] : Encoding.UTF8.GetBytes(text),
            };
            _kept.Add(confirmation);
            Moved();
            Write("asked", confirmation);
            return (confirmation, token, ConfirmationAskRefusal.None, WaitingSentence);
        }
    }

    // Whether an ask from this asker may wait now: past its own bound, or with every place it may fill taken, it is refused, naming
    // its own requests waiting. Judged once before the body is parsed and again as it is kept, since another ask may have come between.
    private (Confirmation?, string?, ConfirmationAskRefusal, string)? Admits(string? asker, int? process)
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            List<Confirmation> waiting = [.. _kept.Where(c => c.State == ConfirmationState.Waiting)];
            // A nameless ask whose process is read is that process's alone, holding one place of the terminal's two (PERSONDOOR1j).
            var byProcess = asker is null && process is not null;
            List<Confirmation> yours = [.. waiting.Where(c => string.Equals(c.Session, asker, StringComparison.Ordinal)
                && (asker is not null || c.Process == process))];
            if (yours.Count >= (byProcess ? PerProcess : PerAsker)) return (null, null, ConfirmationAskRefusal.Bound, BoundSentence(yours));
            return waiting.Count >= (asker is null ? Most + Reserved : Most)
                ? (null, null, ConfirmationAskRefusal.Full, FullSentence(waiting.Count, asker is not null, yours))
                : null;
        }
    }

    /// <summary>
    /// The person's Confirm, with the key, only while it waits: the request is made as it was asked (<see cref="MakeAsync"/>), and it
    /// ends made where its door took it, or stale or failed with its door's own reason, which the Confirm is answered with as
    /// <see cref="ConfirmationAnswerRefusal.NotMade"/>.
    /// </summary>
    public async Task<(Confirmation? Confirmation, ConfirmationAnswerRefusal Refusal, string Message)> ConfirmAsync(string id)
    {
        Confirmation taken;
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            var index = _kept.FindIndex(c => c.Id == id);
            if (index < 0) return Missing(id);
            if (_kept[index] is not { State: ConfirmationState.Waiting } found) return NotWaiting(_kept[index]);

            taken = _kept[index] = found with { State = ConfirmationState.Making };
            Moved();
        }

        MadeRequest made;
        try
        {
            made = await make(taken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            made = new MadeRequest(StatusCodes.Status500InternalServerError, []);
        }

        var state = made.Status switch
        {
            < 400 => ConfirmationState.Made,
            < 500 => ConfirmationState.Stale,
            _ => ConfirmationState.Failed,
        };
        Confirmation ended;
        lock (_gate)
        {
            ended = taken with
            {
                State = state,
                Ended = clock.GetUtcNow(),
                Status = made.Status,
                Answer = made.Body.Length <= BodyLimit ? Encoding.UTF8.GetString(made.Body) : null,
                Reason = state == ConfirmationState.Made ? null : ReasonOf(made),
            };
            var index = _kept.FindIndex(c => c.Id == id);
            if (index >= 0) _kept[index] = ended;
            Write(state.ToString().ToLowerInvariant(), ended);
        }

        return state == ConfirmationState.Made
            ? (ended, ConfirmationAnswerRefusal.None, MadeAnswer)
            : (ended, ConfirmationAnswerRefusal.NotMade, NotDoneSentence(ended.Reason!));
    }

    /// <summary>The person's refusal, with the key, only while it waits: nothing is made.</summary>
    public (Confirmation? Confirmation, ConfirmationAnswerRefusal Refusal, string Message) Refuse(string id) =>
        End(id, token: null, ConfirmationState.Refused, RefusedAnswer);

    /// <summary>Its asker's withdrawal, with the token its ask was answered with, only while it waits: nothing is made.</summary>
    public (Confirmation? Confirmation, ConfirmationAnswerRefusal Refusal, string Message) Withdraw(string id, string? token) =>
        End(id, token ?? "", ConfirmationState.Withdrawn, WithdrawnAnswer);

    // Ends one still waiting as `state`, with nothing made; a withdrawal's token must be its asker's, judged before its state.
    private (Confirmation?, ConfirmationAnswerRefusal, string) End(string id, string? token, ConfirmationState state, string answer)
    {
        lock (_gate)
        {
            Sweep(clock.GetUtcNow());
            var index = _kept.FindIndex(c => c.Id == id);
            if (index < 0) return Missing(id);

            var found = _kept[index];
            if (token is not null
                && !CryptographicOperations.FixedTimeEquals(found.Token, SHA256.HashData(Encoding.UTF8.GetBytes(token))))
            {
                return (null, ConfirmationAnswerRefusal.NotAsker, NotAskerSentence);
            }

            if (found.State != ConfirmationState.Waiting) return NotWaiting(found);

            var ended = _kept[index] = found with { State = state, Ended = clock.GetUtcNow() };
            Moved();
            Write(state.ToString().ToLowerInvariant(), ended);
            return (ended, ConfirmationAnswerRefusal.None, answer);
        }
    }

    private static (Confirmation?, ConfirmationAnswerRefusal, string) NotWaiting(Confirmation found) =>
        (found, ConfirmationAnswerRefusal.NotWaiting, found.State switch
        {
            ConfirmationState.Refused => RefusedSentence,
            ConfirmationState.Withdrawn => WithdrawnSentence,
            ConfirmationState.Making => MakingSentence,
            _ => ConfirmedAlreadySentence,
        });

    // The door's own sentence, as every refusal of this host carries one; else its words, short; else its status alone.
    private static string ReasonOf(MadeRequest made)
    {
        try
        {
            using var answer = JsonDocument.Parse(made.Body);
            if (answer.RootElement is { ValueKind: JsonValueKind.Object } root
                && root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                && error.GetString() is { Length: > 0 } sentence)
            {
                return sentence;
            }
        }
        catch (JsonException)
        {
        }

        var text = made.Body.Length <= 512 ? Encoding.UTF8.GetString(made.Body).Trim() : "";
        return text.Length > 0 ? text : $"its door answered {made.Status}, with no sentence.";
    }

    /// <summary>
    /// The request <paramref name="asked"/>, made through the host's whole pipeline (<paramref name="pipeline"/>, routing included) in
    /// this process, as it was asked: its method, its path unescaped as its door was found and as sent, its query as sent, and its body
    /// to the byte, labelled UTF-8 JSON. It carries the host's own key, which never leaves the process, so the gate judges it as it
    /// judges the person's window; it comes from no address, as the in-process server's calls do. Its answer is read whole.
    /// </summary>
    public static async Task<MadeRequest> MakeAsync(
        RequestDelegate pipeline, IServiceProvider services, PersonKey key, Confirmation asked, CancellationToken ct)
    {
        IHeaderDictionary headers = new HeaderDictionary();
        key.PresentOn(headers);
        if (asked.Bytes.Length > 0) headers.ContentType = "application/json";
        headers.ContentLength = asked.Bytes.Length;

        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature
        {
            Protocol = "HTTP/1.1",
            Scheme = "http",
            Method = asked.Method,
            PathBase = "",
            Path = asked.Unescaped,
            QueryString = asked.Query,
            RawTarget = asked.Path,
            Headers = headers,
            Body = new MemoryStream(asked.Bytes, writable: false),
        });
        var answer = new MemoryStream();
        features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(answer));
        features.Set<IHttpConnectionFeature>(new HttpConnectionFeature { LocalIpAddress = IPAddress.Loopback });
        features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(asked.Bytes.Length > 0));
        features.Set<IHttpRequestLifetimeFeature>(new HttpRequestLifetimeFeature { RequestAborted = ct });

        await using var scope = services.CreateAsyncScope();
        var context = new DefaultHttpContext(features) { RequestServices = scope.ServiceProvider };
        await pipeline(context).ConfigureAwait(false);
        return new MadeRequest(context.Response.StatusCode, answer.ToArray());
    }

    private sealed class BodyDetection(bool body) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => body;
    }

    /// <summary>
    /// An ask, read here rather than bound so it is read no further than <see cref="AskLimit"/> and a byte: a JSON body, which a page
    /// on another origin sends only with CORS's leave (design §4.2), parsed by the host's own options. The ask, or the refusal to
    /// answer instead.
    /// </summary>
    public static async Task<(AskConfirmation? Ask, Routes.Refusal? Refusal)> ReadAskAsync(HttpContext http, CancellationToken ct)
    {
        if (!Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(http.Request.ContentType, out var type)
            || !type.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return (null, new Routes.Refusal(
                StatusCodes.Status415UnsupportedMediaType,
                new ErrorResponse("An ask for a confirmation is a JSON body: `method`, `path`, `body` and `session`. Nothing was asked.")));
        }

        if (await ReadAtMostAsync(http.Request.Body, http.Request.ContentLength, AskLimit, ct) is not { } read)
        {
            return (null, new Routes.Refusal(
                StatusCodes.Status413PayloadTooLarge,
                new ErrorResponse($"An ask for a confirmation is at most {AskLimit / 1024} KB. Nothing was asked.")));
        }

        var json = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        AskConfirmation? ask = null;
        try
        {
            ask = JsonSerializer.Deserialize(read, (JsonTypeInfo<AskConfirmation>)json.GetTypeInfo(typeof(AskConfirmation)));
        }
        catch (JsonException)
        {
        }

        return ask is null
            ? (null, Routes.Refusal.BadRequest(
                "An ask for a confirmation is a JSON object: `method`, `path`, `body` and `session`. Nothing was asked."))
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

    // Forgets what ended `Kept` before, and past `MostEnded` the oldest ended first. What waits moves no clock. Under the lock.
    private void Sweep(DateTimeOffset now)
    {
        _kept.RemoveAll(c => c.Ended is { } ended && now >= ended + Kept);
        var over = _kept.Count(c => c.Ended is not null) - MostEnded;
        if (over > 0)
        {
            var oldest = _kept.Where(c => c.Ended is not null).OrderBy(c => c.Ended).Take(over).ToHashSet();
            _kept.RemoveAll(oldest.Contains);
        }
    }

    // Codes only (design §5.1): what happened, and the door's method and pattern.
    private void Write(string state, Confirmation confirmation) =>
        log.Info(Event, ("state", state), ("method", confirmation.Method), ("route", confirmation.Pattern));

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static (Confirmation?, string?, ConfirmationAskRefusal, string) Shape(string sentence) =>
        (null, null, ConfirmationAskRefusal.Shape, sentence + " Nothing was asked.");

    /// <summary>The wire a confirmation is answered in: never its asker's token.</summary>
    public static ConfirmationResponse Wire(Confirmation c) => new(
        c.Id, c.State.ToString().ToLowerInvariant(), c.Method, c.Path, c.Pattern, c.Act,
        [.. c.Values.Select(v => new ConfirmationFieldResponse(v.Name, v.Type, v.Value))],
        [.. c.Fields.Select(f => new ConfirmationFieldResponse(f.Name, f.Type, f.Value))],
        c.Body, c.Asked, c.Session, c.Ended, c.Status, c.Answer, c.Reason);
}
