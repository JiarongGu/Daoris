using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// The key a workflow's <c>request</c> node publishes its quest under (INTEGRATE1p, the integration design §17.5, R5's C32):
/// <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c>, the run, the node's id and the publication's number. It widens the quest's id
/// (<see cref="QuestStore.MakeId"/>), so two runs asking in the same words make two quests and the same key sent again is its own quest.
/// </summary>
public static partial class QuestRequests
{
    /// <summary>The longest key, in characters.</summary>
    public const int Longest = 200;

    /// <summary>The key as it is kept and why it is refused: null or blank is none; anything not a run, a node and a number from 1 is refused.</summary>
    public static (string? Key, string? Refusal) Judge(string? given)
    {
        if (string.IsNullOrWhiteSpace(given)) return (null, null);
        var key = given.Trim();
        return Fits(key)
            ? (key, null)
            : (null, $"A request's key is `<run>/<node>/<n>`, the run's and the node's ids and the publication's number from 1, at most {Longest} "
                     + $"characters — `{(key.Length > 60 ? key[..60] + "…" : key)}` is not one.");
    }

    /// <summary>Whether a key read back from a store or the wire is one a publish would keep.</summary>
    public static bool Fits(string? key) => key is { Length: > 0 and <= Longest } && Shape().IsMatch(key);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:-]*/[A-Za-z0-9][A-Za-z0-9._:-]*/[1-9][0-9]{0,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
