using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>One option an agent offered on a permission request, as the wire names it: its id, its label and its kind.</summary>
/// <param name="Id">The <c>optionId</c> the answer names.</param>
/// <param name="Name">The agent's own label for it, shown as written.</param>
/// <param name="Kind">The protocol's kind: <c>allow_once</c>, <c>allow_always</c>, <c>reject_once</c> or <c>reject_always</c>.</param>
public sealed record PermissionChoice(string Id, string Name, string Kind)
{
    public const string AllowOnce = "allow_once";
    public const string AllowAlways = "allow_always";
    public const string RejectOnce = "reject_once";
    public const string RejectAlways = "reject_always";

    /// <summary>Whether choosing it lets the call run: the one allow the person may choose, this call alone.</summary>
    public bool Allows => Kind == AllowOnce;

    /// <summary>
    /// Whether the person may choose it (AUTONOMY1b3a): this call's allow, and either rejection. A standing allow is a rule for the
    /// rest of the session, which is a rule proposal's to change (D74), never one call's press; a kind this build does not know is
    /// never read as an allow.
    /// </summary>
    public bool Choosable => Kind is AllowOnce or RejectOnce or RejectAlways;
}

/// <summary>A permission request held for the person (AUTONOMY1b3a), as the page and the terminal read it.</summary>
/// <param name="Session">The Daoris session whose agent asked.</param>
/// <param name="Request">The request's id on the agent's wire, which the answer names.</param>
/// <param name="Call">The tool call it asks for, by the wire's id, or null where the request named none.</param>
/// <param name="Title">The call's title as the agent wrote it, bounded (the agent's words, which may hold a path); null where it gave none.</param>
/// <param name="Choices">What the person may choose, in the agent's order: never a standing allow, and no allow at all where none was offered.</param>
/// <param name="Since">When it was held, by this machine's clock.</param>
public sealed record HeldPermission(
    string Session, string Request, string? Call, string? Title, IReadOnlyList<PermissionChoice> Choices, DateTimeOffset Since)
{
    /// <summary>The call's kind as the wire names it (<c>execute</c>, <c>edit</c>, …), or null.</summary>
    public string? Kind { get; init; }
}

/// <summary>Why a hold ended: one of <see cref="PermissionWhy"/>'s.</summary>
/// <param name="Chosen">The option the person chose, or null: a release, or a refusal where the agent offered no rejection.</param>
public sealed record PermissionEnd(PermissionChoice? Chosen, string Why);

/// <summary>Why a held permission request ended.</summary>
public static class PermissionWhy
{
    /// <summary>The person answered it.</summary>
    public const string Answered = "answered";

    /// <summary>The turn it was asked in was cancelled: the person's stop of the turn, or words sent that stop it.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>The session ended: it closed, its stream ended (a stop's kill among the causes), or its reader was let go.</summary>
    public const string Ended = "ended";

    /// <summary>Nobody answered it within its bound (<see cref="HeldPermissions.Bound"/>).</summary>
    public const string Bound = "bound";
}

/// <summary>The person's answer as the registry took it: whether it stands, and where it does not, its code and a sentence.</summary>
/// <param name="Why">Null where it stands; else <c>unheld</c>, <c>unoffered</c> or <c>standing</c>.</param>
public sealed record PermissionAnswer(bool Answered, string? Why, string Message);

/// <summary>
/// The permission requests this process's driven sessions hold for the person (AUTONOMY1b3a, D184 point 3): under a repository's
/// <c>asking</c> permission prompts, the protocol door holds each tool request its agent sends instead of refusing it, until the person
/// answers it with an option the agent offered, or the session releases it.
/// </summary>
/// <remarks>
/// <para><b>In memory alone.</b> A held request is the agent process's own open request: a restart ends that process, and the request
/// with it, so nothing here is written. Keyed by the session and the request's id on its wire.</para>
///
/// <para><b>Never an invented allow.</b> The answer names an option the agent offered, by its id; a standing allow is refused (a rule is
/// proposed, D74), and so is an option it never offered. A refusal naming no option takes the offered one-time rejection, else none,
/// which the wire says as <c>cancelled</c>. Daoris answers nothing allowing by itself: only the person's press does (D37, D52).</para>
///
/// <para><b>Bounded</b> (REV3: an unanswered request is a turn hung for ever): each hold is released at <see cref="Bound"/>, and at
/// its session's cancel, close or stream's end, whichever comes first.</para>
///
/// <para><b>Told as it moves</b>: <see cref="Changed"/> names a session whenever what it holds moved, after the registry's lock is let
/// go, as <see cref="RunningCalls.Changed"/> does (AUTONOMY1b3b's waiting mark reads it).</para>
/// </remarks>
public sealed class HeldPermissions
{
    /// <summary>
    /// How long a request waits for the person before it is refused: half the session's default 30-minute run, so a refusal at the
    /// bound still leaves the session time to go on with what it can do, and long enough for a person at the window to read and press.
    /// </summary>
    public static readonly TimeSpan Bound = TimeSpan.FromMinutes(15);

