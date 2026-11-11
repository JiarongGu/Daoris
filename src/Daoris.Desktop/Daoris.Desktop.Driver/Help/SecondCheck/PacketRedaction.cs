using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a packet's text loses before it is sent: address credentials and secrets (<see cref="WiringRedact"/>), a registered checkout's path
/// as its repository's name, the home folder as <c>~</c>, each path in every spelling a record holds it.
/// </summary>
public sealed record PacketRedaction(IReadOnlyCollection<string> Secrets, IReadOnlyDictionary<string, string> Checkouts, string? UserHome)
{
    public static PacketRedaction None { get; } = new([], new Dictionary<string, string>(), null);

    public string Apply(string? text)
    {
        var cut = WiringRedact.Text(text ?? "", Secrets)!;
        foreach (var (name, root) in Checkouts.OrderByDescending(each => each.Value.Length)) cut = Rooted(cut, root, $"[{name}]");
        return UserHome is { Length: > 0 } home ? Rooted(cut, home, "~") : cut;
    }

    private static string Rooted(string text, string root, string said)
    {
        var trimmed = root.TrimEnd('\\', '/');
        if (trimmed.Length < 3) return text;
        var spellings = new[] { trimmed, trimmed.Replace('\\', '/'), trimmed.Replace('/', '\\'), trimmed.Replace('/', '\\').Replace("\\", "\\\\", StringComparison.Ordinal) };
        foreach (var spelling in spellings.Distinct(StringComparer.Ordinal).OrderByDescending(each => each.Length))
        {
            text = Regex.Replace(text, Regex.Escape(spelling) + @"(?=$|[\\/""'\s:;,)\]}])", said.Replace("$", "$$", StringComparison.Ordinal), RegexOptions.IgnoreCase);
        }

        return text;
    }
}
