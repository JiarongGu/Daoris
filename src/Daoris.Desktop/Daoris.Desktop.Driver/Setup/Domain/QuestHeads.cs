namespace Daoris.Driver;

/// <summary>The opening of a quest's brief as a card shows it: words, where the brief is markdown.</summary>
public static class QuestHeads
{
    /// <summary>
    /// <paramref name="markdown"/> as plain words in one line: the bold marks gone, each line trimmed of its quote mark, and the lines run
    /// together by one space. Backticks stay, as the card shows a name between them.
    /// </summary>
    public static string Plain(string markdown) => string.Join(' ', markdown.Replace("**", "", StringComparison.Ordinal)
        .ReplaceLineEndings("\n").Split('\n')
        .Select(line => line.Trim().TrimStart('>').Trim())
        .Where(line => line.Length > 0));
}
