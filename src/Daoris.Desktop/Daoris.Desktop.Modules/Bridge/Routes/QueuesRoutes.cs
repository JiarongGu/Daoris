using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The machine's queues read (<c>QUEUES</c>): <c>Said</c> asks for <c>daoris queue</c>'s lines beside the rows, <c>Json</c> for its <c>--json</c> form.</summary>
public sealed record QueuesRead(bool Said = false, bool Json = false);

/// <summary>A class set by the person's press (<c>PRIORITY_SET</c>) on an ask or a quest, with why, and an urgent one's end where they name one.</summary>
public sealed record PrioritySetPress(string? Ask, string? Quest, string? Class, string? Why, string? Until = null);

/// <summary>
/// The machine's queues and the class that orders them: the window's queues card reads the one reader of the waiting items that
/// <c>daoris queue</c> prints (<see cref="QueueRows"/>) and composes each place with its twin of the comparator, and one press sets a class
/// through the operation <c>daoris priority</c> calls (<see cref="PriorityCommand.SetAsync"/>).
/// </summary>
public sealed class QueuesRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// Every waiting item, <c>{ rows: [{ queue, item, class, stage, arrived, order, source, by?, why?, ask?, agedFrom?, repository, code, verdict?,
    /// values, reason }] }</c>: the last look's waiting starts (a fresh plan before the first look) and the checks waiting for a slot.
    /// </summary>
    [DriverRoute("QUEUES", PipeClass.Open)]
    [ByEvent("QUEUES", Untold = "the check slots' waiter files, which the heavy executor writes between looks")]
    private async Task<object?> QueuesAsync(QueuesRead read, CancellationToken cancellationToken)
    {
        var plan = _loop.Look.Latest;
        if (plan is null)
        {
            var service = _loop.Service ?? throw DriverArguments.NotReady();
            var config = DriverConfig.Load(_loop.ConfigPath);
            plan = await SessionGroups.VerdictsAsync(service, config, context.Unadopted(config), lastLook: null, cancellationToken, _loop.Home).ConfigureAwait(false);
        }

        // The waiters are files beside the slots, read off the window's thread.
        var rows = await Task.Run(() => QueueRows.Read(plan, VerifySlot.Look(_loop.Home)), cancellationToken).ConfigureAwait(false);
        return new
        {
            Rows = rows.Select(QueueRows.Wire).ToArray(),
            Said = read.Said ? new TreesSaid(QueueCommand.Lines(rows, read.Json), [], 0).ToWire() : null,
        };
    }

    /// <summary>One class set on an ask or a quest, answered as the class standing; a refusal is the command's words.</summary>
    [DriverRoute("PRIORITY_SET", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("PRIORITY_SET", Ask = "ask", Quest = "quest")]
    [GroundAsk("PRIORITY_SET", "ask")]
    [GroundQuest("PRIORITY_SET", "quest")]
    [Says("PRIORITY_SET", "quest:quest", "ask:ask")]
    [Publishes("PRIORITY_SET", StateScopes.Driver)]
    private async Task<object?> PrioritySetAsync(PrioritySetPress press, CancellationToken cancellationToken)
    {
        var named = new[] { press.Ask, press.Quest }.Where(each => !string.IsNullOrWhiteSpace(each)).ToList();
        if (named.Count != 1) throw new DriverException("a class is set on one ask or one quest: name one.");
        if (PriorityClasses.Parse(press.Class) is not { } @class) throw new DriverException("a class is urgent, high, normal or low.");
        if (string.IsNullOrWhiteSpace(press.Why)) throw new DriverException("a class is set with why: say it.");
        var service = _loop.Service ?? throw DriverArguments.NotReady();
        var clock = TimeProvider.System;
        DateTimeOffset? until = null;
        if (press.Until is { Length: > 0 } end && (until = PriorityCommand.Time(end, clock.GetUtcNow())) is null)
        {
            throw new DriverException($"an end is a moment or minutes, hours or days from now (`2h`) — `{end}` is neither.");
        }

        var world = new PriorityWorld(_loop.Home, PriorityWorld.FindIn(service), clock.GetUtcNow, "the person");
        var said = await PriorityCommand.SetAsync(world, named[0]!, @class, press.Why.Trim(), until, cancellationToken).ConfigureAwait(false);
        // The next look orders its starts by the class; the check queue reads it at its executor's next pass.
        _loop.Nudge();
        return new { Said = said };
    }
}
