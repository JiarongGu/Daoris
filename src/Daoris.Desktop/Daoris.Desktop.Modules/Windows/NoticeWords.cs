using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// The words a main window's page handed the shell's notifier (NOTIFY2d, D196, the notification design §3.3): a headline and a body
/// per kind of notification and the acts of the <c>Person</c> routes, each by its catalogue key and in the page's language, their
/// placeholders kept for the notifier to fill (<see cref="SessionBalloon"/>). The shell has no catalogue of its own, so the page hands
/// these at its load and at each change of language over <c>SET_NOTICE_WORDS</c>, the way <c>SET_BADGE</c> takes its description.
/// </summary>
/// <remarks>
/// <para><b>Kept whole as last told.</b> A telling replaces the last, so a page that changed its language is one telling and no word of
/// the other language is left behind. Any main window's page tells it; every main window's page speaks the one language the machine
/// chose, so the last told is the window's language. Until a page tells it there are none, and a balloon says English.</para>
///
/// <para><b>Bounded.</b> A page hands a few hundred words; past <see cref="MostWords"/> or <see cref="Longest"/> a word is not kept,
/// so a page cannot make the shell hold what it likes. Only strings are words, and a blank one is none. Read and told from any thread:
/// the dictionary is replaced, never changed in place.</para>
/// </remarks>
public sealed class NoticeWords
{
    /// <summary>The most words kept: every kind's two and every person's route's act, with room to grow.</summary>
    public const int MostWords = 1000;

    /// <summary>The longest key, and the longest word, kept.</summary>
    public const int Longest = 1000;

    private IReadOnlyDictionary<string, string>? _words;

    /// <summary>The words as last told, by catalogue key; null where no page has told any.</summary>
    public IReadOnlyDictionary<string, string>? Now => Volatile.Read(ref _words);

    /// <summary>A page told its words, <c>{ key: words }</c>: they replace the last. Anything but an object of them hands none.</summary>
    public void Told(JsonElement words)
    {
        if (words.ValueKind != JsonValueKind.Object)
        {
            Volatile.Write(ref _words, null);
            return;
        }

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var each in words.EnumerateObject())
        {
            if (kept.Count >= MostWords) break;
            if (each.Value.ValueKind != JsonValueKind.String || each.Name.Length > Longest) continue;
            var text = each.Value.GetString()!;
            if (string.IsNullOrWhiteSpace(text) || text.Length > Longest) continue;
            kept[each.Name] = text;
        }

        Volatile.Write(ref _words, kept);
    }
}
