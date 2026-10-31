using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>The phases of bringing sessions together (LINESTOGETHER1d, the review-flow design §4), as stored.</summary>
public static class TogetherPhase
{
    /// <summary>Recorded and its words said; the head's turn has not begun.</summary>
    public const string Asked = "asked";

    /// <summary>The head's turn replays the members.</summary>
    public const string Replaying = "replaying";

    /// <summary>The head's tip is accounted, or its last accounting failed.</summary>
    public const string Checking = "checking";

    /// <summary>Checked: every member change accounted at <see cref="TogetherOperation.Checked"/>.</summary>
    public const string Ready = "ready";

    /// <summary>The combined Accept wrote its attempt; git may be making it.</summary>
    public const string Landing = "landing";

    /// <summary>A landing's outcome not yet proved after a restart: the members stay bound.</summary>
    public const string Uncertain = "uncertain";

    public const string Landed = "landed";
    public const string Stale = "stale";
    public const string LetGo = "let-go";

    /// <summary>Whether an operation in this phase still binds its trees.</summary>
    public static bool Open(string phase) => phase is not (Landed or Stale or LetGo);

    /// <summary>Before <i>landing</i>: what <i>Let them go</i> may end.</summary>
    public static bool BeforeLanding(string phase) => phase is Asked or Replaying or Checking or Ready;
}

/// <summary>Why an operation went stale, or what its last check failed on, as stored.</summary>
public static class TogetherWhy
{
    public const string AnotherWord = "another-word";
    public const string HandedOver = "handed-over";
    public const string Stopped = "stopped";
    public const string MemberMoved = "member-moved";
    public const string HeadGone = "head-gone";
}

/// <summary>How a check accounted one path of a member's commit, as stored (the review-flow design §4, <i>Checking</i>).</summary>
public static class TogetherAccounts
{
    /// <summary>The commit's change, held by the head's tip by content or patch.</summary>
    public const string Held = "held";

    /// <summary>Tried alone onto the tip, the path comes out as the tip has it.</summary>
    public const string Contained = "contained";

    /// <summary>A path the line's rules settle: a generated file, the declared backlog or a row table.</summary>
    public const string Settled = "settled";

    /// <summary>Unproven, and a head commit since the press changed it and names the commit in a <c>Supersedes:</c> trailer.</summary>
    public const string Superseded = "superseded";

    /// <summary>Changed cleanly, and the tip lacks it.</summary>
    public const string Missing = "missing";

    /// <summary>In conflict with the tip, and nothing supersedes it.</summary>
    public const string Unproven = "unproven";

    /// <summary>Whether a path so accounted lets the operation be checked.</summary>
    public static bool Proves(string how) => how is Held or Contained or Settled or Superseded;
}

/// <summary>The forms a combined Accept lands by: the rule's branch, its squash, or the merge into the line.</summary>
public static class TogetherForms
{
    public const string Branch = LandingForm.Branch;
    public const string Squash = "squash";
    public const string Merge = LandingForm.Merge;
}

/// <summary>The head an operation is bound to: its line, the record it is on now, and its tree.</summary>
/// <param name="Line">The line's first record: what the binding follows as the line goes on.</param>
/// <param name="Record">The head's record now; moves to a successor its own word opened.</param>
/// <param name="PressTip">The head's tip when pressed: its commits since are the replay's, the only ones a <c>Supersedes:</c> trailer counts in.</param>
public sealed record TogetherHead(string Line, string Record, string Tree, string Branch, string PressTip);

/// <summary>One session brought to the head, pinned at the press.</summary>
public sealed record TogetherMember(string Session, string Tree, string Branch, string Base, string Tip)
{
    public string? Quest { get; init; }
    public string? Unit { get; init; }

    /// <summary>The submission its gates were read for, pinned with them.</summary>
    public string? Submission { get; init; }
}

/// <summary>One path of a member's commit as a check accounted it.</summary>
/// <param name="How">One of <see cref="TogetherAccounts"/>.</param>
public sealed record TogetherPath(string Session, string Commit, string Path, string How);

/// <summary>
/// One landing attempt of the combined Accept, written before anything is made, then told what came of it: what a restart settles
/// <i>landing</i> by (the review-flow design §4, <i>Restart</i>).
/// </summary>
/// <param name="Form">One of <see cref="TogetherForms"/>.</param>
/// <param name="Target">The ref the attempt moves or makes: a branch, or the line.</param>
/// <param name="Expected">The tip expected at <paramref name="Target"/>; null where the attempt makes the ref.</param>
/// <param name="Pinned">The head's checked tip the attempt lands.</param>
public sealed record TogetherAttempt(string Form, string Target, string? Expected, string Pinned, DateTimeOffset At)
{
    /// <summary>A merge's replayed tip, the one its request is admitted at.</summary>
    public string? Replayed { get; init; }

