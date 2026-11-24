using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The owner's change of the risk policy through Daoris's assistant (<see cref="PolicyChanges"/>): the assistant proposes edits of the policy's rows
/// and the proposal waits (<c>POLICY_PROPOSE</c>, <c>POLICY_PROPOSALS</c> reading them), and a change is applied (<c>POLICY_APPLY</c>) only by the
/// owner's own turn in the proposing conversation naming its id and digest, read from that conversation's kept events, or by the owner's own
/// press in the window. The channel is read from the pipe's proof (<see cref="PipeDoor.By"/>), never from the payload: a working session's press
/// is refused, and a press that proves no session applies nothing.
/// </summary>
public sealed class PolicyRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>The home's proposals and the policy's revision, as the page and the assistant read them. Read-only.</summary>
    [DriverRoute("POLICY_PROPOSALS", PipeClass.Open)]
    [ByEvent("POLICY_PROPOSALS", Untold = "driver.json's risk rows and the proposals the home keeps, which no scope or slice tells")]
    private object? Proposals(NoPayload _) => View();

    /// <summary>The assistant's proposal of row edits: kept pending with its id, digest and the waits it opens or releases; nothing changes.</summary>
    [DriverRoute("POLICY_PROPOSE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("POLICY_PROPOSE", None = OwnerNone.MachineSetting)]
    [GroundRead("POLICY_PROPOSE", nameof(PipeReads.PolicyRevision))]
    [NotAlready("POLICY_PROPOSE", "each proposal is a new id, so the same edits proposed again are a second proposal")]
    [Publishes("POLICY_PROPOSE", StateScopes.Driver)]
    private async Task<object?> ProposeAsync(ProposePolicy press, CancellationToken cancellationToken)
    {
        var workspaceOf = await WorkspaceOfAsync(cancellationToken).ConfigureAwait(false);
        return Said(Changes().Propose(ChannelOf(press.Conversation), EditsOf(press.Edits), workspaceOf));
    }

    /// <summary>The owner's own set, written at once at the revision the press read: the window's act table or their terminal; an agent's is a proposal instead.</summary>
    [DriverRoute("POLICY_SET", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("POLICY_SET", None = OwnerNone.MachineSetting)]
    [GroundRead("POLICY_SET", nameof(PipeReads.PolicySet))]
    [NotAlready("POLICY_SET", "a row set again at a revision that has moved is refused as stale by the door itself")]
    [Publishes("POLICY_SET", StateScopes.Driver)]
    private async Task<object?> SetAsync(SetPolicy press, CancellationToken cancellationToken)
    {
        var (by, session) = PipeDoor.By;
        var door = by == TerminalTierBy.Window ? PolicyChangeLine.Window : by == TerminalTierBy.Card ? PolicyChangeLine.Pressed : PolicyChangeLine.Cli;
        var workspaceOf = await WorkspaceOfAsync(cancellationToken).ConfigureAwait(false);
        var outcome = Changes().Set(SetChannelOf(by, session), door, EditsOf(press.Edits), press.Revision, workspaceOf);
        if (outcome.Done) _loop.Nudge();
        return Said(outcome);
    }

    /// <summary>A place's act table (its classes, sources, answerers and seven days of passes and gates) and, for a pending edit, the waits it would open or release.</summary>
    [DriverRoute("RISK_TABLE", PipeClass.Open)]
    [ByEvent("RISK_TABLE", Untold = "driver.json's risk rows, the cast, and the judge's journal, which no scope or slice tells")]
    private object? Table(RiskTableRead read)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        var table = RiskActTable.Of(config, _loop.Home, DateTimeOffset.UtcNow, read.Workspace, read.Repository);
        var edits = EditsOf(read.Edits);
        var shifts = RiskActTable.Preview(config, table.Place, read.Workspace, edits);
        return new { table.Place, table.Mode, table.Revision, table.Fault, table.Unknown, table.Rows, shifts, seeded = RiskActs.All };
    }

    private async Task<Func<string, string?>> WorkspaceOfAsync(CancellationToken cancellationToken)
    {
        // A repository's workspace is the registry's, so a proposal is previewed at the place the runtime judges it at.
        var snapshot = _loop.Service is { } service ? await service.SnapshotAsync(cancellationToken).ConfigureAwait(false) : null;
        return repository => snapshot?.Repositories
            .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
    }

    private static List<PolicyEdit> EditsOf(IReadOnlyList<PolicyEditPress>? edits) =>
        [.. (edits ?? []).Select(edit => new PolicyEdit(edit.Place ?? "", edit.Act ?? "", edit.Class, edit.By, edit.Mode) { Subject = edit.Subject ?? PolicyEdit.RowSubject })];

    /// <summary>The proposal applied where the owner's own turn or press confirms it; a refusal is an answer with its code, and nothing is written.</summary>
    [DriverRoute("POLICY_APPLY", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("POLICY_APPLY", None = OwnerNone.MachineSetting)]
    [GroundRead("POLICY_APPLY", nameof(PipeReads.PolicyProposal))]
    [NotAlready("POLICY_APPLY", "an applied proposal is refused as taken by the door itself")]
    [Publishes("POLICY_APPLY", StateScopes.Driver)]
    private object? Apply(ApplyPolicy press)
    {
        var outcome = Changes().Apply(ChannelOf(null), press.Proposal ?? "", press.Digest ?? "");
        if (outcome.Done) _loop.Nudge();
        return Said(outcome);
    }

    private PolicyChanges Changes() =>
        new(_loop.Home, () => DateTimeOffset.UtcNow, new OwnerTurns(session => _loop.Events.After(session, 0).Events));

    /// <summary>
    /// Who pressed, by the channel it came by: Ask Daoris's conversation by its proved session, a working session's own press as an agent's, the
    /// owner's own press in the window or on their card as the owner (naming, for a proposal, the conversation it concerns), anything else as none.
    /// </summary>
    private static PolicyChannel ChannelOf(string? named)
    {
        var (by, session) = PipeDoor.By;
        return ChannelOf(by, session, named);
    }

    /// <summary>The channel of a set: the owner's window, card or terminal (a terminal in an agents' job is refused before the route), a working session as an agent, any other as none.</summary>
    public static PolicyChannel SetChannelOf(string by, string? session) =>
        by switch
        {
            TerminalTierBy.Window or TerminalTierBy.Card or TerminalTierBy.Terminal => PolicyChannel.OwnerPress(null),
            TerminalTierBy.Session when session is not null => PolicyChannel.Agent(session),
            _ => PolicyChannel.Keyless,
        };

    public static PolicyChannel ChannelOf(string by, string? session, string? named) =>
        by switch
        {
            TerminalTierBy.Session when session is not null => PolicyChannel.Agent(session),
            TerminalTierBy.AskDaoris => PolicyChannel.Of(session),
            TerminalTierBy.Window or TerminalTierBy.Card => PolicyChannel.OwnerPress(named),
            _ => PolicyChannel.Keyless,
        };

    private object View() => new
    {
        revision = RiskPolicy.Of(DriverConfig.Load(_loop.ConfigPath)).Revision,
        proposals = Changes().Proposals().Select(proposal => new
        {
            proposal.Id, proposal.Digest, proposal.Session, proposal.State, proposal.BaseRevision, proposal.Edits, proposal.Shifts, said = proposal.Said(),
        }).ToList(),
    };

    private static object Said(PolicyOutcome outcome) => new
    {
        outcome.Done, outcome.Code, Message = outcome.Said, proposal = outcome.Proposal?.Id, digest = outcome.Proposal?.Digest,
    };
}

/// <summary>One edit of a place: a row (<c>class</c> null takes it away), or by <c>subject</c> a role's cast (<c>act</c> the role, <c>by</c> the holder) or its access mode (<c>class</c>).</summary>
public sealed record PolicyEditPress(string? Place, string? Act, string? Class, string? By, string? Mode, string? Subject = null);

/// <summary>The rows a proposal edits; <c>conversation</c> names the conversation a window or card press concerns, and is read from the proof otherwise.</summary>
public sealed record ProposePolicy(IReadOnlyList<PolicyEditPress>? Edits, string? Conversation);

/// <summary>The edits the owner's own press sets, and the revision of the policy it read.</summary>
public sealed record SetPolicy(IReadOnlyList<PolicyEditPress>? Edits, string? Revision);

/// <summary>The place an act table is read at (neither named: the machine) and an edit whose waits are previewed beside it.</summary>
public sealed record RiskTableRead(string? Workspace, string? Repository, IReadOnlyList<PolicyEditPress>? Edits);

/// <summary>The proposal to apply and the digest the owner's turn named.</summary>
public sealed record ApplyPolicy(string? Proposal, string? Digest);
