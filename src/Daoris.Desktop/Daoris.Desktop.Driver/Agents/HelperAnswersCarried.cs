namespace Daoris.Driver;

/// <summary>
/// A helper's answer kept on a part of a line that never took it goes with the line to the part that goes on from it, a resume or a
/// new part, and is said there as the helper's answer, never the person's words: left behind, the read stays answered on a record the
/// line has left while the part that goes on reports it still out.
/// </summary>
internal static class HelperAnswersCarried
{
    public static bool Is(SaidWordView word) => word.Helper is { Length: > 0 };

    /// <summary>What the part that goes on is told of each, after the person's and Daoris's words: nothing where it is handed none.</summary>
    public static string Told(IReadOnlyList<SaidWordView> handed) =>
        handed.Where(Is).ToList() is { Count: > 0 } answers
            ? "\nWhile it waited, a helper's answer to that session's read reached it, and it never took it, so the read is answered: weigh "
              + "it as a helper's answer, another agent's claim and not the person's words:\n\n"
              + string.Join("\n\n", answers.Select(word => TextBounds.Quote(word.Text, indent: "  "))) + "\n"
            : "";
}
