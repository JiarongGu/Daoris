namespace Daoris.Driver;

/// <summary>A judged sweep kept by the facts; Knowledge neither interprets nor rewrites its plan.</summary>
public interface IKnowledgeSweep;

internal interface IKnowledgeWorkplace
{
    string TreeGuardMatcher { get; }
    string TreeGuardEditMatcher { get; }
    IReadOnlyList<string> AcceptanceNotes { get; }
    string LandingsFileName { get; }
    string AutoLandingsFileName { get; }
    (IReadOnlyList<LandedBranch> Entries, string Path) Landings(string home);
    IReadOnlyList<AutoLanding> Due(string home);
    Task<(int Code, string Stdout, string Stderr)> GitAsync(string root, IReadOnlyList<string> arguments, TimeSpan patience, CancellationToken ct);
    Task<(int Code, string Stdout, string Stderr)> GitAsync(string root, IReadOnlyList<string> arguments, string input, CancellationToken ct);
    Task<HelpTrees> ReadTreesAsync(string home, HelpTreesPart part, CancellationToken ct, LookTimes? times);
    Task<IKnowledgeSweep> ReadSweepAsync(string home, HelpSweepPart part, CancellationToken ct);
    Task<string?> GuessAsync(string root, CancellationToken ct);
    Line? JudgedLine(DriverConfig config, string repository, string? workspace, string? root, HelpLineGuess? guessed);
    Landing ChooseLanding(DriverConfig config, string repository, string? workspace);
    string PullRequestRow(PullRequestState? kept, PullRequestAskFailed? failed);
    string? HistoryCount(int count, string noun, string? nouns = null);
    string? HistoryCopies(int count);
    string HistoryJoin(IEnumerable<string?> parts);
    string HistorySize(long bytes);
}

internal static class KnowledgeWorkplace
{
    private static IKnowledgeWorkplace? _current;

    internal static IKnowledgeWorkplace Current
    {
        get => _current ?? throw new InvalidOperationException("no reading of the workplace is installed");
        set => _current = value;
    }
}
