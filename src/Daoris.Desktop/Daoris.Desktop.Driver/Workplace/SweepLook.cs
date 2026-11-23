namespace Daoris.Driver;

/// <summary>One session branch judged and last looked at: what its removal goes on from.</summary>
internal sealed record SweepLook(
    string Root, SweepItem Now, string Judged, string Line, List<(string Ref, string Commit)> Rests, DateTimeOffset Read,
    List<ContentHolds.TreeHolds> Seen, List<string> Slim);
