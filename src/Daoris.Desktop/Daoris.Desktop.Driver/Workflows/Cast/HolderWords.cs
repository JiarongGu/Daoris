namespace Daoris.Driver;

/// <summary>
/// <c>you</c> as a holder word: a step whose option says it, a request's <c>start</c> or a landing's <c>accept</c>, casts the person as its first
/// holder through its tenure (<see cref="AttemptTenures"/>), so it waits on its role with the person holding it and the settle door admits the
/// person's word. This is the one place that reads the word, of a node and of a version's step alike; nothing else decides by it.
/// </summary>
public static class HolderWords
{
    public const string You = "you";

    private static readonly string[] Options = ["start", "accept"];

    /// <summary>The holder <paramref name="node"/>'s own words cast its first epoch to, or null where they say none and the place's cast holds it.</summary>
    public static string? First(NodeDeclaration node) => Options.Any(name => NodeOptions.Text(node, name) == You) ? CastHolders.Person : null;

    /// <summary>The holder a version's <paramref name="step"/> casts its first epoch to by the same words, as its fields read them, or null.</summary>
    public static string? First(NamedStep? step) => step is not null && Options.Any(name => step.Field(name) as string == You) ? CastHolders.Person : null;
}
