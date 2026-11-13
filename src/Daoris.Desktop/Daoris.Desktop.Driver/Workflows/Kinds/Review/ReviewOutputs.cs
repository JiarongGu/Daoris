using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The outputs a review record presents, derived from what it holds: each part's handback, the changes its kind read, a system's plan. A
/// record is the manifest; the outputs are its reading, so an older record answers them as well as a new one.
/// </summary>
public static class ReviewOutputs
{
    /// <summary><paramref name="record"/>'s outputs: the handbacks in the chain's order, then the artifact its kind names.</summary>
    public static IReadOnlyList<ReviewOutput> Of(ReviewRecord record)
    {
        var outputs = record.Parts
            .Select(part => new ReviewOutput($"handback:{part.Quest}", ReviewOutputTypes.Handback, ReviewDisplays.Document, part.Title)
            {
                Part = part.Quest, Digest = Digest($"{part.Machine}/{part.Sequence}/{part.Note}"),
            })
            .ToList();
        var reference = record.Reference;
        string? changes = record.Kind switch
        {
            ReviewKinds.Branch => $"{reference.Base}..{reference.Tip}",
            ReviewKinds.Checkout => string.Join(';', [.. reference.Turns.Select(turn => $"{turn.From}..{turn.To}"), .. reference.Uncommitted.Select(path => $"{path.Path}={path.Sha256}")]),
            ReviewKinds.Folder or ReviewKinds.Copy => $"{reference.Copy}/{reference.Baseline}/{reference.EndDigest}",
            _ => null,
        };
        if (changes is not null) outputs.Add(new(ReviewOutputTypes.Changes, ReviewOutputTypes.Changes, ReviewDisplays.ChangeSet, null) { Digest = Digest(changes) });
        if (record.Kind == ReviewKinds.Plan)
        {
            outputs.Add(new(ReviewOutputTypes.Plan, ReviewOutputTypes.Plan, ReviewDisplays.ResourcePlan, null) { Digest = Digest($"{reference.PlanRecord}/{reference.Plan}/{reference.PlanDigest}") });
        }

        return outputs;
    }

    /// <summary>The outputs added, dropped or captured at another version between two records, in the newer one's order then the dropped.</summary>
    public static IReadOnlyList<string> Delta(ReviewRecord from, ReviewRecord to)
    {
        var before = Of(from).ToDictionary(output => output.Id, output => output.Digest, StringComparer.Ordinal);
        var after = Of(to);
        return
        [
            .. after.Where(output => !before.TryGetValue(output.Id, out var was) || was != output.Digest).Select(output => output.Id),
            .. before.Keys.Where(id => after.All(output => output.Id != id)),
        ];
    }

    private static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
