namespace Daoris.Driver;

/// <summary>A piece of a brief: the section it is counted in, and its words.</summary>
public readonly record struct BriefPiece(string Section, string Text);

/// <summary>A brief as composed: its pieces in the order they read, and its account, each section's size, source and cuts.</summary>
public sealed record ComposedBrief(IReadOnlyList<BriefPiece> Pieces, IReadOnlyList<HandedSection> Sections)
{
    /// <summary>The brief whole.</summary>
    public string Text => string.Concat(Pieces.Select(piece => piece.Text));

    /// <summary>The session it continues: the part moved from, the last of its line.</summary>
    public string? Continues { get; init; }

    /// <summary>
    /// Where the line's parts keep the files the person attached, which the brief names: the new part's rules read exactly these
    /// folders (the design §3.1 item 5).
    /// </summary>
    public IReadOnlyList<string> Kept { get; init; } = [];

    /// <summary>Its CONTEXT1 account (D143 point 1): its sections sum to its length, by construction.</summary>
    public InstructionAccount Account => new(Text.Length, Sections);
}
