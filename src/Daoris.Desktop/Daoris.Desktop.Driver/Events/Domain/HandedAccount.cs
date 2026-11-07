namespace Daoris.Driver;

/// <summary>
/// What a session is handed, section by section: the composer's account of the instruction it
/// composed, kept beside the instruction on the session's record. Not the words again: each section's size, where it came
/// from, and what its bound left out and why.
/// </summary>
/// <remarks>
/// <para><b>Codes where a screen words it, the driver's sentence where a terminal reads it</b>: every
/// section, source, absence and cut is a code the page words in the reader's language, and each carries the driver's own
/// English (<see cref="HandedSection.Said"/>), which <c>daoris trace</c> prints and a page shows, marked, for a code it
/// does not know.</para>
///
/// <para><b>The sizes add up by construction.</b> <see cref="TargetPrompt.Composed"/> builds the instruction from named pieces
/// and counts each piece as it joins them, so the handed sections' characters sum to <see cref="Chars"/>, the instruction's
/// length. What is handed beside the instruction (the permission rules) is a section with no characters.</para>
///
/// <para><b>Values are facts, never a machine path</b>: an id, a repository's name, a repository-relative file, a branch, a
/// language's code, a count, a moment.</para>
/// </remarks>
/// <param name="Chars">The instruction's length in characters, as it is handed.</param>
/// <param name="Sections">
/// The sections handed, in the instruction's order; then what is handed beside it; then the sections that could have been
/// handed and are not, each saying why (a fact not kept is said missing, and an absence is said too).
/// </param>
public sealed record InstructionAccount(int Chars, IReadOnlyList<HandedSection> Sections)
{
    /// <summary>
    /// This account with what is handed beside the instruction (the permission rules), after the sections handed and before
    /// those that are not.
    /// </summary>
    public InstructionAccount Beside(HandedSection beside)
    {
        var handed = Sections.TakeWhile(section => section.None is null).ToList();
        return this with { Sections = [.. handed, beside, .. Sections.Skip(handed.Count)] };
    }
}

/// <summary>One section of what a session is handed: its size, its source, and what its bound left out.</summary>
/// <param name="Name">What part it is, one of <see cref="HandedSections"/>.</param>
/// <param name="Source">Where it came from, one of <see cref="HandedSources"/>.</param>
/// <param name="Said">The driver's English for it, as a terminal prints it.</param>
public sealed record HandedSection(string Name, string Source, string Said)
{
    /// <summary>Its characters in the instruction: 0 for a section not handed, null for what is handed beside it.</summary>
    public int? Chars { get; init; }

    /// <summary>What its source names: a quest's or an ask's id, a repository's name, a repository-relative file, a branch, a language's code.</summary>
    public string? From { get; init; }

    /// <summary>How many of its items it handed: requirements, words, go-aheads, indexes, plan steps, checkouts, rules.</summary>
    public int? Shown { get; init; }

    /// <summary>How many there were to hand.</summary>
    public int? Of { get; init; }

    /// <summary>When its source is set, where the source keeps it (a standing answer).</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>Why nothing of it is handed, one of <see cref="HandedNones"/>; null for a section that is.</summary>
    public string? None { get; init; }

    /// <summary>What is left out of it, each with why; null where nothing is.</summary>
    public IReadOnlyList<HandedCut>? Cuts { get; init; }
}

/// <summary>What a section's bound, or a fact about its source, leaves out.</summary>
/// <param name="Code">Why, one of <see cref="HandedCuts"/>.</param>
/// <param name="Said">The driver's English for it, as a terminal prints it.</param>
public sealed record HandedCut(string Code, string Said)
{
    /// <summary>How many were left out: items, or characters for one cut by its length. The catalogues call it <c>n</c>.</summary>
    public int? Count { get; init; }

    /// <summary>The bound that left them out, in characters or items.</summary>
    public int? Limit { get; init; }

    /// <summary>For words left out, when the first of them is said; for words not kept, from when they are.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>For words left out, when the last of them is said.</summary>
    public DateTimeOffset? To { get; init; }
}
