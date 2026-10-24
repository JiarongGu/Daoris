using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// What a person's press rests on, read at its ask and read again at its Confirm (CONFIRMWAIT1a-i, the confirm-wait design §5.1): a
/// wire's word, told in lower case as a <c>changed</c> refusal's <c>ground</c>, added to and never renamed.
/// </summary>
public enum PipeGroundKind
{
    /// <summary>A session's record: its state and when it last moved.</summary>
    Session,

    /// <summary>A setting's value where it is kept.</summary>
    Setting,

    /// <summary>That a repository (or workspace) is registered on this machine.</summary>
    Repository,

    /// <summary>A quest's state.</summary>
    Quest,

    /// <summary>An ask's state.</summary>
    Ask,

    /// <summary>The fingerprint of the fields a plan read's row names.</summary>
    Plan,

    /// <summary>The tip of a branch the press lands, hands off or discards.</summary>
    Branch,

    /// <summary>That the held wait the press answers is still open.</summary>
    Wait,
}

/// <summary>
/// One reading of a ground: what it was read about (<see cref="Subject"/>: a session's id, a repository's name, a branch, a plan's
/// route), the value compared (<see cref="Value"/>: a state and a time, a fingerprint, a tip), and the code the person's reason
/// names it by (<see cref="Words"/>: a session's or a quest's state), never the asker's words.
/// </summary>
public sealed record PipeReading(PipeGroundKind Kind, string Subject, string Value, string? Words = null);

