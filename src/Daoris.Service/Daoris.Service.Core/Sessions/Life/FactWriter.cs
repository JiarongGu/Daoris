using System.Globalization;
using System.Text;

namespace Daoris.Knowledge;

/// <summary>
/// The writer of the facts a flow of work appends: the application's generation its caller writes in, as the request named it
/// (<c>Daoris-Generation</c>), set for the request's flow by the host's binder and read by the journal's append. The store never stamps a
/// generation of its own, so a process that outlived a restart writes in its own, earlier one; a flow no caller named writes in none (0).
/// </summary>
public static class FactWriter
{
    private static readonly AsyncLocal<long> Writing = new();

    /// <summary>The generation the current flow writes in; 0 for none.</summary>
    public static long Generation => Writing.Value;

    /// <summary>Write in <paramref name="generation"/> for the rest of this flow, until the scope ends.</summary>
    public static IDisposable As(long generation)
    {
        var before = Writing.Value;
        Writing.Value = generation;
        return new WriterScope(before);
    }

    /// <summary>The generation a request's header names, or 0 for none or one that does not read as a positive number.</summary>
    public static long Read(string? header) =>
        long.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out var generation) && generation > 0 ? generation : 0;

    private sealed class WriterScope(long before) : IDisposable
    {
        public void Dispose() => Writing.Value = before;
    }
}

/// <summary>
/// The one spelling of the journal's closed vocabularies on the wire and in the store (kebab case: <c>turn-began</c>,
/// <c>machine-room</c>, <c>needs-you</c>), and its one reader, which knows no other spelling: a value it cannot read is a refusal of its
/// reader, never skipped or guessed.
/// </summary>
public static class Vocabulary<T> where T : struct, Enum
{
    private static readonly Dictionary<T, string> Spelled = Enum.GetValues<T>().ToDictionary(value => value, Kebab);
    private static readonly Dictionary<string, T> Read_ = Spelled.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>The value's spelling.</summary>
    public static string Spell(T value) => Spelled[value];

    /// <summary>The value a spelling names, or null for one this build does not know.</summary>
    public static T? Read(string? spelling) => spelling is not null && Read_.TryGetValue(spelling, out var value) ? value : null;

    private static string Kebab(T value)
    {
        var name = value.ToString();
        var spelled = new StringBuilder(name.Length + 4);
        for (var at = 0; at < name.Length; at++)
        {
            if (char.IsUpper(name[at]) && at > 0) spelled.Append('-');
            spelled.Append(char.ToLowerInvariant(name[at]));
        }

        return spelled.ToString();
    }
}
