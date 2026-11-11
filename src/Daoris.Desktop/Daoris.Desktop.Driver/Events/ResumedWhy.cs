namespace Daoris.Driver;

/// <summary>Whose words a resumed conversation goes on with, by code: the page's <c>note.resumed.why.*</c>.</summary>
public static class ResumedWhy
{
    /// <summary>The person's answer to a park.</summary>
    public const string Answer = "answer";

    /// <summary>The person's words to a conversation that had ended.</summary>
    public const string Words = "words";

    /// <summary>Another agent's findings alone.</summary>
    public const string Findings = "findings";

    /// <summary>The person's answer and another agent's findings.</summary>
    public const string AnswerAndFindings = "answer-and-findings";

    /// <summary>The person's words and another agent's findings.</summary>
    public const string WordsAndFindings = "words-and-findings";

    /// <summary>A helper's answer alone.</summary>
    public const string Helper = "helper";

    /// <summary>The person's words and a helper's answer.</summary>
    public const string WordsAndHelper = "words-and-helper";

    /// <summary>Daoris's own words alone: a reopen told, a step's go-on.</summary>
    public const string Daoris = "daoris";

    /// <summary>Daoris's restart word: the shell closed under a turn, and the conversation goes on by itself at the next start.</summary>
    public const string Restart = "restart";

    /// <summary>The supervisor's answer, on the owner's behalf, with none of the owner's words.</summary>
    public const string Supervisor = "supervisor";
}
