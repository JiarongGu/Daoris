namespace Daoris.Driver;

/// <summary>
/// What a session is handed of words a press marked with a choice (PARKVIEW1): a pressed answer as <c>chose: &lt;label&gt;</c>, then the person's own
/// words after a blank line. The words a record keeps (<see cref="SaidWordView.Handed"/>) are read through it; the question's
/// own judgement of a press (<see cref="QuestionWords"/>) answers its <c>Chose</c>, <c>Begins</c> and <c>Handed</c> through here.
/// </summary>
public static class PressedWords
{
    /// <summary>What a pressed answer's words are handed with before the person's own.</summary>
    public const string Chose = "chose: ";

    /// <summary>Whether the words begin with the label: the label alone, or the label and a blank line.</summary>
    public static bool Begins(string text, string label) =>
        string.Equals(text, label, StringComparison.Ordinal) || text.StartsWith(label + "\n\n", StringComparison.Ordinal);

    /// <summary>What the session is handed of words a press marked with <paramref name="choice"/>; words with none, or not so composed, as they are.</summary>
    public static string Handed(string text, string? choice) =>
        choice is { Length: > 0 } label && Begins(text, label) ? Chose + text : text;
}
