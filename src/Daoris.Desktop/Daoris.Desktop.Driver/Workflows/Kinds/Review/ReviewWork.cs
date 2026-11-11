using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A run's work as its review reads it: its kind, the artifact's reference and every part's handback, and the revision over them that a
/// review's binding holds. Read from the run's own records (its branch, its turn log, its sessions' plans, its quests), never from the place
/// as it stands, so what the person does there between turns moves no revision; a corrected handback at the same content does.
/// </summary>
/// <param name="Kind">One of <see cref="ReviewKinds"/>.</param>
public sealed record ReviewWork(string Kind, string Place, ReviewReference Reference, IReadOnlyList<ReviewPart> Parts)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Revision => RevisionOf(Kind, Reference, Parts);

    /// <summary>Whether <paramref name="scope"/>'s run works on a branch, whose review stays bound to it at its tip, held or not.</summary>
    public static bool OnBranch(RunScope scope) => scope.Branch is not null || scope.Branches.Count > 0;

    /// <summary>
    /// <paramref name="scope"/>'s work in <paramref name="home"/>: its branch at <paramref name="tip"/> where it works on one; else its sessions'
    /// newest plan; else what its newest turn worked in, a copy, a folder or a checkout; else its handbacks alone.
    /// </summary>
    public static ReviewWork Read(string home, RunScope scope, string? tip)
    {
        var (run, place, parts) = (scope.Run.Binding.Run, scope.Run.Binding.Repository, Handbacks(scope.Quests));
        if (scope.Branch?.Records.Branch is { } grown) return new(ReviewKinds.Branch, place, new() { Branch = grown.Branch, Line = grown.Line, Tip = tip }, parts);
        if (PlanReview.Reference(home, scope.Sessions) is { } plan) return new(ReviewKinds.Plan, place, plan, parts);
        var turns = RunTurns.Of(home, RemoteTarget.Workspace(scope.Run.Binding.Workspace), place, run);
        if (turns.LastOrDefault() is not { } newest) return new(ReviewKinds.Answer, place, new(), parts);
        if (newest.Kind == PlaceKind.Git) return new(ReviewKinds.Checkout, place, CheckoutReview.Reference(turns), parts);
        return FolderCopies.IdAt(home, newest.Root) is { } copy
            ? new(ReviewKinds.Copy, place, FolderReview.CopyReference(copy, newest), parts)
            : new(ReviewKinds.Folder, place, FolderReview.Reference(run, turns), parts);
    }

    /// <summary>The record of attempt <paramref name="attempt"/> of <paramref name="node"/>, its rows and what is incomplete composed by its kind's reader.</summary>
    public ReviewRecord Compose(string home, string run, string node, int attempt, DateTimeOffset at)
    {
        var turns = Kind is ReviewKinds.Folder or ReviewKinds.Copy ? RunTurns.Of(home, Reference.Workspace!, Reference.Department!, run) : [];
        var (rows, incomplete, coverage) = Kind switch
        {
            ReviewKinds.Folder => FolderReview.Rows(home, Reference, turns),
            ReviewKinds.Copy => FolderReview.CopyRows(home, Reference),
            ReviewKinds.Plan => ([], PlanReview.Gaps(home, Reference), ReviewCoverage.Whole),
            _ => ((IReadOnlyList<ReviewRow>)[], (IReadOnlyList<ReviewGap>)[], ReviewCoverage.Whole),
        };
        return new ReviewRecord(run, node, attempt, Kind, Revision, at)
        {
            Place = Place, Reference = Reference, Parts = Parts, Rows = rows, Incomplete = incomplete, Coverage = coverage,
        };
    }

    /// <summary>
    /// A digest over what the kind's artifact holds and each handback's id: the tip; a checkout's first start, newest end and own paths; a
    /// folder's baseline and newest end by content; a copy's base and newest end; a plan's record, number and content.
    /// </summary>
    public static string RevisionOf(string kind, ReviewReference reference, IReadOnlyList<ReviewPart> parts)
    {
        object key = kind switch
        {
            ReviewKinds.Branch => new { reference.Branch, reference.Tip },
            ReviewKinds.Checkout => new { From = reference.Turns.FirstOrDefault()?.From, To = reference.Turns.LastOrDefault()?.To, reference.Uncommitted },
            ReviewKinds.Folder or ReviewKinds.Copy => new { reference.Copy, reference.Baseline, reference.EndDigest },
            ReviewKinds.Plan => new { reference.PlanRecord, reference.Plan, reference.PlanDigest },
            _ => new { },
        };
        var handbacks = parts.Select(part => $"{part.Quest}/{part.Machine}/{part.Sequence}");
        var text = JsonSerializer.Serialize(new { kind, key, handbacks }, Json);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Each quest of the chain here with its taker's submission and the requirements it carried.</summary>
    private static List<ReviewPart> Handbacks(IReadOnlyList<QuestView> quests) =>
    [
        .. quests.Select(quest => new ReviewPart(quest.Id, quest.Title)
        {
            Session = quest.Submission?.Session,
            Machine = quest.Submission?.Machine,
            Sequence = quest.Submission?.Sequence,
            At = quest.Submission?.At,
            Note = quest.Submission?.Note ?? quest.Note,
            Commit = quest.Submission?.Commit,
            Requirements = quest.Requirements,
            Unchecked = quest.Requirements.Count == 0,
        }),
    ];
}
