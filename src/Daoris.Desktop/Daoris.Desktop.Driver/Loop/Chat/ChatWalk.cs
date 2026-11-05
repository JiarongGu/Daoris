namespace Daoris.Driver;

/// <summary>
/// A new conversation's walk over its actor's effective order: choice by choice, each choice's accounts by the existing walk before the next,
/// every account judged by its offer reading for the choice's model. What the walk passes is remembered here, so a pass is said when it is new.
/// </summary>
internal sealed class ChatWalk(ChatContext context)
{
    // What each conversation order's walk last passed, so `actor.passed` is written when a pass is new.
    private readonly PassMemory _passed = new();

    /// <summary>
    /// An entry that cannot hold a conversation here is passed <c>unavailable</c> before any account is asked, and, for Ask Daoris, one its
    /// access refuses under the mode standing now <c>no-posture</c> (<see cref="HelpAccess.For"/>), so a full-access mode never reaches an
    /// agent with no mode that asks nothing. With none that can run, the press is refused in the wait's sentence, each choice named.
    /// </summary>
    /// <param name="refused">The entry and account an open refused, passed as if known before the spawn, beside the reading kept.</param>
    /// <param name="counts">False for an open's re-ask, which takes no count of its own, <paramref name="except"/> its start's count left out.</param>
    public async Task<ConversationWalk> AskAsync(
        string actor, IReadOnlyList<ActorEffective> order, DriverConfig config, string? workspace, string? profile, CancellationToken ct,
        (ActorEffective Entry, string? Account)? refused = null, bool counts = true, long except = 0)
    {
        using var log = new MachineLog(context.Home, "driver");
        // A conversation has no record yet while it is walked, and what its walk passes is fixed by the actor's order and the place it
        // is asked in, so those name the subject whose passes are not said again while they stand.
        StartPlan plan;
        try
        {
            // Through the plan with the catalogue and the place, so the walk's role is the one that takes the work there.
            plan = new StartPlan(
                actor, config, context.Harnesses, context.Harnesses.Adapters, _passed, order: order, catalogue: RoleCatalog.ReadHome(context.Home),
                place: new RolePlace(workspace))
            {
                Workspace = workspace, Kind = StartKind.Conversation, Subject = $"conversation:{actor}:{workspace}:{profile}", Log = log,
                Pinned = profile, WalksOne = true, Staff = context.Harnesses.StaffFor(context.Home), OwnOrder = true,
                Gate = (_, adapter) => !adapter.Interactive ? (StartPasses.Unavailable, $"`{adapter.Name}` does not hold conversations")
                    : actor == "ask-daoris" && HelpAccess.For(config.HelpMode, new ChatAgent(adapter)).Refusal is { } unposed ? (StartPasses.NoPosture, unposed)
                    : null,
            };
        }
        catch (DriverException error)
        {
            // A catalogue that will not read, or two roles taking the work in one scope: the start holds, saying so.
            return new(null, null, null, [], error.Message);
        }

        var settled = await plan.ResolveAsync(ct, counts, refused, except).ConfigureAwait(false);
        return settled.Chosen is null
            ? new(null, null, null, settled.Passes, settled.Selection.Refusal)
            : new(settled.Adapter, settled.Selection, settled.Chosen, settled.Passes, null);
    }
}
