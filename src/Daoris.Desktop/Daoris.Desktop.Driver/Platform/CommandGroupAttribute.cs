namespace Daoris.Driver;

/// <summary>
/// A group of the <c>daoris</c> command tree, declared on the library unit that runs it, or whose words the host's console reads for it.
/// <see cref="CommandWords.Groups"/> collects every mark of this assembly at start, and a name marked twice fails that first use.
/// </summary>
/// <param name="name">The group, the first word a person types.</param>
/// <param name="verbs">Its sub-verbs.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CommandGroupAttribute(string name, params string[] verbs) : Attribute
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Verbs { get; } = verbs;

    /// <summary>What the group reads where its first word is no sub-verb (a new ask's words, a role's id); null where it needs one.</summary>
    public string? Takes { get; set; }

    /// <summary>Whether the group's own command judges every word after it, its sub-verbs listed for the usage only.</summary>
    public bool Whole { get; set; }

    /// <summary>A sub-verb's own sub-verbs, each written <c>"verb: sub sub …"</c> (<c>"account: list add …"</c>).</summary>
    public string[] Nested { get; set; } = [];
}
