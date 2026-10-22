using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One kind of Ask Daoris's proposal (MOD6): the fields only it carries, its judge — the route's own code,
/// run against the machine as it stands — and its Apply, through the door the screen's own route uses.
/// </summary>
/// <remarks>
/// <para>A kind is a class in a file of its own under <c>Help/Proposals/</c>, registered in
/// <see cref="HelpProposalKinds.All"/> by one line. Its file also adds to <see cref="HelpProposal"/>,
/// <see cref="HelpMachineFacts"/>, <see cref="HelpPlan"/> and <see cref="IHelpDoors"/> what only it needs, so a
/// new kind edits none of theirs.</para>
///
/// <para>Its twins are the service's writer of the same kind (<c>HelpProposalBox.&lt;Kind&gt;.cs</c>) and the
/// page's card; the kinds' structural test holds the three together, so a kind cannot be half-added.</para>
/// </remarks>
internal interface IHelpProposalKind
{
    /// <summary>The kind the file names, as the service's box writes it.</summary>
    string Kind { get; }

    /// <summary>The connector's tool that proposes it, which the room allows (HELP1c).</summary>
    string Tool { get; }

    /// <summary>
    /// The doors it takes, as the file's <c>door</c> spells them and its judge reads them: a door not here is refused.
    /// </summary>
    /// <remarks>
    /// HELP9 (D110): what Ask Daoris's coverage is held against — each <c>daoris driver</c> verb and each Settings
    /// control is one of these doors, a door owed, or exempt with its reason — and what the service's writer of the
    /// same kind spells, which the kinds' structural test reads.
    /// </remarks>
    IReadOnlyList<string> Doors { get; }

    /// <summary>The fields only this kind carries, read from a file; one the file lacks reads as null.</summary>
    HelpProposal Read(HelpProposal proposal, JsonElement file) => proposal;

    /// <summary>
    /// The workspaces and repositories it names (ASKSCOPE1c): what a conversation bound to one workspace may name only of its own,
    /// judged before <see cref="Plan"/> (<see cref="HelpProposalScope"/>). By default the file's <c>workspace</c> and
    /// <c>repository</c>; a kind whose target or value names one says so in its file, and one naming a quest, an ask or a session
    /// by id names that record with its workspace, as the facts its judge reads hold it (ASKSCOPE1c2).
    /// </summary>
    HelpNames Named(HelpProposal proposal, HelpMachineFacts facts) => new([proposal.Workspace], [proposal.Repository]);

    /// <summary>Judge a proposal with the route's own code, against the machine as it stands.</summary>
    HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts);

    /// <summary>
    /// Its card drawn for the person, judged as <paramref name="plan"/> (HIST1f-b): a kind whose Apply sends exactly what its
    /// card listed keeps that now, in the proposal's file, for <see cref="Read"/> to hand its next judge. Nothing for every other.
    /// </summary>
    void Drawn(string home, HelpProposal proposal, HelpPlan plan)
    {
    }

    /// <summary>The person's Apply of a plan the route takes, through the screen's own door, settled either way.</summary>
    /// <remarks>A refusal a door raises before anything happened is let through, unsettled (<see cref="HelpProposals.ApplyAsync"/>).</remarks>
    Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct);
}

/// <summary>What a kind's Apply is handed (MOD6): the proposal, its plan, the doors, and the conversation for what comes after.</summary>
internal sealed class HelpApplying(string home, HelpProposal proposal, HelpPlan plan, IHelpDoors doors, Action<string> later)
{
    /// <summary>The Daoris home the proposal's file is under, for a kind that keeps what its first press found (HELP10).</summary>
    public string Home => home;

    public HelpProposal Proposal => proposal;

    public HelpPlan Plan => plan;

    public IHelpDoors Doors => doors;

    /// <summary>The conversation, for what is said after the Apply's own answer: an agent action's end.</summary>
    public Action<string> Later => later;

    public string Id => proposal.Id;

    /// <summary>Settle the proposal — `applied`, or `refused` with the door's words as its note — and answer what the press did.</summary>
    /// <param name="published">
    /// What an applied press published (ASKQUESTS1): the ask and the quest the service's answer named, kept with the conversation
    /// (<see cref="HelpPublished"/>) before the proposal settles, so a card settled applied never stands without its entry.
    /// </param>
    public HelpApplied Settled(bool applied, string told, string? note, HelpPublishing? published = null)
    {
        if (applied && published is { Names: true }) HelpProposals.KeepPublished(home, proposal, published);
        HelpProposals.Settle(home, proposal.Id, applied ? "applied" : "refused", note);
        return new HelpApplied(applied, told);
    }
}

/// <summary>
/// The kinds Ask Daoris proposes (MOD6), one line each, in the order the room allows their tools. A new kind
/// is a file under <c>Help/Proposals/</c> and a line here.
/// </summary>
internal static class HelpProposalKinds
{
    public static readonly IReadOnlyList<IHelpProposalKind> All =
    [
        new HelpSettingProposals(),
        new HelpAskProposals(),
        new HelpAgentProposals(),
        new HelpDeleteProposals(),
        new HelpAccountProposals(),
        new HelpGoProposals(),
        new HelpPluginProposals(),
        new HelpHandProposals(),
        new HelpBrowserProposals(),
        new HelpSyncProposals(),
        new HelpAcceptProposals(),
        new HelpRepositoryProposals(),
        new HelpSessionProposals(),
        new HelpPauseProposals(),
        new HelpClearProposals(),
        new HelpWorkflowProposals(),
        new HelpSetupProposals(),
    ];

    /// <summary>The class that judges a kind, matched exactly as the file spells it; null for a kind none judges.</summary>
    public static IHelpProposalKind? Find(string kind) =>
        All.FirstOrDefault(each => string.Equals(each.Kind, kind, StringComparison.Ordinal));
}
