namespace Daoris.Driver;

/// <summary>
/// The model families of an agent, read from what it offers: the words that tell one offered model from another
/// (<c>opus</c>, <c>haiku</c>, <c>fable</c> in <c>claude-opus-5-5</c>, <c>haiku</c>), never a list Daoris keeps. A scope a usage
/// reading names, or a family a limit's sentence names, is a family where it is one of these words; a word every offered model
/// carries (the maker's, <c>claude</c>) tells none apart and is not one.
/// </summary>
public static class ModelFamilies
{
    /// <summary>The families the offered models name, each once, in the order they are first offered.</summary>
    public static IReadOnlyList<string> Of(IEnumerable<string> offered)
    {
        var models = offered.Select(Words).Where(words => words.Count > 0).ToList();
        var common = models.Count > 1
            ? models.Select(words => (IEnumerable<string>)words).Aggregate((left, right) => left.Intersect(right, StringComparer.OrdinalIgnoreCase)).ToList()
            : [];
        return
        [
            .. models.SelectMany(words => words)
                .Where(word => word.Length >= 3 && word.All(char.IsLetter) && !common.Contains(word, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(word => word.ToLowerInvariant()),
        ];
    }

    /// <summary>The families of the models an agent's opens listed on any account, and its settings file's aliases where it keeps one.</summary>
    public static IReadOnlyList<string> Of(IReadOnlyList<OfferReading> readings, IEnumerable<string> agents, ISettingsFile? settings)
    {
        var names = agents.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> offered =
        [
            .. readings.Where(reading => names.Contains(reading.Agent)).SelectMany(reading => reading.Models ?? []),
            .. settings?.Models ?? [],
        ];
        return Of(offered);
    }

    private static List<string> Words(string model)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i <= model.Length; i++)
        {
            var letter = i < model.Length && char.IsLetterOrDigit(model[i]);
            if (letter && start < 0) start = i;
            if (letter || start < 0) continue;
            words.Add(model[start..i]);
            start = -1;
        }

        return words;
    }
}
