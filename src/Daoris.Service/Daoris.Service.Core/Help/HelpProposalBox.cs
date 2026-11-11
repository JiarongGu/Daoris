namespace Daoris.Knowledge;

/// <summary>Where Ask Daoris's proposals are written: one file each, under the driver's home, for the person to apply or not.</summary>
/// <remarks>
/// <para><b>One writer per kind</b>, listed in <see cref="Kinds"/>, hands its checked fields to <see cref="HelpProposalFile.Write"/>.</para>
/// <para><b>This checks the shape and nothing more.</b> Whether the route would take it is the driver's, which judges each with
/// the route's own code before the person sees it and hands a refusal back to the conversation in the route's words.</para>
/// <para><b>THE FILE is the contract</b>: the driver's <c>HelpProposals</c> reads and settles it; each side's tests hold its shape.</para>
/// </remarks>
public sealed class HelpProposalBox(string? home)
{
    /// <summary>The kinds this box writes, each with the connector's tool, in the order the room allows them.</summary>
    public static readonly IReadOnlyList<(string Kind, string Tool)> Kinds =
    [
        ("go", "go_propose"),
        ("workflow", "workflow_propose"),
    ];

    private readonly HelpProposalFile file = new(home);

    public string? Home => file.Home;

    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.HelpProposals);

    public static HelpProposalBox FromEnvironment() =>
        new(RuleProposalBox.FromEnvironment().Home);

    /// <inheritdoc cref="HelpGoProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeGo(
        string view, string? domain, string? part, string? item, string why, string? session, DateTimeOffset at, string? workspace = null,
        string? repository = null, string? sentence = null) =>
        new HelpGoProposalWriter(file).Propose(view, domain, part, item, why, session, at, workspace, repository, sentence);

    /// <inheritdoc cref="HelpWorkflowProposalWriter.Propose"/>
    public (string? Id, string Message) ProposeWorkflow(
        string? scope, string? workflow, int? baseVersion, string? steps, bool choose,
        string? changes, string? kind, string? import, string why, string? session, DateTimeOffset at, string? name = null,
        string? graph = null, string? route = null, string? draft = null) =>
        new HelpWorkflowProposalWriter(file).Propose(scope, workflow, baseVersion, steps, choose, changes, kind, import, why, session, at, name, graph, route, draft);
}
