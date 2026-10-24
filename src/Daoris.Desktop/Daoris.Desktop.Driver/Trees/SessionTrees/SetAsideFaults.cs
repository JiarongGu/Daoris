namespace Daoris.Driver;

/// <summary>
/// The moments a test's flow interleaves with the set-aside's destructive steps (KEEPSIZE1b, Codex's reads of the design and the branch):
/// a holder moved between the tip's ref and the branch's delete, a tracked file deleted between the judgement and the move, git refusing
/// to let go of a tree and the folder's put-back refused, and a stop right after the move, right after a removal's rename, or inside its
/// delete. Null in
/// production, as <see cref="ChildProcess"/>'s seams are; set for one flow alone (<see cref="Within"/>).
/// </summary>
internal static class SetAsideFaults
{
    private static readonly AsyncLocal<SetAsideFault?> s_within = new();

    /// <summary>The fault this flow carries; null in production.</summary>
    internal static SetAsideFault? Current => s_within.Value;

    /// <summary>Carry <paramref name="fault"/> in this flow until the result is disposed.</summary>
    internal static IDisposable Within(SetAsideFault fault)
    {
        var before = s_within.Value;
        s_within.Value = fault;
        return new Undo(() => s_within.Value = before);
    }

    private sealed class Undo(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}

/// <summary>What a test's flow makes happen at the set-aside's steps (<see cref="SetAsideFaults"/>).</summary>
internal sealed class SetAsideFault
{
    /// <summary>Run right after the tip is held (its ref written, or the line read holding it) and before the branch's delete.</summary>
    public Func<string, Task>? AfterTipHeld { get; init; }

    /// <summary>Run right before the tree's folder is renamed aside, with the tree's path.</summary>
    public Action<string>? BeforeMove { get; init; }

    /// <summary>git's <c>worktree remove</c> of the moved tree is answered as refused.</summary>
    public bool RefuseLetGo { get; init; }

    /// <summary>Putting the folder back after a refused let-go fails.</summary>
    public bool RefusePutBack { get; init; }

    /// <summary>The process stops right after the move is marked <c>moved</c>, before the branch's delete.</summary>
    public bool StopAfterMove { get; init; }

    /// <summary>The process stops right after a removal's rename into <c>.going</c>, before its delete.</summary>
    public bool StopAfterRename { get; init; }

    /// <summary>
    /// The process stops inside a removal's delete, right after its first file is deleted: its record in <c>.going</c> says the delete began,
    /// and what remains lies there for the resume.
    /// </summary>
    public bool StopInDelete { get; init; }
}

/// <summary>A stop a test's flow makes (<see cref="SetAsideFault"/>): the process ends here, as a crash would.</summary>
internal sealed class SetAsideStop() : Exception("stopped here by the test's fault");
