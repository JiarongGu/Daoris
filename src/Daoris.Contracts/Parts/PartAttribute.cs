namespace Daoris.Contracts.Parts;

/// <summary>
/// Marks a class as a part its root runs: its name, the phase it runs in and the parts it comes after. Each seam declares its
/// own sealed subclass, with its own phase enum, so a root finds its own parts and no other seam's.
/// </summary>
/// <remarks>
/// <see cref="Parts.Declared{TAttribute}"/> reads the marks and <see cref="PartOrder.Of"/> orders them; <c>tools/orient-index.mjs</c>
/// reads the same marks as text into <c>docs/index/registered.md</c>, so a subclass's constructor keeps the name, the phase and the
/// parts it comes after as its first three parameters, in that order, each written as a literal.
/// </remarks>
public abstract class PartAttribute(string name, Enum phase, string[] after) : Attribute
{
    /// <summary>The part's name, unique within its seam: what <see cref="After"/> names, and what the part is timed and listed under.</summary>
    public string Name { get; } = name;

    /// <summary>The phase it runs in. The phases run in their enum's order.</summary>
    public Enum Phase { get; } = phase;

    /// <summary>The parts of its seam it runs after, in its phase or an earlier one.</summary>
    public IReadOnlyList<string> After { get; } = after;
}