/// <summary>
/// The ground that moved while a press waited, as the person and the terminal are told it (§5.1, §8.3): <see cref="Reason"/> the
/// catalogue's code (<c>session</c>, <c>turn</c>, <c>setting</c>, <c>repository</c>, <c>quest</c>, <c>ask</c>, <c>plan</c>,
/// <c>branch</c>, <c>wait</c>; the page's <c>person.changed.&lt;reason&gt;</c>), and its values by name (<c>then</c>, <c>now</c>,
/// <c>repository</c>, <c>branch</c>), codes the instance read.
/// </summary>
public sealed record PipeGroundChange(string Reason, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>The terminal's sentence for it, the verb's own English (§8.3's last line).</summary>
    public string Sentence => Reason switch
    {
        "session" => $"the session moved on while this waited: it was {Value("then")}, it is {Value("now")}",
        "turn" => "the session ran another turn while this waited",
        "setting" => "what this would set changed while this waited",
        "repository" => $"the repository {Value("repository")} is no longer registered on this machine",
        "quest" => $"the quest moved on while this waited: it was {Value("then")}, it is {Value("now")}",
        "ask" => "the ask was closed while this waited",
        "plan" => "what this would act on changed while this waited; look at it again and ask again",
        "branch" => $"the branch {Value("branch")} gained or lost commits while this waited",
        _ => "what this answers no longer waits: it was answered or ended",
    };

    private string Value(string name) => Values.TryGetValue(name, out var value) ? value : "unknown";
}

/// <summary>
/// What the grounds are read from (§5.2): the loop's readers, handed to the pipe as the standing confirmation's are. Each answers what
/// it read now; a throw, or no answer within the ground's bound, is a read that failed.
/// </summary>
public interface IPipeGroundWorld
{
    /// <summary>A session's record by its id: its state's public spelling and when it last moved; null where no record has the id.</summary>
    Task<(string State, DateTimeOffset Updated)?> SessionAsync(string id, CancellationToken cancellationToken);

    /// <summary>Whether a repository or a workspace of that name is registered on this machine.</summary>
    Task<bool> RegisteredAsync(string name, CancellationToken cancellationToken);

    /// <summary>A quest's status by its id; null where the service has none.</summary>
    Task<string?> QuestAsync(string id, CancellationToken cancellationToken);

    /// <summary>An ask's state by its id; null where the service has none.</summary>
    Task<string?> AskAsync(string id, CancellationToken cancellationToken);

    /// <summary>The commit <paramref name="branch"/> points at in <paramref name="repository"/>'s checkout; null where there is no such ref.</summary>
    Task<string?> TipAsync(string repository, string branch, CancellationToken cancellationToken);

    /// <summary>The commit a session's tree's head points at, read as the landing reads its tree; null where it has none.</summary>
    Task<string?> SessionHeadAsync(string session, CancellationToken cancellationToken);

    /// <summary>
    /// A read of <paramref name="module"/>'s own, called in process with <paramref name="payload"/> as a window would send it (§5.2): its
    /// answer's data, or a throw where it refused.
    /// </summary>
    Task<JsonElement> ReadAsync(string module, string route, object? payload, CancellationToken cancellationToken);

    /// <summary>
    /// A wait the loop holds that no read of the bridge lists by the press's id (<see cref="LoopGround.Waits"/>): its state now, or null
    /// where there is none.
    /// </summary>
    Task<string?> HeldAsync(string wait, JsonElement? payload, CancellationToken cancellationToken);

    /// <summary>
    /// What a press acts on that only its files say (<see cref="LoopGround.Files"/>): a fingerprint of the files it copies or runs, of a
    /// tree's uncommitted paths, of a file, or of the notices it dismisses.
    /// </summary>
    Task<string> FingerprintAsync(string what, JsonElement? payload, CancellationToken cancellationToken);
}

/// <summary>One ground a route names: how it is read from the press's payload, and how long the read may take.</summary>
public abstract class PipeGround(PipeGroundKind kind, TimeSpan? bound = null)
{
    /// <summary>A state's read, and a read of one file (§5.1, §5.2).</summary>
    public static readonly TimeSpan StateBound = TimeSpan.FromSeconds(5);

    /// <summary>The bridge's own bound for a read it passes none for (<c>bridge/call.ts</c>'s 30 seconds), "as the page reads it".</summary>
    public static readonly TimeSpan PageBound = TimeSpan.FromSeconds(30);

    public PipeGroundKind Kind { get; } = kind;

    /// <summary>How long its read may take, never shorter than the page's own for the same read.</summary>
    public TimeSpan Bound { get; } = bound ?? StateBound;

    /// <summary>What it reads now about <paramref name="payload"/>'s press.</summary>
    public abstract Task<IReadOnlyList<PipeReading>> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken);

    /// <summary>The payload fields it names, which the census test holds to what the route's handler reads.</summary>
    public abstract IReadOnlyList<string> Fields { get; }

    /// <summary>A setting's ground: whether the value it reads is already what the press sets (§5.1's <c>already</c>). Others never.</summary>
    public virtual bool Already(IReadOnlyList<PipeReading> read, JsonElement? payload) => false;

    /// <summary>The text of <paramref name="payload"/>'s field <paramref name="name"/>, as the handler reads it (a string), or null.</summary>
    public static string? Text(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } named && named.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Each string of <paramref name="payload"/>'s array field <paramref name="name"/>.</summary>
    public static IReadOnlyList<string> Texts(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } named && named.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
            : [];

    /// <summary>A quest's or an ask's id as its handler takes it, a leading <c>#</c> trimmed; null where none is named.</summary>
    public static string? Id(string? text) => text?.Trim().TrimStart('#') is { Length: > 0 } id ? id : null;

    /// <summary>A value of <paramref name="payload"/> as its JSON, or null where absent: what a fingerprint or a comparison takes.</summary>
    public static string? Raw(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } named && named.TryGetProperty(name, out var value) ? value.GetRawText() : null;

    /// <summary>A SHA-256 over <paramref name="parts"/> in order, each length-prefixed so two lists never print alike.</summary>
    public static string Fingerprint(IEnumerable<string?> parts)
    {
        var text = new StringBuilder();
        foreach (var part in parts)
        {
            if (part is null) text.Append("-;");
            else text.Append(part.Length).Append(':').Append(part).Append(';');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}

/// <summary>The session or sessions a payload's field names: <c>session</c>, its state and when its record last moved.</summary>
public sealed class SessionGround(string named, bool many = false) : PipeGround(PipeGroundKind.Session)
{
    public override IReadOnlyList<string> Fields => [named];

    public override async Task<IReadOnlyList<PipeReading>> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ids = many ? Texts(payload, named) : Text(payload, named) is { Length: > 0 } one ? [one] : [];
        var read = new List<PipeReading>();
        foreach (var id in ids)
        {
            var record = await world.SessionAsync(id, cancellationToken).ConfigureAwait(false);
            read.Add(record is { } found
                ? new PipeReading(Kind, id, $"{found.State}|{found.Updated.UtcTicks}", found.State)
                : new PipeReading(Kind, id, "", "gone"));
        }

        return read;
    }
}

/// <summary>A repository or workspace the payload names, registered on this machine.</summary>
public sealed class RepositoryGround(params string[] named) : PipeGround(PipeGroundKind.Repository)
{
    public override IReadOnlyList<string> Fields => named;

    public override async Task<IReadOnlyList<PipeReading>> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken) =>
        named.Select(each => Text(payload, each)).FirstOrDefault(each => !string.IsNullOrWhiteSpace(each)) is { } name
            ? [new PipeReading(Kind, name, await world.RegisteredAsync(name, cancellationToken).ConfigureAwait(false) ? "registered" : "")]
            : [];
}

