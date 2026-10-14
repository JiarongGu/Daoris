using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// One Ask Daoris proposal's file under the home (HELP1c, D89), as every kind's writer writes it: its head and its tail
/// around the kind's own fields, and the small rules the writers share (PART7).
/// </summary>
/// <remarks>
/// The kinds' writers (<c>Help&lt;Kind&gt;ProposalWriter</c>) import the shared rules with <c>using static</c>, so each
/// writer reads as the rules of its own kind; <see cref="Write"/> is the one way a file is written.
/// </remarks>
internal sealed class HelpProposalFile(string? home)
{
    public string? Home { get; } = home;

    /// <summary>The answer to a proposal with no reason, which every kind refuses first.</summary>
    internal static readonly (string? Id, string Message) NoReason =
        (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");

    /// <summary>Why a name is not one word, or null when it is: every name a proposal carries is.</summary>
    internal static string? Word(string value, string what) =>
        value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) ? $"{what} is one word — `{value}` is not." : null;

    /// <summary>One proposal's file: its id, when and by whom, the kind's own fields, its reason and its state.</summary>
    public (string? Id, string Message) Write(Action<Utf8JsonWriter> body, string why, string? session, DateTimeOffset at)
    {
        if (Home is null) return (null, $"{Capital(DaorisHome.Sentence)} Nothing was proposed.");

        var id = Guid.NewGuid().ToString("N")[..8];
        var directory = HelpProposalBox.FolderOf(Home);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{id}.json");

        // Hand-rolled, for the reason every store here is: nothing may stop working under AOT.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("proposed", Moments.Write(at));
            writer.WriteStartObject("by");
            Nullable(writer, "session", session);
            writer.WriteEndObject();
            body(writer);
            writer.WriteString("why", why.Trim());
            writer.WriteString("state", "proposed");
            writer.WriteNull("note");
            writer.WriteEndObject();
        }

        AtomicFile.WriteText(path, Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");

        return (id,
            $"Proposed `#{id}`. The person sees it as a card saying what it changes and the command that does the "
            + "same, with Apply and Not now; nothing changes until they press Apply, and their answer comes back to "
            + "you as their next message. If the driver finds the route would refuse it, you are told why instead.");
    }

    internal static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static void Nullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    internal static string Capital(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];
}