    private static readonly ConditionalWeakTable<SessionEvents, HeldPermissions> Registries = new();

    private readonly object _gate = new();
    private readonly Dictionary<(string Session, string Request), Entry> _held = new();

    /// <summary>A session whose held requests moved: one held, answered or released. A hearer's failure is its own.</summary>
    public event Action<string>? Changed;

    /// <summary>The registry of the sessions writing to <paramref name="events"/>: the driver's and the page's, one per process.</summary>
    public static HeldPermissions Of(SessionEvents events) => Registries.GetValue(events, _ => new HeldPermissions());

    /// <summary>The door one protocol session holds its requests through, each released at <paramref name="bound"/> (<see cref="Bound"/>).</summary>
    public PermissionHolds For(string session, TimeSpan? bound = null) => new(this, session, bound ?? Bound);

    /// <summary>What the session holds for the person, oldest first.</summary>
    public IReadOnlyList<HeldPermission> Held(string session)
    {
        lock (_gate)
        {
            return [.. _held.Values.Where(hold => string.Equals(hold.Seen.Session, session, StringComparison.OrdinalIgnoreCase))
                .OrderBy(hold => hold.Seen.Since).Select(hold => hold.Seen)];
        }
    }

    /// <summary>Every request held for the person, oldest first.</summary>
    public IReadOnlyList<HeldPermission> Now()
    {
        lock (_gate)
        {
            return [.. _held.Values.OrderBy(hold => hold.Seen.Since).Select(hold => hold.Seen)];
        }
    }

    /// <summary>
    /// The person's answer to <paramref name="request"/> of <paramref name="session"/>: an offered option by its id, or null to refuse.
    /// It ends the hold where it stands; otherwise the hold waits on as it was, and the answer says why by its code.
    /// </summary>
    public PermissionAnswer Answer(string session, string request, string? option)
    {
        PermissionChoice? chosen;
        lock (_gate)
        {
            if (!_held.TryGetValue(Key(session, request), out var hold))
            {
                return new(false, "unheld",
                    $"Session `{session}` holds no permission request `{request}` for the person: it was answered or released, or it was never held "
                    + "here. Nothing was answered.");
            }

            if (option is null)
            {
                chosen = hold.Offered.FirstOrDefault(choice => choice.Kind == PermissionChoice.RejectOnce);
            }
            else if (hold.Offered.FirstOrDefault(choice => choice.Id == option) is { Kind: PermissionChoice.AllowAlways } standing)
            {
                return new(false, "standing",
                    $"`{standing.Name}` would allow it for the rest of the session, which is a rule, not this call: a rule is proposed and "
                    + "accepted as one (D74). Nothing was answered.");
            }
            else if (hold.Offered.FirstOrDefault(choice => choice.Id == option && choice.Choosable) is { } offered)
            {
                chosen = offered;
            }
            else
            {
                return new(false, "unoffered", $"`{option}` is not an option the agent offered for this request. Nothing was answered.");
            }
        }

        if (!End(session, request, new PermissionEnd(chosen, PermissionWhy.Answered)))
        {
            return new(false, "unheld", $"Session `{session}`'s permission request `{request}` was released first. Nothing was answered.");
        }

        return new(true, null, chosen is null ? "Refused." : $"Answered `{chosen.Name}`.");
    }