    /// <summary>The tip the target came to, once known.</summary>
    public string? Resulting { get; init; }

    /// <summary>The integration request the attempt was admitted as, once known.</summary>
    public string? Request { get; init; }
}

/// <summary>
/// One operation bringing a repository's sessions together (LINESTOGETHER1d): the head, each member pinned, the phase, the last
/// accounting and the landing attempt. The inclusion record of every form: a merge keeps no landing record of its own.
/// </summary>
public sealed record TogetherOperation(
    string Id, string Repository, TogetherHead Head, IReadOnlyList<TogetherMember> Members, string Phase, DateTimeOffset Since)
{
    /// <summary>The head's tip the last proved check accounted every member change at.</summary>
    public string? Checked { get; init; }

    /// <summary>The head's tip the last check was tried at, proved or not: a failed check is not tried again there.</summary>
    public string? Tried { get; init; }

    /// <summary>The last check's accounting, every member path; empty before one ran.</summary>
    public IReadOnlyList<TogetherPath> Account { get; init; } = [];

    /// <summary>Why it went stale, or what the last check failed on (<see cref="TogetherWhy"/>, or the check's own words).</summary>
    public string? Why { get; init; }

    public TogetherAttempt? Attempt { get; init; }

    /// <summary>How many times <i>Ask again</i> said the words once more.</summary>
    public int Asks { get; init; }

    /// <summary>The key its words are said under, so a retry says them once; each <i>Ask again</i> its own.</summary>
    [JsonIgnore]
    public string Key => Asks == 0 ? $"together:{Id}" : $"together:{Id}:{Asks}";

    [JsonIgnore]
    public bool IsOpen => TogetherPhase.Open(Phase);

    /// <summary>Whether the operation binds <paramref name="session"/>, as its head or a member.</summary>
    public bool Holds(string session) =>
        Same(Head.Record, session) || Same(Head.Line, session) || Members.Any(each => Same(each.Session, session));

    /// <summary>Whether the operation binds <paramref name="tree"/>, the head's or a member's.</summary>
    public bool Binds(string tree) => SameTree(Head.Tree, tree) || Members.Any(each => SameTree(each.Tree, tree));

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    internal static bool SameTree(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
}

/// <summary>An operation on the wire for one session's page (<c>TOGETHER</c>): no tree, no path, no account of its own.</summary>
public static class TogetherWire
{
    public static object Of(TogetherOperation operation, string session) => new
    {
        operation.Id,
        operation.Phase,
        Role = operation.Members.Any(each => string.Equals(each.Session, session, StringComparison.OrdinalIgnoreCase)) ? "member" : "head",
        Head = operation.Head.Record,
        Members = operation.Members.Select(each => new { each.Session, each.Branch, each.Tip }),
        operation.Why,
        Into = operation.Phase == TogetherPhase.Landed ? operation.Attempt?.Target : null,
        Line = operation.Attempt?.Form == TogetherForms.Merge,
    };
}

/// <summary>
/// The operations bringing sessions together (<see cref="TogetherOperation"/>): <c>&lt;home&gt;/sessions/together.json</c>, machine-local,
/// edited under its record lock, the third of the door's lock order. Each change is a compare of the phase it expects, so two doors
/// cannot both move one operation: the second reads null and does nothing.
/// </summary>
/// <remarks>A file that does not read binds nothing and refuses a new press, since an open operation may be unread there.</remarks>
public sealed class TogetherOperations(string home)
{
    public const string FileName = "together.json";

    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string FilePath => DaorisHome.Entry(home, HomeEntry.Sessions, FileName);

    public string LockPath => RecordLock.PathOf(home, FileName);

    /// <summary>Every operation kept, oldest first; none where the file is missing.</summary>
    /// <exception cref="IOException">The file is there and does not read.</exception>
    public IReadOnlyList<TogetherOperation> All()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            if (JsonNode.Parse(AtomicFile.ReadText(FilePath))?["operations"] is not JsonArray array) throw Unread(null);
            return [.. array.Select(each => each.Deserialize<TogetherOperation>(Options)).OfType<TogetherOperation>()];
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw Unread(error);
        }
    }

    public TogetherOperation? Of(string id) => All().FirstOrDefault(each => each.Id == id);

    /// <summary>The open operation <paramref name="session"/> is in, as head or member; null where none.</summary>
    public TogetherOperation? OpenFor(string session) => All().FirstOrDefault(each => each.IsOpen && each.Holds(session));

    /// <summary>
    /// The open operation binding <paramref name="tree"/>, read under the record lock as every landing, sync, update and discard reads
    /// it; null where none.
    /// </summary>
    /// <exception cref="IOException">The lock was held past its wait, or the file does not read: the caller binds nothing it could not read.</exception>
    public TogetherOperation? Binding(string tree)
    {
        lock (Gate)
        {
            using var held = RecordLock.Take(LockPath);
            return All().FirstOrDefault(each => each.IsOpen && each.Binds(tree));
        }
    }

    /// <summary>The open operation binding <paramref name="branch"/> of <paramref name="repository"/>, read under the record lock; null where none.</summary>
    /// <exception cref="IOException">The lock was held past its wait, or the file does not read.</exception>
    public TogetherOperation? BindingBranch(string repository, string branch)
    {
        lock (Gate)
        {
            using var held = RecordLock.Take(LockPath);
            return All().FirstOrDefault(each => each.IsOpen && string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                && (Same(each.Head.Branch, branch) || each.Members.Any(member => Same(member.Branch, branch))));
        }
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>
    /// Record a press at <i>asked</i>: refused, with its sentence, where the head or a member is in an open operation already, or a member
    /// tree is bound. Nothing is written where it is refused.
    /// </summary>
    public (TogetherOperation? Opened, string? Refused) Open(
        string repository, TogetherHead head, IReadOnlyList<TogetherMember> members, DateTimeOffset at, string? id = null)
    {
        var operation = new TogetherOperation(id ?? NewId(), repository, head, members, TogetherPhase.Asked, at);
        string? refused = null;
        Edit(all =>
        {
            if (all.FirstOrDefault(each => each.IsOpen && (each.Holds(head.Record)
                    || members.Any(member => each.Holds(member.Session) || each.Binds(member.Tree)) || each.Binds(head.Tree))) is { } open)
            {
                refused = $"a session here is being brought together already ({open.Id}, {open.Phase}): let that one go or land it first.";
                return null;
            }

            return [.. all, operation];
        });
        return refused is null ? (operation, null) : (null, refused);
    }

    /// <summary>The head's turn began on the words: <i>asked</i> to <i>replaying</i>.</summary>
    public TogetherOperation? Replaying(string id, DateTimeOffset at) =>
        Move(id, [TogetherPhase.Asked], each => each with { Phase = TogetherPhase.Replaying, Since = at });

    /// <summary>The head's turn ended: <i>replaying</i> (or a word never seen begun) to <i>checking</i>.</summary>
    public TogetherOperation? TurnEnded(string id, DateTimeOffset at) =>
        Move(id, [TogetherPhase.Asked, TogetherPhase.Replaying], each => each with { Phase = TogetherPhase.Checking, Since = at });

    /// <summary>
    /// A check's accounting at <paramref name="tip"/>: proved, <i>ready to accept</i> pinned there; failed, it stays <i>checking</i> with
    /// what failed, for <i>Ask again</i>.
    /// </summary>
    public TogetherOperation? Checked(string id, string tip, IReadOnlyList<TogetherPath> account, string? failed, DateTimeOffset at) =>
        Move(id, [TogetherPhase.Checking], each => failed is null
            ? each with { Phase = TogetherPhase.Ready, Checked = tip, Tried = tip, Account = account, Why = null, Since = at }
            : each with { Checked = null, Tried = tip, Account = account, Why = failed, Since = at });

    /// <summary><i>Ask again</i> after a failed check: back to <i>asked</i>, its words said again under a fresh key.</summary>
    public TogetherOperation? AskedAgain(string id, DateTimeOffset at) =>
        Move(id, [TogetherPhase.Checking], each => each with { Phase = TogetherPhase.Asked, Why = null, Asks = each.Asks + 1, Since = at });

    /// <summary>
    /// The head's tip read at a look: moved after <i>checked</i>, back to <i>checking</i>; anything else unchanged (null).
    /// </summary>
    public TogetherOperation? HeadAt(string id, string tip, DateTimeOffset at) =>
        Move(id, [TogetherPhase.Ready], each => string.Equals(each.Checked, tip, StringComparison.OrdinalIgnoreCase)
            ? null
            : each with { Phase = TogetherPhase.Checking, Checked = null, Since = at });

    /// <summary>A member's tip read at a look: moved before <i>checked</i>, the operation ends <i>stale</i>, holding nothing.</summary>
    public TogetherOperation? MemberAt(string id, string session, string tip, DateTimeOffset at) =>
        Move(id, [TogetherPhase.Asked, TogetherPhase.Replaying, TogetherPhase.Checking], each =>
            each.Members.FirstOrDefault(member => string.Equals(member.Session, session, StringComparison.OrdinalIgnoreCase)) is { } member
            && !string.Equals(member.Tip, tip, StringComparison.OrdinalIgnoreCase)
                ? each with { Phase = TogetherPhase.Stale, Why = TogetherWhy.MemberMoved, Since = at }
                : null);

    /// <summary>
    /// The head's line went on past <see cref="TogetherHead.Record"/> to <paramref name="successor"/>: carried there where its own keyed
    /// word opened it (<paramref name="byOwnWord"/>), else <i>stale</i> for <paramref name="why"/>. Before <i>landing</i> only.
    /// </summary>
    public TogetherOperation? LineMoved(string id, string successor, bool byOwnWord, string why, DateTimeOffset at) =>
        Move(id, [TogetherPhase.Asked, TogetherPhase.Replaying, TogetherPhase.Checking, TogetherPhase.Ready], each => byOwnWord
            ? each with { Head = each.Head with { Record = successor } }
            : each with { Phase = TogetherPhase.Stale, Why = why, Since = at });

    /// <summary><i>Let them go</i>: ends it before <i>landing</i>; what the head made stays.</summary>
    public TogetherOperation? LetGo(string id, DateTimeOffset at) =>
        Move(id, [TogetherPhase.Asked, TogetherPhase.Replaying, TogetherPhase.Checking, TogetherPhase.Ready],
            each => each with { Phase = TogetherPhase.LetGo, Since = at });

    /// <summary>
    /// The combined Accept's attempt, written before anything is made: <i>ready to accept</i> at the pinned tip to <i>landing</i>.
    /// Null where it is not ready there, so nothing is made.
    /// </summary>
    public TogetherOperation? Landing(string id, TogetherAttempt attempt) =>
        Move(id, [TogetherPhase.Ready], each => string.Equals(each.Checked, attempt.Pinned, StringComparison.OrdinalIgnoreCase)
            ? each with { Phase = TogetherPhase.Landing, Attempt = attempt, Since = attempt.At }
            : null);

    /// <summary>What the attempt came to, as known: its resulting tip, or the request admitted.</summary>
    public TogetherOperation? Correlate(string id, string? resulting = null, string? request = null) =>
        Move(id, [TogetherPhase.Landing, TogetherPhase.Uncertain], each => each.Attempt is not { } attempt ? null : each with
        {
            Attempt = attempt with { Resulting = resulting ?? attempt.Resulting, Request = request ?? attempt.Request },
        });

    /// <summary>
    /// A landing or uncertain operation settled (<see cref="TogetherSettle"/>): <i>landed</i>, still <i>landing</i>, back to <i>ready to
    /// accept</i> where proved unmade, else <i>uncertain</i>, its members bound.
    /// </summary>
    public TogetherOperation? Settle(string id, TogetherSettled settled, DateTimeOffset at, string? request = null) =>
        Move(id, [TogetherPhase.Landing, TogetherPhase.Uncertain], each =>
        {
            var attempt = each.Attempt is { } kept && request is not null ? kept with { Request = request } : each.Attempt;
            return settled switch
            {
                TogetherSettled.Landed => each with { Phase = TogetherPhase.Landed, Attempt = attempt, Since = at },
                TogetherSettled.Open => each with { Phase = TogetherPhase.Landing, Attempt = attempt },
                TogetherSettled.Unmade => each with { Phase = TogetherPhase.Ready, Attempt = null, Since = at },
                _ => each.Phase == TogetherPhase.Uncertain && attempt == each.Attempt ? null
                    : each with { Phase = TogetherPhase.Uncertain, Attempt = attempt, Since = at },
            };
        });

    /// <summary>Forget an operation whose sessions were deleted: nothing reads it again, and it binds nothing.</summary>
    public bool Drop(string id)
    {
        var dropped = false;
        Edit(all =>
        {
            if (!all.Any(each => each.Id == id)) return null;
            dropped = true;
            return [.. all.Where(each => each.Id != id)];
        });
        return dropped;
    }

    private static string NewId() => "tg" + Guid.NewGuid().ToString("N")[..10];

    private TogetherOperation? Move(string id, string[] from, Func<TogetherOperation, TogetherOperation?> change)
    {
        TogetherOperation? moved = null;
        Edit(all =>
        {
            var at = all.ToList().FindIndex(each => each.Id == id);
            if (at < 0 || !from.Contains(all[at].Phase) || change(all[at]) is not { } next) return null;
            moved = next;
            return [.. all.Take(at), next, .. all.Skip(at + 1)];
        });
        return moved;
    }

    /// <summary>Read, change and write whole under the gate and the record lock; a change answering null writes nothing.</summary>
    private void Edit(Func<IReadOnlyList<TogetherOperation>, IReadOnlyList<TogetherOperation>?> change)
    {
        lock (Gate)
        {
            using var held = RecordLock.Take(LockPath);
            if (change(All()) is not { } changed) return;
            var array = new JsonArray([.. changed.Select(each => JsonSerializer.SerializeToNode(each, Options))]);
            JsonFile.Write(FilePath, new JsonObject { ["operations"] = array });
        }
    }

    private IOException Unread(Exception? error) =>
        new($"the operations bringing sessions together ({FilePath}) do not read{(error is null ? "" : $": {error.Message}")}", error);
}
