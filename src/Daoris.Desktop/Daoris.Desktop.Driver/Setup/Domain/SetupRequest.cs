namespace Daoris.Driver;

/// <summary>
/// The set-up brief's opening paragraph, what is asked and whose it is: composed into the body by <see cref="SetupBrief"/> and shown on a card
/// before the quest is asked, so both say the same words.
/// </summary>
public static class SetupRequest
{
    /// <param name="kind">The case the brief is composed for.</param>
    /// <param name="declares">Whether the line already declares what it owns, which a move's wording turns on.</param>
    public static string Of(SetupCase kind, bool declares = false)
    {
        var what = kind switch
        {
            SetupCase.Whole =>
                "Take up the shared doctrine in the layout every agent reads, and write down what this repository owns "
                + "and promises, so that a session here, or in a neighbouring repository, finds its answers in files "
                + "rather than asking a person.",
            SetupCase.Move =>
                "Move this repository's doctrine to the layout every agent reads"
                + (declares ? "" : ", and write down what this repository owns and promises")
                + ", so that a session here, or in a neighbouring repository, finds its answers in files rather than "
                + "asking a person.",
            _ =>
                "Write down what this repository owns and promises, so that a session in a neighbouring repository finds "
                + "its answers here rather than asking a person. The doctrine is already taken up here, on the layout "
                + "every agent reads: this is the knowledge step alone.",
        };

        return "**What is asked, and whose it is.** " + what
            + " This is this repository's own change, made by its own session on its own branch: nothing was "
            + "written here from outside, and nothing outside this tree is this session's to change.";
    }
}
