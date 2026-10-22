namespace Daoris.Knowledge;

/// <summary>
/// Where Ask Daoris's proposals are written (HELP1c, D89): one file each, under the driver's home, for
/// the person to apply or not.
/// </summary>
/// <remarks>
/// <para><b>One writer per kind (MOD6)</b>, each a door a screen or a terminal already has, each a class,
/// <c>Help&lt;Kind&gt;ProposalWriter</c>, in a file of its own (<c>HelpProposalBox.&lt;Kind&gt;.cs</c>, the name the
/// driver's twin tests read) and listed in <see cref="Kinds"/>. The box's <c>Propose…</c>
/// method, which the connector's tool calls, is one call to the kind's writer, which checks the kind's shape and hands
/// its fields to <see cref="HelpProposalFile.Write"/>, which writes every file's head and tail. Every file carries
/// <c>target</c>, <c>workspace</c>, <c>value</c> and <c>sentence</c>, null where the kind has none; a kind's own fields
/// follow them.</para>
///
/// <para><b>Not a partial type (PART7, D166's note on partial types)</b>: a new kind is a writer's file, a line in
/// <see cref="Kinds"/> and a one-line <c>Propose…</c> here, so the connector's tools and the tests keep calling the box.</para>
///
/// <para><b>PERM2's shape</b> (<see cref="RuleProposalBox"/>): a file under the home, never a row in the
/// store, since what it would change is machine-local; one file per proposal, so two never collide.</para>
///
/// <para><b>This checks the shape and nothing more.</b> Whether the route would take it — a line git
/// accepts, a pattern that names one branch per session, a repository registered here, an agent this
/// machine has — is the driver's, which judges each with the route's own code before the person sees
/// it and hands a refusal back to the conversation in the route's words.</para>
///
/// <para><b>It never applies.</b> Every change is the person's press (D89).</para>
///
/// <para><b>THE FILE is the contract</b>: the driver's <c>HelpProposals</c> reads and settles it, shares no
/// code with this, and each side's tests hold the same shape — and the same table of kinds.</para>
/// </remarks>
public sealed class HelpProposalBox(string? home)
{
    /// <summary>
    /// The kinds this box writes, each with the connector's tool that proposes it, in the order the room allows
    /// them (MOD6). A new kind is a file <c>HelpProposalBox.&lt;Kind&gt;.cs</c> holding its writer, a line here and its
    /// <c>Propose…</c>; the driver's <c>HelpProposalKinds</c> lists the same.
    /// </summary>
    public static readonly IReadOnlyList<(string Kind, string Tool)> Kinds =
    [
        ("setting", "setting_propose"),
        ("ask", "ask_propose"),
        ("agent", "agent_propose"),
        ("delete", "delete_propose"),
        ("account", "agent_settings_propose"),
        ("go", "go_propose"),
        ("plugin", "plugin_propose"),
        ("hand", "hand_propose"),
        ("browser", "browser_propose"),
        ("sync", "sync_propose"),
        ("accept", "accept_propose"),
        ("repository", "repository_propose"),
        ("session", "session_propose"),
        ("pause", "pause_propose"),
        ("clear", "clear_propose"),
        ("workflow", "workflow_propose"),
        ("setup", "setup_propose"),
    ];

    private readonly HelpProposalFile file = new(home);

    public string? Home => file.Home;

