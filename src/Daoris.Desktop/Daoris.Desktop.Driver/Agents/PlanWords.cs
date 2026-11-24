namespace Daoris.Driver;

/// <summary>
/// An agent says one plan in more than one word: its type (<c>max</c>, a usage reading's) and its tier (<c>max_20x</c>, a status's).
/// The tier is the type said more exactly, so the two are one plan.
/// </summary>
internal static class PlanWords
{
    /// <summary>Whether <paramref name="exact"/> is <paramref name="type"/> said more exactly: its tier.</summary>
    public static bool Refines(string exact, string type) =>
        exact.StartsWith(type + "_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether two words name one plan: equal, or one the tier of the other.</summary>
    public static bool Same(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || Refines(a, b) || Refines(b, a);
}
