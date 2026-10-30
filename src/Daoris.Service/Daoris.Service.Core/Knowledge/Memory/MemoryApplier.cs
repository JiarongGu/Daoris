using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>How an <c>applying</c> row settles: its state, and the entry and revision the engine answered.</summary>
internal sealed record MemoryOutcome(string State, string? Entry = null, long? Revision = null)
{
    public static readonly MemoryOutcome Stale = new(MemoryOpState.Stale);

    public static readonly MemoryOutcome Held = new(MemoryOpState.Held);
}

/// <summary>
/// The row id an end carries as its reason's trailing token (the memory coordinator design §3), so recovery tells the end this row made
/// from another's; the page strips it.
/// </summary>
public static partial class MemoryEndToken
{
    /// <summary><paramref name="reason"/> carrying <paramref name="op"/>'s token.</summary>
    public static string Append(string? reason, string op) => $"{reason?.TrimEnd()} [memory-op {op}]".TrimStart();

    /// <summary>The row id an ended entry's reason carries; null where it carries none.</summary>
    public static string? Of(string? ended) => ended is not null && Token().Match(ended) is { Success: true } found ? found.Groups[1].Value : null;

    /// <summary>The reason as its reader sees it, without the token.</summary>
    public static string Strip(string ended) => Token().Replace(ended, "").TrimEnd();

    [GeneratedRegex(@"\s*\[memory-op ([0-9a-z]+)\]\s*$")]
    private static partial Regex Token();
}

/// <summary>
/// The service's half of the log (KNOWCENTRAL2c; the design §3): it alone marks <c>applying</c>, calls the engine and settles. Each row is
/// marked in its own transaction before the call and settled in another after it, so a kill between leaves it <c>applying</c>, and the next
/// start reads it observationally: an end by its token and the revision it read, a confirmation by its guard, an addition or a relation
/// <c>held</c> for the person, since nothing says whether it ran.
/// </summary>
public sealed class MemoryApplier(MemoryOpStore ops, IMemoryEngines engines, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>At the service's start: settle what a kill left <c>applying</c>, then apply what is <c>ready</c>.</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        await RecoverAsync(ct).ConfigureAwait(false);
        await ApplyReadyAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Apply every <c>ready</c> row whose repository has an engine; a proposal waits for its quest (2e).</summary>
    /// <returns>How many rows settled.</returns>
    public async Task<int> ApplyReadyAsync(CancellationToken ct = default)
    {
        var settled = 0;
        foreach (var op in await ops.ListAsync(MemoryOpState.Ready, ct).ConfigureAwait(false))
        {
            if (op.Change.Kind == MemoryOpKind.Propose || Engine(op) is not { } engine) continue;
            if (!await ops.BeginAsync(op.Id, ct).ConfigureAwait(false)) continue;

            MemoryOutcome outcome;
            try
            {
                outcome = await ApplyAsync(engine, op, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // The call's effect is unknown, as after a kill.
                outcome = await RecoverAsync(engine, op, ct).ConfigureAwait(false);
            }

            await ops.SettleAsync(op.Id, outcome, _time.GetUtcNow(), ct).ConfigureAwait(false);
            settled++;
        }

        return settled;
    }

    /// <summary>Settle each <c>applying</c> row by reading its target observationally.</summary>
    public async Task RecoverAsync(CancellationToken ct = default)
    {
        foreach (var op in await ops.ListAsync(MemoryOpState.Applying, ct).ConfigureAwait(false))
        {
            if (Engine(op) is not { } engine) continue;
            await ops.SettleAsync(op.Id, await RecoverAsync(engine, op, ct).ConfigureAwait(false), _time.GetUtcNow(), ct).ConfigureAwait(false);
        }
    }

    private IMemoryEngine? Engine(MemoryOp op) => engines.Open(op.Repository, MemoryReinforcement.None);

    private static async Task<MemoryOutcome> ApplyAsync(IMemoryEngine engine, MemoryOp op, CancellationToken ct)
    {
        var change = op.Change;
        switch (change.Kind)
        {
            case MemoryOpKind.Write:
                var written = await engine.RememberAsync(change.Text ?? "", ct).ConfigureAwait(false);
                if (change is { Relation: { } kind, Target: { } target }) await engine.RelateAsync(written.Id, target, kind, ct).ConfigureAwait(false);
                return new(MemoryOpState.Applied, written.Id, written.Revision);
            case MemoryOpKind.Relate:
                await engine.RelateAsync(change.Source ?? "", change.Target ?? "", change.Relation ?? "", ct).ConfigureAwait(false);
                return new(MemoryOpState.Applied, change.Target);
            case MemoryOpKind.End:
                return Settled(await EndAsync(engine, op, ct).ConfigureAwait(false));
            case MemoryOpKind.Confirm:
                return Settled(await engine.ConfirmAsync(change.Target ?? "", change.Revision ?? -1, ct).ConfigureAwait(false));
            default:
                return MemoryOutcome.Held;
        }
    }

    private static async Task<MemoryOutcome> RecoverAsync(IMemoryEngine engine, MemoryOp op, CancellationToken ct)
    {
        var change = op.Change;
        if (change.Kind is not (MemoryOpKind.End or MemoryOpKind.Confirm)) return MemoryOutcome.Held;

        var entry = await engine.GetAsync(change.Target ?? "", ct).ConfigureAwait(false);
        if (entry is null) return MemoryOutcome.Stale;
        if (entry.Ended is { } ended)
        {
            return change.Kind == MemoryOpKind.End && MemoryEndToken.Of(ended) == op.Id
                ? new(MemoryOpState.Applied, entry.Id, entry.Revision)
                : MemoryOutcome.Stale;
        }

        if (entry.Revision != change.Revision) return MemoryOutcome.Stale;
        return change.Kind == MemoryOpKind.End
            ? Settled(await EndAsync(engine, op, ct).ConfigureAwait(false))
            : Settled(await engine.ConfirmAsync(entry.Id, entry.Revision, ct).ConfigureAwait(false));
    }

    private static Task<MemoryEntry?> EndAsync(IMemoryEngine engine, MemoryOp op, CancellationToken ct) =>
        engine.EndAsync(op.Change.Target ?? "", op.Change.Revision ?? -1, MemoryEndToken.Append(op.Change.Text, op.Id), ct);

    private static MemoryOutcome Settled(MemoryEntry? entry) =>
        entry is null ? MemoryOutcome.Stale : new(MemoryOpState.Applied, entry.Id, entry.Revision);
}
