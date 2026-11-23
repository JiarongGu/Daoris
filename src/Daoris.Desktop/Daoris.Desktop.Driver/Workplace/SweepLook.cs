namespace Daoris.Driver;

/// <summary>One session branch judged and last looked at; <paramref name="Stamp"/> is its tree's top before the judgement (<see cref="HoldLooks"/>).</summary>
internal sealed record SweepLook(
    string Root, SweepItem Now, string Judged, string Line, List<(string Ref, string Commit)> Rests, DateTimeOffset Read,
    List<ContentHolds.TreeHolds> Seen, List<string> Slim, TreeStamp? Stamp);
