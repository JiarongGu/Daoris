using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One thing an Ask Daoris conversation's Apply published (ASKQUESTS1, the Ask Daoris design §9 item 12): when, the proposal applied,
/// its kind (<c>ask</c> or <c>setup</c>), the workspace it was asked at, and the ask and the quest by the ids the service answered. Ids
/// and when only: their state is the service's, read live.
/// </summary>
public sealed record HelpPublishedEntry(DateTimeOffset At, string Proposal, string Kind, string? Workspace)
{
    /// <summary>The ask it published, by id; null where the answer named none.</summary>
    public string? Ask { get; init; }

    /// <summary>The quest it published, by id; null where the answer named none (an ask its intake has not answered yet).</summary>
    public string? Quest { get; init; }
}

/// <summary>
/// What an Apply published, as its kind hands it to <see cref="HelpProposals.ApplyAsync"/> (ASKQUESTS1): the ask and the quest the
/// service's answer named, and the workspace they were asked at.
/// </summary>
public sealed record HelpPublishing(string? Workspace, string? Ask, string? Quest)
{
    /// <summary>Whether the answer named anything to keep: an answer with neither id leaves nothing to open.</summary>
    public bool Names => Ask is not null || Quest is not null;
}

/// <summary>
/// The asks and quests one Ask Daoris conversation published (ASKQUESTS1), kept in <c>&lt;home&gt;/sessions/&lt;id&gt;.published.json</c>
/// beside its other files, oldest first, and answered newest first; a delete or a history clear takes the file with the rest
/// (<see cref="SessionHomeFiles"/>, under Ask Daoris's own name). A conversation that applied before this row has none.
/// </summary>
/// <remarks>
/// <para><b>A file of its own, not a field of <see cref="HelpConversations"/>' file.</b> A build before this one rewrites that file whole
/// at a rename or a pin, with the four fields it knows, and would drop a fifth (<c>compatible-builds</c>); no such build knows this one.</para>
///
/// <para><b>Written where the Apply runs, and read there.</b> The desktop host's Apply keeps an entry (<see cref="HelpProposals.ApplyAsync"/>)
/// and its <c>HELP_PUBLISHED</c> reads them; no terminal door applies a proposal (D89), and neither the service nor a terminal reads it.</para>
///
/// <para><b>A file that does not read is nothing kept</b>, as Ask Daoris's other files are: it lists nothing, and the next keep starts it
/// again. An entry is read back field by field.</para>
/// </remarks>
public sealed class HelpPublished(string home)
{
    public const string Suffix = ".published.json";

    // One writer at a time in this process: two Applies of one conversation's cards would otherwise each drop the other's entry.
    private static readonly object Gate = new();

    /// <summary>Where a conversation's list is kept, beside its transcript and Ask Daoris's other files for it.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, Checked(session) + Suffix);

    /// <summary>Add what an Apply published after what is kept.</summary>
    /// <exception cref="DriverException">An id that names no session.</exception>
    public void Keep(string session, HelpPublishedEntry entry)
    {
        lock (Gate)
        {
            var kept = Oldest(session);
            JsonFile.Write(PathOf(session), writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("published");
                foreach (var each in kept.Append(entry)) Write(writer, each);
                writer.WriteEndArray();
                writer.WriteEndObject();
            });
        }
    }

    /// <summary>A conversation's list, the newest first; none for a conversation from before, an id no session's, or a file that does not read.</summary>
    public IReadOnlyList<HelpPublishedEntry> Read(string session) => SessionEventText.IsId(session) ? [.. Oldest(session).Reverse()] : [];

    private IReadOnlyList<HelpPublishedEntry> Oldest(string session)
    {
        if (!SessionEventText.IsId(session)) return [];
        try
        {
            var path = PathOf(session);
            if (!File.Exists(path)) return [];
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];
            return [.. Json.Items(document.RootElement, "published").Select(ReadEntry).OfType<HelpPublishedEntry>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>An entry as it was written; one with no proposal, no kind or neither id is no entry.</summary>
    private static HelpPublishedEntry? ReadEntry(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var proposal = Json.NonEmpty(element, "proposal");
        var kind = Json.NonEmpty(element, "kind");
        var ask = Json.NonEmpty(element, "ask");
        var quest = Json.NonEmpty(element, "quest");
        if (proposal is null || kind is null || (ask is null && quest is null)) return null;
        return new HelpPublishedEntry(Moments.Read(element, "at") ?? DateTimeOffset.MinValue, proposal, kind, Json.NonEmpty(element, "workspace"))
        {
            Ask = ask,
            Quest = quest,
        };
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives; an absent id left out.</summary>
    private static void Write(Utf8JsonWriter writer, HelpPublishedEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("at", Moments.Write(entry.At));
        writer.WriteString("proposal", entry.Proposal);
        writer.WriteString("kind", entry.Kind);
        if (entry.Workspace is { } workspace) writer.WriteString("workspace", workspace);
        if (entry.Ask is { } ask) writer.WriteString("ask", ask);
        if (entry.Quest is { } quest) writer.WriteString("quest", quest);
        writer.WriteEndObject();
    }

    private static string Checked(string session) =>
        SessionEventText.IsId(session) ? session : throw new DriverException($"`{session}` names no session.");
}
