namespace Daoris.Driver;

/// <summary>
/// <c>you</c> as a holder word: a step whose option says it, a request's <c>start</c> or a landing's <c>accept</c>, casts the person as its first
/// holder through its tenure (<see cref="AttemptTenures"/>), so it waits on its role with the person holding it and the settle door admits the
/// person's word. This is the one place that reads the word; nothing else decides by it, the named steps' reading, gate and diff included,
/// which ask it of a version's step as the runtime asks it of a node.
/// </summary>
public static class HolderWords
{
    public const string You = "you";

    private static readonly string[] Options = ["start", "accept"];

    /// <summary>The holder <paramref name="node"/>'s own words cast its first epoch to, or null where they say none and the place's cast holds it.</summary>
    public static string? First(NodeDeclaration node) => Options.Any(name => NodeOptions.Text(node, name) == You) ? CastHolders.Person : null;

    /// <summary>
    /// The holder a version's <paramref name="step"/> casts its first epoch to by the same words, as its fields read them (a field left out
    /// takes its kind's default), or null where they say none: the person's part a reader says is the holder the runtime casts.
    /// </summary>
    public static string? First(NamedStep? step) => step is not null && Options.Any(name => step.Field(name) as string == You) ? CastHolders.Person : null;
}
