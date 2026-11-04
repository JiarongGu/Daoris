namespace Daoris.Driver;

/// <summary>
/// The standing block's facts as a start reads them (CONTEXTONE1c, D230 point 3): the tree's, this machine's for the repository and
/// its workspace, the harness's, and the file the block goes through where the whole first prompt cannot carry it.
/// </summary>
public static class StandingBlocks
{
    /// <summary>The folders a tree keeps its skills in, named where the harness loads none of them itself.</summary>
    private static readonly string[] SkillFolders = [".claude/skills", ".agents/skills"];

    /// <summary>The folder the repository's rules are kept in, each its own row in the knowledge.</summary>
    private const string RulesFolder = ".claude/rules";

    /// <summary>The instruction file a harness may cut.</summary>
    private const string InstructionFile = "AGENTS.md";

    /// <summary>The name of the session's block file in its folder.</summary>
    private const string FileName = "block.md";

    /// <summary>The facts a session working in <paramref name="repository"/> reads from its <paramref name="tree"/>: its indexes and code map.</summary>
    public static StandingFacts OfTree(string repository, string tree) => new()
    {
        Repository = repository,
        CodeMap = CodeMapFile.Find(tree),
        Indexes = RepositoryIndexes.Find(tree),
    };

    /// <summary>
    /// <paramref name="facts"/> with this machine's for its repository and <paramref name="workspace"/>: the routing standing, for a
    /// <paramref name="work"/> that may publish (<see cref="RoutingStanding.Told"/>), and the person's choices.
    /// </summary>
    public static StandingFacts OnMachine(StandingFacts facts, string work, DriverConfig config, IReadOnlyList<RepoView> registry, string? workspace)
    {
        var known = facts.Repository is { } repository
            ? registry.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))
            : null;
        var routing = RoutingStanding.Told(work, config, known);
        return facts with
        {
            Workspace = workspace,
            Routing = routing?.Standing,
            Lanes = routing?.Lanes ?? [],
            Choices = facts.Repository is { } named ? config.StandingFor(named) : null,
        };
    }

    /// <summary>
    /// <paramref name="facts"/> on <paramref name="adapter"/>: whether it takes none of the person's denies (D233 choice 3), and, for a kind
    /// with a repository, the tree's skill folders where it loads none itself and the repository's rules where its instruction file is
    /// longer than the harness reads.
    /// </summary>
    public static StandingFacts OnHarness(StandingFacts facts, ISessionAdapter adapter, string tree)
    {
        facts = facts with { NoDenies = !adapter.TakesSettings };
        if (facts.Repository is not { } repository) return facts;
        IReadOnlyList<string> skills = adapter.LoadsTreeSkills ? [] : [.. SkillFolders.Where(folder => Directory.Exists(Path.Combine(tree, folder)))];
        return facts with { Skills = skills, Rules = adapter.InstructionFileLimit is { } limit ? Rules(repository, tree, limit) : null };
    }

    /// <summary>The session's block file under <paramref name="home"/>, in its own folder, which its rules read and its record's delete takes.</summary>
    public static string FileOf(string home, string session) => Path.Combine(FolderOf(home, session), FileName);

    /// <summary>The session's block folder under <paramref name="home"/>.</summary>
    public static string FolderOf(string home, string session) => DaorisHome.Entry(home, HomeEntry.Spawn, session + StandingFacts.FolderSuffix);

    /// <summary>
    /// Write the block where <paramref name="target"/>'s goes through its file, and say whether it did: a start does this before its
    /// spawn, so the file is there when the session reads the line naming it.
    /// </summary>
    public static bool Hand(SessionTarget target)
    {
        if (target.Block is not { File: { } file } facts || !TargetPrompt.Filed(target)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        AtomicFile.WriteText(file, StandingBlock.Compose(facts).Text + "\n");
        return true;
    }

    /// <summary>The repository's rule ids where its instruction file passes <paramref name="limit"/> bytes; null where it does not.</summary>
    private static RuleIndex? Rules(string repository, string tree, int limit)
    {
        var instruction = new FileInfo(Path.Combine(tree, InstructionFile));
        var folder = Path.Combine(tree, RulesFolder);
        if (!instruction.Exists || instruction.Length <= limit || !Directory.Exists(folder)) return null;
        IReadOnlyList<string> ids = [.. Directory.EnumerateFiles(folder, "*.md").Select(Path.GetFileName).OfType<string>()
            .Order(StringComparer.Ordinal).Select(name => $"{repository}:{RulesFolder}/{name}")];
        return ids.Count == 0 ? null : new RuleIndex(InstructionFile, limit, ids);
    }
}
