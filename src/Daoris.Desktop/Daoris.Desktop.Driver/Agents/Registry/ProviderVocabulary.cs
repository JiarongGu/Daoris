namespace Daoris.Driver;

/// <summary>
/// The words each registered provider owns, read off the registration list (the agent-provider design §9, O1-F18): the ids an agent or a
/// door goes by, each with the provider that owns it, and the variables, files and wire keys its <see cref="VocabularyPart"/> declares. The
/// four provider censuses hold every site outside a provider's folder that names one: the driver's reads this, and the service's, the
/// page's and the tools' read it as the contracts' generator writes it into the page's service types (<c>PROVIDER_VOCABULARY</c>), so a
/// provider adds its words by registering.
/// </summary>
public static class ProviderVocabulary
{
    /// <summary>An id an agent or a door goes by, and the provider that owns it.</summary>
    public sealed record IdRow(string Id, string Owner);

    /// <summary>A word a provider owns: <c>variable</c> (an environment variable), <c>file</c> (a file its program reads) or <c>wire</c> (a key on its wire).</summary>
    public sealed record WordRow(string Word, string Owner, string Kind);

    /// <summary>
    /// Every provider's id, the name its own program goes by on the machine, and each of its doors' ids, in the registration list's order,
    /// each once.
    /// </summary>
    public static IReadOnlyList<IdRow> Ids { get; } =
        [.. Providers.Built().SelectMany(IdsOf).DistinctBy(row => row.Id, StringComparer.Ordinal)];

    /// <summary>Every word each provider's vocabulary declares, in the registration list's order.</summary>
    public static IReadOnlyList<WordRow> Words { get; } = [.. Providers.Built().SelectMany(WordsOf)];

    private static IEnumerable<IdRow> IdsOf(Provider provider)
    {
        var owner = provider.Id.Value;
        yield return new IdRow(owner, owner);
        if (provider.Program.TryGet(out var program) && program.Binary is [var name, ..]) yield return new IdRow(name, owner);
        foreach (var door in provider.Doors) yield return new IdRow(door.Id.Value, owner);
    }

    private static IEnumerable<WordRow> WordsOf(Provider provider)
    {
        if (!provider.Vocabulary.TryGet(out var vocabulary)) yield break;
        var owner = provider.Id.Value;
        foreach (var word in vocabulary.Variables) yield return new WordRow(word, owner, "variable");
        foreach (var word in vocabulary.Files) yield return new WordRow(word, owner, "file");
        foreach (var word in vocabulary.WireKeys) yield return new WordRow(word, owner, "wire");
    }
}