/// <summary>A quest the payload names: its status.</summary>
public sealed class QuestGround(string named) : PipeGround(PipeGroundKind.Quest)
{
    public override IReadOnlyList<string> Fields => [named];

    public override async Task<IReadOnlyList<PipeReading>> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (Id(Text(payload, named)) is not { } id) return [];
        var status = await world.QuestAsync(id, cancellationToken).ConfigureAwait(false);
        return [new PipeReading(Kind, id, status ?? "", status ?? "gone")];
    }
}

/// <summary>An ask the payload names: its state.</summary>
public sealed class AskGround(string named) : PipeGround(PipeGroundKind.Ask)
{
    public override IReadOnlyList<string> Fields => [named];

    public override async Task<IReadOnlyList<PipeReading>> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (Id(Text(payload, named)) is not { } id) return [];
        var state = await world.AskAsync(id, cancellationToken).ConfigureAwait(false);
        return [new PipeReading(Kind, id, state ?? "", state ?? "gone")];
    }
}

/// <summary>
/// A branch's tip: the refs <paramref name="refs"/> picks from the payload, each by its repository and branch; or, with
/// <paramref name="sessionHead"/>, the head of the tree of the session the field names.
/// </summary>
public sealed class BranchGround(
    IReadOnlyList<string> fields, Func<JsonElement?, IReadOnlyList<(string Repository, string Branch)>>? refs = null, string? sessionHead = null)
    : PipeGround(PipeGroundKind.Branch)
{
    /// <summary>The head of the tree of the session <paramref name="field"/> names.</summary>
    public static BranchGround HeadOf(string field) => new([field], sessionHead: field);

    /// <summary>The branch <paramref name="branch"/> of the repository <paramref name="repository"/>, each a payload field.</summary>
    public static BranchGround Named(string repository, params string[] branch) =>
        new([repository, .. branch], payload => Text(payload, repository) is { Length: > 0 } where
            && branch.Select(each => Text(payload, each)).FirstOrDefault(each => !string.IsNullOrEmpty(each)) is { } name
            ? [(where, name)]
            : []);

    public override IReadOnlyList<string> Fields => fields;

    public override async Task<IReadOnlyList<PipeReading>> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (sessionHead is not null)
        {
            if (Text(payload, sessionHead) is not { Length: > 0 } session) return [];
            var head = await world.SessionHeadAsync(session, cancellationToken).ConfigureAwait(false);
            return [new PipeReading(Kind, session, head ?? "")];
        }

        var read = new List<PipeReading>();
        foreach (var (repository, branch) in refs!(payload))
        {
            var tip = await world.TipAsync(repository, branch, cancellationToken).ConfigureAwait(false);
            read.Add(new PipeReading(Kind, branch, tip ?? ""));
        }

        return read;
    }
}

