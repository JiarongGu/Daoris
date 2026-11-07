using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A driven line's limit move onto another account of the same agent, carrying its own conversation there: planned from the door's
/// declaration and the context the part left, its files copied before anything runs, and the resume the new part goes on with, Daoris's
/// word its prompt and no brief. A move to another agent asks no carry.
/// </summary>
internal static class LimitCarried
{
    /// <summary>Daoris's word to the carried conversation, after whatever the person said.</summary>
    public const string Words = "Your account reached its limit, so this conversation goes on on another account. Go on with the work where you stopped.";

    /// <summary>How the move takes the conversation, its copy made where it carries; null where the move is no limit's within one agent.</summary>
    public static CarryPlan? Plan(LoopContext context, AgentDoor door, PriorSession? prior, string? why, HarnessSelection selection, string tree)
    {
        if (prior is null || why != HandOverWhy.Limit) return null;
        var registry = context.Adapters;
        string owner;
        try
        {
            owner = registry.OwnerOf(door.Id).Value;
            if (!Ids.Same(registry.OwnerOfAgent(prior.Adapter ?? door.Id.Value).Value, owner)) return null;
        }
        catch (DriverException)
        {
            return null;
        }

        var carry = LimitCarry.Declared(registry, door.Id);
        string? from;
        try
        {
            from = prior.Profile is { Length: > 0 } left ? HarnessSettings.ProfileHome(context.Home, owner, left) : null;
        }
        catch (DriverException)
        {
            from = null;
        }

        var to = selection.Profile is { Length: > 0 } ? selection.ProfileHome : null;
        var used = RotatedOpening.ContextOf(context.Usage, context.Events, prior.Session);
        var size = context.Usage?.Of(prior.Session)?.Size;
        var plan = LimitCarry.Plan(carry, context.Conversations.Read(prior.Session)?.Conversation, from, to, used, size);
        return from is null || to is null ? plan : LimitCarry.Copied(plan, carry, from, to, tree);
    }

    /// <summary>The resume the new part goes on with: the carried conversation, the person's words moved to it, then Daoris's word.</summary>
    public static ResumeAsk Ask(CarryPlan.Carried carried, Noted first, IReadOnlyList<SaidWordView>? moved)
    {
        var persons = moved is null ? "" : SaidWordView.PersonsJoined(moved);
        return new ResumeAsk(carried.Conversation, [], first, persons is { Length: > 0 } ? $"{persons}\n\n{Words}" : Words);
    }

    /// <summary>
    /// The spawn of the carried conversation: on a door that resumes by a restart, its resume request; the compaction's variables on
    /// either door where the context is past the bound.
    /// </summary>
    public static ProcessStartInfo? Prepared(LoopContext context, AgentDoor door, SessionTarget target, CarryPlan.Carried carried) =>
        door.Can.ResumesByRestart
            ? context.Adapters.Resume(door, SpawnRequests.Of(target, context.Config.Commands.GetValueOrDefault(door.Id.Value)) with { Resume = carried.Conversation })
            : null;

    /// <summary>The compaction's variables set on the process, where it is compacted first.</summary>
    public static void Compact(ProcessStartInfo info, CarryPlan.Carried carried)
    {
        if (carried is not CarryPlan.Compacted compacted) return;
        foreach (var (name, value) in compacted.Variables) info.Environment[name] = value;
    }
}
