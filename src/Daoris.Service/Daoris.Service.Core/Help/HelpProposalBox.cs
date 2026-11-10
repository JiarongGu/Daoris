namespace Daoris.Knowledge;

/// <summary>
/// Where Ask Daoris's proposals are written: one file each, under the driver's home, for
/// the person to apply or not.
/// </summary>
/// <remarks>
/// <para><b>One writer per kind</b>, each a class, <c>Help&lt;Kind&gt;ProposalWriter</c>, in a file of its own and listed in
/// <see cref="Kinds"/>. The box's <c>Propose…</c> method, which the connector's tool calls, is one call to the kind's writer, which
/// checks the kind's shape and hands its fields to <see cref="HelpProposalFile.Write"/>. Every file carries
/// <c>target</c>, <c>workspace</c>, <c>value</c> and <c>sentence</c>, null where the kind has none.</para>
///
/// <para><b>This checks the shape and nothing more.</b> Whether the route would take it — a line git
/// accepts, a pattern that names one branch per session, a repository registered here, an agent this
/// machine has — is the driver's, which judges each with the route's own code before the person sees
/// it and hands a refusal back to the conversation in the route's words.</para>
///
/// <para><b>THE FILE is the contract</b>: the driver's <c>HelpProposals</c> reads and settles it, shares no
/// code with this, and each side's tests hold the same shape — and the same table of kinds.</para>
/// </remarks>
public sealed class HelpProposalBox(string? home)
{
    /// <summary>
    /// The kinds this box writes, each with the connector's tool that proposes it, in the order the room allows
    /// them; the driver's <c>HelpProposalKinds</c> lists the same.
    /// </summary>
    public static readonly IReadOnlyList<(string Kind, string Tool)> Kinds =
    [
        ("ask", "ask_propose"),
        ("go", "go_propose"),
        ("plugin", "plugin_propose"),
        ("browser", "browser_propose"),
        ("sync", "sync_propose"),
        ("clear", "clear_propose"),
        ("workflow", "workflow_propose"),
        ("setup", "setup_propose"),
        ("role", "role_propose"),
    ];

    private readonly HelpProposalFile file = new(home);

    public string? Home => file.Home;

    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.HelpProposals);

    public static HelpProposalBox FromEnvironment() =>
        new(RuleProposalBox.FromEnvironment().Home);

    public (string? Id, string Message) ProposeAsk(
        string sentence, string workspace, string why, string? session, DateTimeOffset at,
        string? review = null, string? reviewWords = null, string? shortTitle = null) =>
        new HelpAskProposalWriter(file).Propose(sentence, workspace, why, session, at, review, reviewWords, shortTitle);

    /// <inheritdoc cref="HelpGoProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeGo(
        string view, string? domain, string? part, string? item, string why, string? session, DateTimeOffset at, string? workspace = null,
        string? repository = null, string? sentence = null) =>
        new HelpGoProposalWriter(file).Propose(view, domain, part, item, why, session, at, workspace, repository, sentence);

    /// <inheritdoc cref="HelpPluginProposalWriter.Propose"/>
    public (string? Id, string Message) ProposePlugin(
        string action, string? id, string? folder, string? repository, string why, string? session, DateTimeOffset at,
        string? offer = null, string? server = null, IReadOnlyList<string>? writes = null) =>
        new HelpPluginProposalWriter(file).Propose(action, id, folder, repository, why, session, at, offer, server, writes);

    /// <inheritdoc cref="HelpBrowserProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeBrowser(
        string door, string? value, string? address, string? title, string why, string? session, DateTimeOffset at) =>
        new HelpBrowserProposalWriter(file).Propose(door, value, address, title, why, session, at);

    /// <inheritdoc cref="HelpSyncProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeSync(
        string? repository, string why, string? session, DateTimeOffset at, string? workspace = null) =>
        new HelpSyncProposalWriter(file).Propose(repository, why, session, at, workspace);

    /// <inheritdoc cref="HelpRoleProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeRole(
        string change, string id, string? name, IReadOnlyDictionary<string, string>? nameIn, IReadOnlyList<string>? takes, string? scope, IReadOnlyList<string>? choices,
        long? revision, string why, string? session, DateTimeOffset at, string? policy = null, long? policyRevision = null,
        string? place = null, string? rules = null, long? rulesRevision = null, HelpSupervisorFields? supervisor = null) =>
        new HelpRoleProposalWriter(file).Propose(change, id, name, nameIn, takes, scope, choices, revision, why, session, at, policy, policyRevision, place, rules, rulesRevision, supervisor);

    /// <inheritdoc cref="HelpClearProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeClear(
        string door, string? quest, string? ask, string? workspace, string why, string? session, DateTimeOffset at) =>
        new HelpClearProposalWriter(file).Propose(door, quest, ask, workspace, why, session, at);

    /// <inheritdoc cref="HelpWorkflowProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeWorkflow(
        string? scope, string? workflow, int? baseVersion, string? steps, bool choose,
        string? changes, string? kind, string? import, string why, string? session, DateTimeOffset at, string? name = null,
        string? graph = null, string? route = null, string? draft = null) =>
        new HelpWorkflowProposalWriter(file).Propose(scope, workflow, baseVersion, steps, choose, changes, kind, import, why, session, at, name, graph, route, draft);

    /// <inheritdoc cref="HelpSetupProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeSetup(string repository, string why, string? session, DateTimeOffset at, HelpSetupPlace? place = null) =>
        new HelpSetupProposalWriter(file).Propose(repository, why, session, at, place);
}
