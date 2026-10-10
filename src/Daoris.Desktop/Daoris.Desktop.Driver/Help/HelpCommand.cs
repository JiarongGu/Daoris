using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <c>daoris-driver help</c> was asked (ASKHIST1).</summary>
/// <param name="Verb"><c>list</c>, <c>resume</c>, <c>rename</c>, <c>pin</c>, <c>unpin</c> or <c>delete</c>.</param>
public sealed record HelpVerbAsk(string Verb)
{
    /// <summary>The conversation a verb names.</summary>
    public string? Id { get; init; }

    /// <summary>A resume's words, or a rename's name, joined by a space.</summary>
    public string? Text { get; init; }

    /// <summary>A resume's files, each as the person named its path.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>A listing's words to find.</summary>
    public string? Search { get; init; }

    /// <summary>The listing as the screen's answer.</summary>
    public bool Json { get; init; }

    /// <summary>A listing's <c>--offset</c>: where its page starts in the whole list (ASKHIST1d).</summary>
    public int Offset { get; init; }

    /// <summary>A listing's <c>--limit</c>: the most its page holds, or null for <see cref="HelpConversations.PageLimit"/>.</summary>
    public int? Limit { get; init; }

    /// <summary>A rename's <c>--clear</c>: its first question is its title again.</summary>
    public bool Clear { get; init; }

    /// <summary>A delete's <c>--yes</c>: delete what the first ask listed.</summary>
    public bool Yes { get; init; }
}

/// <summary>
/// <c>daoris-driver help</c> (ASKHIST1, D50): Ask Daoris's history from a terminal, the panel's other door. It lists and searches
/// this machine's Ask Daoris conversations through <see cref="HelpConversations"/>, the one reader the panel's route reads; says
/// words to one, which goes on in its own conversation, through <c>sessions say</c>'s own door; names, pins and unpins one; and
/// deletes one through <c>sessions delete</c>'s owner, listed first and pressed with <c>--yes</c>, as the panel asks once.
/// </summary>
/// <remarks>
/// <para><b>Only Ask Daoris's own conversations of this machine's</b>: an id that names any other session is refused, exit 1,
/// naming the <c>sessions</c> verb that acts on it.</para>
///
/// <para><b>Not here: a new conversation from an earlier one.</b> A conversation with Ask Daoris runs in the window's panel, and
/// no terminal door opens one; the panel's history is where one starts from another's words.</para>
///
/// <para>Exit codes are the family's: 0 done or listed · 1 refused · 2 could not, the usage among them.</para>
/// </remarks>
public static class HelpCommand
{
    public const string Usage =
        """
        usage: daoris-driver help list [--search "…"] [--json] [--offset <n>] [--limit <n>]
               daoris-driver help resume <id> "…" [--file <path>]…
               daoris-driver help rename <id> "…" | --clear  ·  help pin <id>  ·  help unpin <id>
               daoris-driver help delete <id> [--yes]
        """;

    /// <summary>The verbs this door answers; <c>help</c> alone, or anything else after it, is the host's usage.</summary>
    public static IReadOnlySet<string> Verbs { get; } = new HashSet<string>(StringComparer.Ordinal) { "list", "resume", "rename", "pin", "unpin", "delete" };

    /// <summary>The fields of a conversation in <c>--json</c>, in order: <c>HELP_CONVERSATIONS</c>' row, field for field.</summary>
    public static IReadOnlyList<string> JsonFields { get; } =
        ["session", "title", "name", "opening", "about", "created", "last", "pinned", "live", "resumable", "from", "handed", "found", "foundLine"];

    /// <summary>
    /// How much of a conversation's line the terminal's listing shows (ASKHIST1d). The terminal shows it raw, so it cuts it to a
    /// line itself; <c>--json</c> carries it whole as far as <see cref="HelpConversations.PreviewLimit"/>, as the page's route does.
    /// </summary>
    public const int LineLimit = 160;

    /// <summary>What the words after <c>help</c> ask, or null with what is wrong with them.</summary>
    public static HelpVerbAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        var words = string.Join(' ', args.Skip(2)).Trim();
        switch (args)
        {
            case ["list", ..]:
                return ReadList([.. args.Skip(1)], out problem);
            case ["resume", ..]:
                return ReadResume(args, out problem);
            case ["rename", var id, "--clear"] when SessionEvents.IsId(id):
                return new HelpVerbAsk("rename") { Id = id, Clear = true };
            case ["rename", var id, _, ..] when SessionEvents.IsId(id) && words.Length > 0 && !args.Skip(2).Contains("--clear"):
                return new HelpVerbAsk("rename") { Id = id, Text = words };
            case ["rename", ..]:
                problem = "`rename` takes one conversation's id, then its name, or `--clear`.";
                return null;
            case ["pin" or "unpin", var id] when SessionEvents.IsId(id):
                return new HelpVerbAsk(args[0]) { Id = id };
            case ["pin" or "unpin", ..]:
                problem = $"`{args[0]}` takes one conversation's id.";
                return null;
            case ["delete", var id] when SessionEvents.IsId(id):
                return new HelpVerbAsk("delete") { Id = id };
            case ["delete", var id, "--yes"] when SessionEvents.IsId(id):
                return new HelpVerbAsk("delete") { Id = id, Yes = true };
            case ["delete", ..]:
                problem = "`delete` takes one conversation's id, and `--yes` once you have read what it takes.";
                return null;
        }