    /// <summary>The folder a proposal is written in, under the home.</summary>
    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.HelpProposals);

    /// <summary>The driver's named home first — where what this would change lives — then the account's (D63).</summary>
    public static HelpProposalBox FromEnvironment() =>
        new(RuleProposalBox.FromEnvironment().Home);

    /// <inheritdoc cref="HelpSettingProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeSetting(SettingChange change, string why, string? session, DateTimeOffset at) =>
        new HelpSettingProposalWriter(file).Propose(change, why, session, at);

    /// <inheritdoc cref="HelpAskProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeAsk(
        string sentence, string workspace, string why, string? session, DateTimeOffset at,
        string? review = null, string? reviewWords = null, string? shortTitle = null) =>
        new HelpAskProposalWriter(file).Propose(sentence, workspace, why, session, at, review, reviewWords, shortTitle);

    /// <inheritdoc cref="HelpAgentProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeAgent(
        string action, string agent, string? version, string why, string? session, DateTimeOffset at,
        string? account = null,
        string? use = null, string? keep = null, bool noKeep = false, bool? early = null, int? near = null,
        IReadOnlyList<string>? accounts = null, bool clear = false,
        string? switched = null, IReadOnlyList<string>? curve = null, bool noCurve = false) =>
        new HelpAgentProposalWriter(file).Propose(
            action, agent, version, why, session, at, account, use, keep, noKeep, early, near, accounts, clear,
            switched, curve, noCurve);

    /// <inheritdoc cref="HelpDeleteProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeDelete(string? quest, string? ask, string? deleteSession, string why, string? session, DateTimeOffset at) =>
        new HelpDeleteProposalWriter(file).Propose(quest, ask, deleteSession, why, session, at);

    /// <inheritdoc cref="HelpAccountProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeAgentSettings(
        string agent, string account, string? model, string? effort, string why, string? session, DateTimeOffset at) =>
        new HelpAccountProposalWriter(file).Propose(agent, account, model, effort, why, session, at);

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

    /// <inheritdoc cref="HelpHandProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeHand(string target, string? repository, string? plugin, string why, string? session, DateTimeOffset at) =>
        new HelpHandProposalWriter(file).Propose(target, repository, plugin, why, session, at);

    /// <inheritdoc cref="HelpBrowserProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeBrowser(
        string door, string? value, string? address, string? title, string why, string? session, DateTimeOffset at) =>
        new HelpBrowserProposalWriter(file).Propose(door, value, address, title, why, session, at);

    /// <inheritdoc cref="HelpSyncProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeSync(
        string? repository, string why, string? session, DateTimeOffset at, string? workspace = null) =>
        new HelpSyncProposalWriter(file).Propose(repository, why, session, at, workspace);

    /// <inheritdoc cref="HelpAcceptProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeAccept(string quest, string why, string? session, DateTimeOffset at) =>
        new HelpAcceptProposalWriter(file).Propose(quest, why, session, at);

    /// <inheritdoc cref="HelpRepositoryProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeRepository(string repository, string workspace, string why, string? session, DateTimeOffset at) =>
        new HelpRepositoryProposalWriter(file).Propose(repository, workspace, why, session, at);

    /// <inheritdoc cref="HelpSessionProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeSession(string door, string? sessions, bool ended, string why, string? session, DateTimeOffset at) =>
        new HelpSessionProposalWriter(file).Propose(door, sessions, ended, why, session, at);

    /// <inheritdoc cref="HelpPauseProposalWriter.Propose"/>
    public (string? Id, string Message) ProposePause(string door, string? quest, string? ask, string why, string? session, DateTimeOffset at) =>
        new HelpPauseProposalWriter(file).Propose(door, quest, ask, why, session, at);

    /// <inheritdoc cref="HelpClearProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeClear(
        string door, string? quest, string? ask, string? workspace, string why, string? session, DateTimeOffset at) =>
        new HelpClearProposalWriter(file).Propose(door, quest, ask, workspace, why, session, at);

    /// <inheritdoc cref="HelpWorkflowProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeWorkflow(
        string? scope, string? workflow, int? baseVersion, string? steps, bool choose,
        string? changes, string? kind, string? import, string why, string? session, DateTimeOffset at, string? name = null,
        string? graph = null, string? route = null) =>
        new HelpWorkflowProposalWriter(file).Propose(scope, workflow, baseVersion, steps, choose, changes, kind, import, why, session, at, name, graph, route);

    /// <inheritdoc cref="HelpSetupProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeSetup(string repository, string why, string? session, DateTimeOffset at) =>
        new HelpSetupProposalWriter(file).Propose(repository, why, session, at);
}
