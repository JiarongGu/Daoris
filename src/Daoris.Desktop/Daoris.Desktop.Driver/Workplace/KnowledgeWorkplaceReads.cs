using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class KnowledgeWorkplaceReads : IKnowledgeWorkplace
{
    [ModuleInitializer]
    internal static void Install() => KnowledgeWorkplace.Current = new KnowledgeWorkplaceReads();

    public string TreeGuardMatcher => TreeGuard.Matcher;
    public string TreeGuardEditMatcher => TreeGuard.EditMatcher;
    public IReadOnlyList<string> AcceptanceNotes =>
        [LandingRules.PersonAccepted, LandingRules.AutoAccepted, LandingRules.HandNote(new TreeHand(true, "")).Text!];
    public string LandingsFileName => LandedBranches.FileName;
    public string AutoLandingsFileName => AutoLandings.FileName;

    public (IReadOnlyList<LandedBranch> Entries, string Path) Landings(string home)
    {
        var landed = new LandedBranches(home);
        return (landed.Entries(), landed.FilePath);
    }

    public IReadOnlyList<AutoLanding> Due(string home) => new AutoLandings(home).All();

    public Task<(int Code, string Stdout, string Stderr)> GitAsync(string root, IReadOnlyList<string> arguments, TimeSpan patience, CancellationToken ct) =>
        WorkingTree.GitAsync(root, arguments, environment: null, patience, ct);

    public Task<(int Code, string Stdout, string Stderr)> GitAsync(string root, IReadOnlyList<string> arguments, string input, CancellationToken ct) =>
        WorkingTree.GitAsync(root, arguments, input, ct);

    public async Task<HelpTrees> ReadTreesAsync(string home, HelpTreesPart part, CancellationToken ct, LookTimes? times)
    {
        var trees = new SessionTrees(home);
        var pass = TreeReadMemo.Of(home).Begin(trees, times);
        return await HelpFacts.ReadTreesAsync(part, trees.Holds, pass.WorkAsync, ct, times, HelpFacts.TreesAtOnce, TreeFolders.Walkable).ConfigureAwait(false);
    }

    public async Task<IKnowledgeSweep> ReadSweepAsync(string home, HelpSweepPart part, CancellationToken ct) =>
        await new SessionTrees(home).SweepPartAsync(part.Repository, part.Workspace, part.Root, part.InUse, ct, part.ClosedQuests).ConfigureAwait(false);

    public Task<string?> GuessAsync(string root, CancellationToken ct) => LineGuesses.Shared.GuessAsync(root, ct);
    public Line? JudgedLine(DriverConfig config, string repository, string? workspace, string? root, HelpLineGuess? guessed) =>
        CanonicalLine.Judged(config, repository, workspace, root, guessed);
    public Landing ChooseLanding(DriverConfig config, string repository, string? workspace) => LandingRules.Choose(config, repository, workspace);
    public string PullRequestRow(PullRequestState? kept, PullRequestAskFailed? failed) => PullRequestWords.Row(kept, null, failed, null);
    public string? HistoryCount(int count, string noun, string? nouns = null) => HistoryCommand.Count(count, noun, nouns);
    public string? HistoryCopies(int count) => HistoryCommand.Copies(count);
    public string HistoryJoin(IEnumerable<string?> parts) => HistoryCommand.Join(parts);
    public string HistorySize(long bytes) => HistoryCommand.Size(bytes);
}