    /// <summary>Hold a request, or null where the same one is already held.</summary>
    internal Task<PermissionEnd>? Hold(
        PermissionHolds door, string request, string? call, string? title, IReadOnlyList<PermissionChoice> offered, string? kind)
    {
        var seen = new HeldPermission(door.Session, request, call, title, [.. offered.Where(choice => choice.Choosable)], DateTimeOffset.UtcNow)
        {
            Kind = kind,
        };
        var hold = new Entry(door, seen, offered, new Timer(_ => End(door.Session, request, new PermissionEnd(null, PermissionWhy.Bound))));
        lock (_gate)
        {
            if (!_held.TryAdd(Key(door.Session, request), hold))
            {
                hold.Bound.Dispose();
                return null;
            }

            // Armed while it is held, under the lock, so no end can come before it and leave it running.
            hold.Bound.Change(door.Bound, Timeout.InfiniteTimeSpan);
        }

        Tell(door.Session);
        return hold.Ended.Task;
    }

    /// <summary>End every hold <paramref name="door"/> made, saying why.</summary>
    internal void ReleaseAll(PermissionHolds door, string why)
    {
        List<string> requests;
        lock (_gate)
        {
            requests = [.. _held.Values.Where(hold => ReferenceEquals(hold.Door, door)).Select(hold => hold.Seen.Request)];
        }

        foreach (var request in requests) End(door.Session, request, new PermissionEnd(null, why));
    }

    /// <summary>End a hold, once: false where it had already ended.</summary>
    private bool End(string session, string request, PermissionEnd end)
    {
        Entry? hold;
        lock (_gate)
        {
            if (!_held.Remove(Key(session, request), out hold)) return false;
        }

        hold.Bound.Dispose();
        hold.Ended.TrySetResult(end);
        Tell(session);
        return true;
    }

    private static (string, string) Key(string session, string request) => (session.ToLowerInvariant(), request);

    private void Tell(string session)
    {
        if (Changed is not { } changed) return;
        foreach (var hearer in changed.GetInvocationList().Cast<Action<string>>())
        {
            try
            {
                hearer(session);
            }
            catch (Exception)
            {
                // A hearer's failure is its own: the hold moved, and the other hearers still hear.
            }
        }
    }

    private sealed class Entry(PermissionHolds door, HeldPermission seen, IReadOnlyList<PermissionChoice> offered, Timer bound)
    {
        public PermissionHolds Door { get; } = door;

        public HeldPermission Seen { get; } = seen;

        public IReadOnlyList<PermissionChoice> Offered { get; } = offered;

        // Its bound, disposed at its end: a timer may be disposed from its own callback, which is how the bound ends it.
        public Timer Bound { get; } = bound;

        // Its waiter goes on off the thread that ended it: never inside a release's caller, or the registry's hearers.
        public TaskCompletionSource<PermissionEnd> Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

/// <summary>
/// One protocol session's door into <see cref="HeldPermissions"/> (AUTONOMY1b3a): the requests it holds for the person, and their
/// release when the session's turn is cancelled or the session ends. Made per session run, so a release ends only its own.
/// </summary>
public sealed class PermissionHolds
{
    private readonly HeldPermissions _registry;

    internal PermissionHolds(HeldPermissions registry, string session, TimeSpan bound)
    {
        _registry = registry;
        Session = session;
        Bound = bound;
    }

    /// <summary>The Daoris session whose requests these are.</summary>
    public string Session { get; }

    /// <summary>How long each waits for the person before it is released (<see cref="HeldPermissions.Bound"/>).</summary>
    public TimeSpan Bound { get; }

    /// <summary>
    /// Hold a request for the person: what ends it, the person's answer or a release, or null where the same request is already held.
    /// </summary>
    /// <param name="offered">Every option the agent offered, in its order: what the person may choose is read from them.</param>
    /// <param name="kind">The call's kind as the wire names it, or null.</param>
    public Task<PermissionEnd>? Hold(string request, string? call, string? title, IReadOnlyList<PermissionChoice> offered, string? kind = null) =>
        _registry.Hold(this, request, call, title, offered, kind);

    /// <summary>End every request this door holds, saying why: one of <see cref="PermissionWhy"/>'s.</summary>
    public void ReleaseAll(string why) => _registry.ReleaseAll(this, why);
}
