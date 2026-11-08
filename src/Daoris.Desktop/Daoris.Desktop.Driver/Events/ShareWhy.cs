namespace Daoris.Driver;

/// <summary>
/// Why the files stage did not link a shared path into a tree (<c>started.share-declined</c>), each a reason of the files stage's
/// family (<see cref="NoteCodes.Deps"/>), worded in both catalogues as <c>note.deps.why.share-*</c>. A permission's deny or hold says
/// <see cref="DepsWhy.DeniedBy"/> or <see cref="DepsWhy.HeldFor"/> with the share's act.
/// </summary>
internal static class ShareWhy
{
    internal const string Pattern = "share-pattern";
    internal const string Absent = "share-absent";
    internal const string Unignored = "share-unignored";
    internal const string Repository = "share-repository";
    internal const string PlaceLink = "share-place-link";
    internal const string Standing = "share-standing";
    internal const string FileLink = "share-file-link";
    internal const string Failed = "share-failed";

    internal static DepsReason NoOnePath() => DepsReason.Of(Pattern, "its pattern names no one path, and a share is one place");

    internal static DepsReason PlaceAbsent() => DepsReason.Of(Absent, "your checkout holds nothing there, and Daoris makes no file in it");

    internal static DepsReason NotIgnored() => DepsReason.Of(Unignored, "git does not ignore it in this tree and your checkout, and a path git manages is git's");

    internal static DepsReason InRepository() => DepsReason.Of(Repository, "its place in your checkout is, or lies inside, another repository or tree");

    internal static DepsReason PlaceIsLink() => DepsReason.Of(PlaceLink, "its place in your checkout is itself a link");

    internal static DepsReason StandsThere() => DepsReason.Of(Standing, "something stands at it or on its way in this tree, which is this line's own and is never written over");

    internal static DepsReason NoFileLink() => DepsReason.Of(
        FileLink, "this machine will not make a link to a single file (on Windows, Developer Mode is off), and a copy never stands in for a share");

    internal static DepsReason FailedSaying(string tail) => DepsReason.Of(Failed, $"its link could not be made: {tail}", ("tail", tail));
}
