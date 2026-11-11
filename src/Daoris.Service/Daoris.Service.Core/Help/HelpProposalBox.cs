namespace Daoris.Knowledge;

/// <summary>Where Ask Daoris's proposals are written: one file each, under the driver's home, for the person to apply or not.</summary>
/// <remarks>One writer per kind, listed in <see cref="Kinds"/>, hands its checked fields to <see cref="HelpProposalFile.Write"/>; this checks the shape alone, and the driver's <c>HelpProposals</c> judges, reads and settles the file.</remarks>
public sealed class HelpProposalBox(string? home)
{
    /// <summary>The kinds this box writes, each with the connector's tool, in the order the room allows them.</summary>
    public static readonly IReadOnlyList<(string Kind, string Tool)> Kinds =
    [
        ("go", "go_propose"),
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
}
