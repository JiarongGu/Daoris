namespace Daoris.Driver;

/// <summary>
/// The notes for a review set-up's record: each English as its site always wrote it, byte for byte, without the "— " lead the
/// conversation's event adds, with its parts. The service's message and the served sentence are a program's words.
/// </summary>
public static class ReviewNotes
{
    /// <summary>No review rule names the environment any more, so its kind is not known.</summary>
    public static Noted NoRule(string named, string to) =>
        Noted.Of(
            NoteCodes.ReviewNoRule,
            $"its set-up was not posted: no review rule here names `{named}` for `{to}` any more, so its kind is not known. "
            + "Declare it, and its next end posts it.",
            ("named", named), ("to", to));

    /// <summary>The tree's commit could not be read.</summary>
    public static Noted NoCommit() => Noted.Of(NoteCodes.ReviewNoCommit, "its set-up was not posted: its tree's commit could not be read.");

    /// <summary>Posted with the commit Daoris read, the service's message and then what was served, each as its own words.</summary>
    /// <param name="commit">The full commit id; the 8-character short id is the value.</param>
    public static Noted Posted(string commit, string named, string message, string? served)
    {
        var noted = Noted.Of(
                NoteCodes.ReviewPosted, $"Daoris read `{TextBounds.ShortCommit(commit, 8)}` from its tree and posted what it showed in `{named}`:",
                ("commit", TextBounds.ShortCommit(commit, 8)),("named", named))
            .Then(" ", Noted.Said(message, NoteBy.Program));
        return served is { Length: > 0 }
            ? noted.Then(" ", Noted.Said($"{char.ToUpperInvariant(served[0])}{served[1..]}", NoteBy.Program))
            : noted;
    }

    /// <summary>Not posted, the program's message beneath; <paramref name="next"/> where it is the failure the next end retries.</summary>
    public static Noted NotPosted(string message, bool next)
    {
        var noted = Noted.Of(NoteCodes.ReviewNotPosted, "its set-up was not posted:").Then(" ", Noted.Said(message, NoteBy.Program));
        return next ? noted.Then(". ", Noted.Of(NoteCodes.ReviewPostsNext, "Its next end posts it.")) : noted;
    }
}