        problem = "`help` takes `list`, `resume`, `rename`, `pin`, `unpin` or `delete`.";
        return null;
    }

    private static HelpVerbAsk? ReadList(IReadOnlyList<string> rest, out string? problem)
    {
        problem = null;
        var ask = new HelpVerbAsk("list");
        for (var at = 0; at < rest.Count; at++)
        {
            switch (rest[at])
            {
                case "--json":
                    ask = ask with { Json = true };
                    break;
                case "--search" when at + 1 < rest.Count && rest[at + 1].Trim().Length > 0:
                    ask = ask with { Search = rest[++at].Trim() };
                    break;
                case "--search":
                    problem = "`--search` takes the words to find.";
                    return null;
                case "--offset" when at + 1 < rest.Count && int.TryParse(rest[at + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var offset):
                    ask = ask with { Offset = offset };
                    at++;
                    break;
                case "--offset":
                    problem = "`--offset` takes where the page starts: a whole number, 0 for the first.";
                    return null;
                case "--limit" when at + 1 < rest.Count && int.TryParse(rest[at + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var limit) && limit is > 0 and <= HelpConversations.PageLimit:
                    ask = ask with { Limit = limit };
                    at++;
                    break;
                case "--limit":
                    problem = $"`--limit` takes how many a page holds: a whole number from 1 to {HelpConversations.PageLimit}.";
                    return null;
                default:
                    problem = $"`{rest[at]}` is not a word `help list` takes.";
                    return null;
            }
        }

        return ask;
    }

    /// <summary><c>resume &lt;id&gt; "…" [--file &lt;path&gt;]…</c>, read as <c>sessions say</c> reads its words.</summary>
    private static HelpVerbAsk? ReadResume(IReadOnlyList<string> args, out string? problem)
    {
        if (SessionsCommand.Read(["say", .. args.Skip(1)], out _) is { } say)
        {
            problem = null;
            return new HelpVerbAsk("resume") { Id = say.Ids[0], Text = say.Text, Files = say.Files };
        }

        problem = "`resume` takes one conversation's id, then your words, and `--file <path>` for each file you give with them.";
        return null;
    }

    public static async Task<int> RunAsync(HelpVerbAsk ask, SessionsWorld world, Func<string, bool> resumes, TextWriter output, CancellationToken ct = default)
    {
        var json = await world.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var records = SessionRecords.Parse(json);
        var events = new SessionEvents(Path.Combine(world.Home, "sessions"));
        if (ask.Verb == "list")
        {
            var listing = new HelpConversations(world.Home).List(
                records, events, resumes, ask.Search, ask.Offset, ask.Limit ?? HelpConversations.PageLimit);
            output.WriteLine(ask.Json ? Json(listing) : Listed(listing, ask, DateTimeOffset.UtcNow));
            return 0;
        }

        var id = ask.Id!;
        var record = records.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null || record.Teammate || !WordsNever.IsHelp(record))
        {
            output.WriteLine(record is null
                ? $"help: no session here is {id}."
                : $"help: {id} is no Ask Daoris conversation of this machine's; `daoris-driver sessions` acts on the others.");
            return 1;
        }

        var kept = new HelpConversations(world.Home);
        switch (ask.Verb)
        {
            case "resume":
                // The words go where the panel's box sends them: the say door, and the same conversation goes on with them.
                return await SessionsCommand.RunAsync(
                    new SessionsAsk("say") { Ids = [id], Text = ask.Text, Files = ask.Files }, world, output, ct).ConfigureAwait(false);
            case "rename":
                try
                {
                    kept.Rename(id, ask.Clear ? null : ask.Text);
                }
                catch (DriverException refused)
                {
                    output.WriteLine($"help: {refused.Message}");
                    return 1;
                }

                output.WriteLine(ask.Clear
                    ? $"help: {id} is called by its first question again."
                    : $"help: {id} is named “{ask.Text}”.");
                return 0;
            case "pin" or "unpin":
                kept.Pin(id, ask.Verb == "pin", DateTimeOffset.UtcNow);
                output.WriteLine(ask.Verb == "pin" ? $"help: {id} is pinned to the top of the list." : $"help: {id} is unpinned.");
                return 0;
            default:
                if (record.Live)
                {
                    output.WriteLine($"help: {id} is still {record.State}; finish it or stop it in the panel first, then delete it.");
                    return 1;
                }

                if (!ask.Yes)
                {
                    var title = kept.Read(id).Name ?? events.Openings([id]).GetValueOrDefault(id) ?? id;
                    output.WriteLine(
                        $"help: deleting {id} “{title}” removes its record and what this machine kept of it: its words, its transcript, "
                        + "its files, its name and its pin. It cannot be undone. `daoris-driver help delete " + id + " --yes` deletes it.");
                    return 0;
                }

                return await SessionsCommand.RunAsync(new SessionsAsk("delete") { Ids = [id] }, world, output, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The listing in the terminal's words: each conversation's id, when it was last spoken in, its title and marks, then its line;
    /// and, for a page that does not hold them all, which it holds of how many and the words that list the next.
    /// </summary>
    internal static string Listed(HelpListing listing, HelpVerbAsk ask, DateTimeOffset now)
    {
        var search = ask.Search;
        if (listing.Conversations.Count == 0)
        {
            return listing.Total > 0 ? $"help: the list holds {listing.Total}, so none from {ask.Offset + 1} on."
                : search is { Length: > 0 } words ? $"help: no Ask Daoris conversation on this machine holds “{words}”."
                : "help: Ask Daoris has no conversations on this machine yet.";
        }

        var text = new StringBuilder();
        foreach (var row in listing.Conversations)
        {
            var marks = new List<string>();
            if (row.Pinned is not null) marks.Add("pinned");
            if (row.Live) marks.Add("running");
            else if (!row.Resumable) marks.Add("starts anew");
            if (row.From is { } from) marks.Add($"from {from}");
            text.Append(row.Session).Append("  ").Append(Ago(row.Last, now)).Append("  ").Append(row.Title);
            if (marks.Count > 0) text.Append("  · ").Append(string.Join(" · ", marks));
            text.Append('\n');
            if (row.About is { } about) text.Append("    ").Append(about.Length <= LineLimit ? about : $"{about[..LineLimit]}…").Append('\n');
            if (row.Found is { } found && found != row.Title) text.Append("    found: ").Append(found).Append('\n');
        }

        if (ask.Offset > 0 || listing.Next is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"{ask.Offset + 1}–{ask.Offset + listing.Conversations.Count} of {listing.Total} are listed");
            if (listing.Next is { } next)
            {
                text.Append("; `daoris-driver help list")
                    .Append(search is { Length: > 0 } words ? $" --search \"{words}\"" : "")
                    .Append(CultureInfo.InvariantCulture, $" --offset {next}")
                    .Append(ask.Limit is { } limit ? string.Create(CultureInfo.InvariantCulture, $" --limit {limit}") : "")
                    .Append("` lists the next");
            }

            text.Append(".\n");
        }

        return text.ToString().TrimEnd('\n');
    }

    /// <summary>How long ago, in the terminal's short words.</summary>
    internal static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var span = at == DateTimeOffset.MinValue || at > now ? TimeSpan.Zero : now - at;
        return span.TotalMinutes < 1 ? "just now"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes}m ago"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours}h ago"
            : $"{(int)span.TotalDays}d ago";
    }

    /// <summary>
    /// The written form of a listing, as <c>HELP_CONVERSATIONS</c> answers it:
    /// <c>{"conversations": [...], "cut": …, "total": …, "next": … | null}</c>.
    /// </summary>
    public static string Json(HelpListing listing)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("conversations");
            foreach (var row in listing.Conversations)
            {
                writer.WriteStartObject();
                writer.WriteString("session", row.Session);
                writer.WriteString("title", row.Title);
                Text(writer, "name", row.Name);
                Text(writer, "opening", row.Opening);
                Text(writer, "about", row.About);
                writer.WriteString("created", row.Created.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("last", row.Last.ToString("O", CultureInfo.InvariantCulture));
                Text(writer, "pinned", row.Pinned?.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteBoolean("live", row.Live);
                writer.WriteBoolean("resumable", row.Resumable);
                Text(writer, "from", row.From);
                Text(writer, "handed", row.Handed);
                Text(writer, "found", row.Found);
                Text(writer, "foundLine", row.FoundLine);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteBoolean("cut", listing.Cut);
            writer.WriteNumber("total", listing.Total);
            if (listing.Next is { } next) writer.WriteNumber("next", next);
            else writer.WriteNull("next");
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");

        static void Text(Utf8JsonWriter writer, string name, string? value)
        {
            if (value is null) writer.WriteNull(name);
            else writer.WriteString(name, value);
        }
    }
}
