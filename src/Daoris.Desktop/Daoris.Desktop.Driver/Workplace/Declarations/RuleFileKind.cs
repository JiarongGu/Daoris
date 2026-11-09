namespace Daoris.Driver;

/// <summary>
/// The rule file's kind: its bytes read as <see cref="RepositoryFilesReader.Read"/> reads the file's text, the rules frozen, and a file
/// or a rule that does not read said as its problems, which are made again at each ask.
/// </summary>
internal sealed class RuleFileKind : IDeclarationKind<RepositoryFilesRead>
{
    // A rule's record and its fields' references, beside the strings it holds.
    private const long Rule = 200;

    internal static readonly RuleFileKind One = new();

    private RuleFileKind()
    {
    }

    public RepositoryFilesRead Parse(DeclarationBytes content) => RepositoryFilesReader.Read(content.Text());

    public bool Problem(RepositoryFilesRead parsed) => parsed.Problems.Count > 0;

    public long Weigh(RepositoryFilesRead parsed) =>
        64 + DeclarationBound.Texts(parsed.Problems) + parsed.Rules.Sum(rule =>
            Rule + DeclarationBound.Texts(rule.Paths) + DeclarationBound.Texts(rule.Setup) + DeclarationBound.Texts(rule.Key)
            + DeclarationBound.Texts(rule.Toolchain) + DeclarationBound.Texts(rule.Env) + DeclarationBound.Texts(rule.Secret)
            + DeclarationBound.Texts(rule.Caches) + DeclarationBound.Texts(rule.Refuses) + DeclarationBound.Texts(rule.Evidence)
            + DeclarationBound.Text(rule.Reach) + DeclarationBound.Text(rule.Take) + DeclarationBound.Text(rule.Finish)
            + DeclarationBound.Text(rule.Install) + DeclarationBound.Text(rule.Per) + DeclarationBound.Text(rule.Builds));
}