/// <summary>
/// A read of the driver's own, projected (§5.1): a <c>plan</c> takes the fingerprint of the fields its row names; a <c>wait</c> whether
/// the wait the press answers is still open under the same id; a <c>setting</c> the value it reads where it is kept, compared at the
/// ask with the value the press sets (<see cref="Sets"/>). <paramref name="ask"/> builds the read's payload from the press's;
/// <paramref name="take"/> projects its answer, with the press's payload beside it, to the compared text.
/// </summary>
public sealed class ReadGround(
    PipeGroundKind kind, string route, IReadOnlyList<string> fields, Func<JsonElement?, object?> ask, Func<JsonElement, JsonElement?, string> take,
    TimeSpan? bound = null) : PipeGround(kind, bound)
{
    /// <summary>The read route it calls.</summary>
    public string Route { get; } = route;

    /// <summary>The module whose read it is: the driver's, unless named (<see cref="Of"/>).</summary>
    public string Module { get; private set; } = DriverModule.Name;

    /// <summary>For a <c>setting</c>: the value the press sets, as <paramref name="take"/> projects the read's; null where it says none.</summary>
    public Func<JsonElement?, string?>? Sets { get; init; }

    /// <summary>Whether the press's form names this ground (<c>SESSION_INPUT</c>'s <c>question</c>); every form where none is given.</summary>
    public Func<JsonElement?, bool>? When { get; private set; }

    /// <summary>This ground, read only for a press whose form <paramref name="when"/> names it.</summary>
    public ReadGround Only(Func<JsonElement?, bool> when)
    {
        When = when;
        return this;
    }

    /// <summary>This ground, a read of <paramref name="module"/>'s.</summary>
    public ReadGround Of(string module)
    {
        Module = module;
        return this;
    }

    public override IReadOnlyList<string> Fields => fields;

    public override async Task<IReadOnlyList<PipeReading>> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (When is { } named && !named(payload)) return [];
        var answer = await world.ReadAsync(Module, Route, ask(payload), cancellationToken).ConfigureAwait(false);
        return [new PipeReading(Kind, Route, take(answer, payload))];
    }

    public override bool Already(IReadOnlyList<PipeReading> read, JsonElement? payload) =>
        Kind == PipeGroundKind.Setting && Sets?.Invoke(payload) is { } sets && read.Count == 1 && read[0].Value == sets;
}

/// <summary>
/// A ground the loop reads from its own records, where no read of the bridge says it by the press's id: a <c>wait</c> it holds
/// (<see cref="Waits"/>), or a <c>plan</c> only files say (<see cref="Files"/>).
/// </summary>
public sealed class LoopGround(PipeGroundKind kind, string what, IReadOnlyList<string> fields, TimeSpan? bound = null) : PipeGround(kind, bound)
{
    /// <summary>The waits <see cref="IPipeGroundWorld.HeldAsync"/> reads.</summary>
    public static readonly IReadOnlyList<string> Waits = ["opinion", "workflow-node", "go-ahead", "trust", "help"];

    /// <summary>The files <see cref="IPipeGroundWorld.FingerprintAsync"/> reads.</summary>
    public static readonly IReadOnlyList<string> Files =
        ["plugin-folder", "plugin-new", "plugin-try", "plugin-offer", "plugin-update", "tree-uncommitted", "file", "declaration", "notices"];

    /// <summary>A wait the loop holds, by the payload's <paramref name="fields"/>.</summary>
    public static LoopGround Wait(string wait, params string[] fields) => new(PipeGroundKind.Wait, wait, fields);

    /// <summary>What the press's files are, by the payload's <paramref name="fields"/>; a plugin's or a tree's within the page's bound.</summary>
    public static LoopGround Plan(string files, TimeSpan bound, params string[] fields) => new(PipeGroundKind.Plan, files, fields, bound);

    /// <summary>Which wait or files it reads.</summary>
    public string What { get; } = what;

    /// <summary>Whether the press's form names this ground (<c>DISCARD_SESSION_TREE</c>'s <c>force</c>); every form where none is given.</summary>
    public Func<JsonElement?, bool>? When { get; private set; }

    /// <summary>This ground, read only for a press whose form <paramref name="when"/> names it.</summary>
    public LoopGround Only(Func<JsonElement?, bool> when)
    {
        When = when;
        return this;
    }

    public override IReadOnlyList<string> Fields => fields;

