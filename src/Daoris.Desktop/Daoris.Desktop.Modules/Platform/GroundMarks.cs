using System.Reflection;

namespace Daoris.Desktop;

/// <summary>
/// One ground a person's press rests on, beside its route's mark: read at the press's ask and again at its Confirm, so a press is
/// refused where what it acts on moved. A route's grounds are read in the order its marks say them, and the first that moved is the
/// reason the person is told.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
internal abstract class RouteGroundAttribute(string route) : Attribute
{
    public string Route { get; } = route;

    public abstract IEnumerable<PipeGround> Grounds();
}

/// <summary>The session, or with <see cref="Many"/> the sessions, a payload's field names.</summary>
internal sealed class GroundSessionAttribute(string route, string field) : RouteGroundAttribute(route)
{
    public bool Many { get; set; }

    public override IEnumerable<PipeGround> Grounds() => [new SessionGround(field, Many)];
}

/// <summary>The repository or workspace the first of the payload's fields that holds one names.</summary>
internal sealed class GroundRepositoryAttribute(string route, params string[] fields) : RouteGroundAttribute(route)
{
    public override IEnumerable<PipeGround> Grounds() => [new RepositoryGround(fields)];
}

internal sealed class GroundQuestAttribute(string route, string field) : RouteGroundAttribute(route)
{
    public override IEnumerable<PipeGround> Grounds() => [new QuestGround(field)];
}

internal sealed class GroundAskAttribute(string route, string field) : RouteGroundAttribute(route)
{
    public override IEnumerable<PipeGround> Grounds() => [new AskGround(field)];
}

/// <summary>The head of the tree of the session the field names.</summary>
internal sealed class GroundHeadAttribute(string route, string field) : RouteGroundAttribute(route)
{
    public override IEnumerable<PipeGround> Grounds() => [BranchGround.HeadOf(field)];
}

/// <summary>A branch's tip: the first of <paramref name="branch"/>'s fields that holds one, in the repository the other names.</summary>
internal sealed class GroundBranchAttribute(string route, string repository, params string[] branch) : RouteGroundAttribute(route)
{
    public override IEnumerable<PipeGround> Grounds() => [BranchGround.Named(repository, branch)];
}

/// <summary>A wait the loop holds (<see cref="LoopGround.Waits"/>), by the payload's fields.</summary>
internal sealed class GroundWaitAttribute(string route, string wait, params string[] fields) : RouteGroundAttribute(route)
{
    public override IEnumerable<PipeGround> Grounds() => [LoopGround.Wait(wait, fields)];
}

/// <summary>
/// What the press's files are (<see cref="LoopGround.Files"/>), by the payload's fields: within the page's bound, or with
/// <see cref="Quick"/> a state's, for one file.
/// </summary>
internal sealed class GroundFilesAttribute(string route, string files, params string[] fields) : RouteGroundAttribute(route)
{
    public bool Quick { get; set; }

    public override IEnumerable<PipeGround> Grounds() => [LoopGround.Plan(files, Quick ? PipeGround.StateBound : PipeGround.PageBound, fields)];
}

/// <summary>A repository named in one of <c>STATE</c>'s lists, set by the payload's boolean <paramref name="flag"/>.</summary>
internal sealed class GroundListedAttribute(string route, string list, string flag) : RouteGroundAttribute(route)
{
    public override IEnumerable<PipeGround> Grounds() => [PipeReads.Listed(list, flag)];
}

/// <summary>
/// A standing choice at a repository's or a workspace's scope: the scope named, and its row in <c>STATE</c>'s
/// <paramref name="list"/> or its workspace twin. <see cref="Sets"/> names the field of a single value it sets; a rule merged
/// into names none, so it is never <c>already</c>.
/// </summary>
internal sealed class GroundScopedAttribute(string route, string list, string workspaces) : RouteGroundAttribute(route)
{
    public string? Sets { get; set; }

    public override IEnumerable<PipeGround> Grounds() => [new RepositoryGround("repository", "workspace"), PipeReads.Scoped(list, workspaces, Sets)];
}

/// <summary>A read of the bridge or the loop projected to what the press acts on, by its name in <see cref="PipeReads"/>.</summary>
internal sealed class GroundReadAttribute(string route, string read) : RouteGroundAttribute(route)
{
    public override IEnumerable<PipeGround> Grounds() =>
    [
        typeof(PipeReads).GetMethod(read, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)?.Invoke(null, null) as PipeGround
            ?? throw new InvalidOperationException($"`{Route}` rests on `{read}`, and PipeReads has no such read."),
    ];
}

/// <summary>A person's press that rests on nothing a wait moves: it deletes nothing, moves no line or branch and starts no work.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
internal sealed class TimelessAttribute(string route, string why) : Attribute
{
    public string Route { get; } = route;

    public string Why { get; } = why;
}

/// <summary>
/// The payload's values a person's press's sentence names on its card, each <c>field:role</c> (<see cref="PipeRole"/>), in the
/// order the sentence names them. A press that says none says its act alone, every value listed under it.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
internal sealed class SaysAttribute(string route, params string[] words) : Attribute
{
    public string Route { get; } = route;

    public IEnumerable<PipeWord> Words() => words.Select(word => word.Split(':') is [var field, var role] && Enum.TryParse<PipeRole>(role, true, out var named)
        ? new PipeWord(field, named)
        : throw new InvalidOperationException($"`{Route}` says `{word}`, which is no field:role."));
}