    public override async Task<IReadOnlyList<PipeReading>> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (When is { } named && !named(payload)) return [];
        var value = Kind == PipeGroundKind.Wait
            ? await world.HeldAsync(What, payload, cancellationToken).ConfigureAwait(false) ?? ""
            : await world.FingerprintAsync(What, payload, cancellationToken).ConfigureAwait(false);
        return [new PipeReading(Kind, What, value)];
    }
}

/// <summary>
/// A route's row of grounds (§5.1): the grounds it names, or none for a <c>timeless</c> press, which rests on nothing a wait moves; read
/// at the ask, read again at the Confirm, and judged by <see cref="Judge"/>.
/// </summary>
public sealed class PipeGroundRow(IReadOnlyList<PipeGround> grounds, string? timeless = null)
{
    /// <summary>The grounds it names; empty for a timeless press.</summary>
    public IReadOnlyList<PipeGround> Grounds { get; } = grounds;

    /// <summary>Why it is timeless (it deletes nothing, moves no line or branch, starts no work), or null where it names grounds.</summary>
    public string? Timeless { get; } = timeless;

    /// <summary>The longest one reading of all its grounds may take: the slowest ground's bound, the grounds read one after another.</summary>
    public TimeSpan Bound => Grounds.Aggregate(TimeSpan.Zero, (sum, each) => sum + each.Bound);

    /// <summary>
    /// Every ground read now, each bounded by its <see cref="PipeGround.Bound"/> on <paramref name="clock"/>: a throw or the bound passing is
    /// a read that failed.
    /// </summary>
    public async Task<IReadOnlyList<PipeReading>> ReadAsync(
        IPipeGroundWorld? world, JsonElement? payload, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (Grounds.Count == 0) return [];
        if (world is null) throw new InvalidOperationException("this Daoris has no readers for a press's grounds.");
        var read = new List<PipeReading>();
        foreach (var ground in Grounds)
        {
            using var bounded = new CancellationTokenSource(ground.Bound, clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bounded.Token);
            read.AddRange(await ground.ReadAsync(world, payload, linked.Token).WaitAsync(ground.Bound, clock, cancellationToken).ConfigureAwait(false));
        }

        return read;
    }

    /// <summary>Whether a <c>setting</c> ground already reads what the press sets (§5.1's <c>already</c>).</summary>
    public bool Already(IReadOnlyList<PipeReading> read, JsonElement? payload) =>
        Grounds.OfType<ReadGround>().Any(ground =>
            ground.Already([.. read.Where(each => each.Kind == ground.Kind && each.Subject == ground.Route)], payload));

    /// <summary>
    /// The readings at the ask compared with those now (§5.2): the first ground whose reading moved, as the reason the person and the
    /// terminal are told, or null where all hold. A change made before the ask is no change: the ask's reading is the baseline.
    /// </summary>
    public static PipeGroundChange? Judge(IReadOnlyList<PipeReading> then, IReadOnlyList<PipeReading> now)
    {
        foreach (var asked in then)
        {
            var current = now.FirstOrDefault(each => each.Kind == asked.Kind && each.Subject == asked.Subject);
            if (current is not null && current.Value == asked.Value) continue;
            return asked.Kind switch
            {
                PipeGroundKind.Session when current is not null && current.Words == asked.Words => new("turn", new Dictionary<string, string>()),
                PipeGroundKind.Session or PipeGroundKind.Quest => new(Code(asked.Kind), new Dictionary<string, string>
                {
                    ["then"] = asked.Words ?? "gone",
                    ["now"] = current?.Words ?? "gone",
                }),
                PipeGroundKind.Repository => new("repository", new Dictionary<string, string> { ["repository"] = asked.Subject }),
                PipeGroundKind.Branch => new("branch", new Dictionary<string, string> { ["branch"] = asked.Subject }),
                _ => new(Code(asked.Kind), new Dictionary<string, string>()),
            };
        }

        return null;
    }

    /// <summary>A ground's code on the wire, in lower case.</summary>
    public static string Code(PipeGroundKind kind) => kind.ToString().ToLowerInvariant();
}
