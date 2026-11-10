using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <c>daoris help</c> asks.</summary>
/// <param name="Verb"><c>list</c>, <c>rename</c>, <c>pin</c>, <c>unpin</c>, <c>delete</c> or <c>mode</c>.</param>
public sealed record HelpVerbAsk(string Verb)
{
    /// <summary>The conversation a verb names.</summary>
    public string? Id { get; init; }

    /// <summary>A rename's name, or a mode's, joined by a space.</summary>
    public string? Text { get; init; }

    /// <summary>A listing's words to find.</summary>
    public string? Search { get; init; }

    /// <summary>The listing as the screen's answer.</summary>
    public bool Json { get; init; }

    /// <summary>A listing's <c>--offset</c>: where its page starts in the whole list.</summary>
    public int Offset { get; init; }

    /// <summary>A listing's <c>--limit</c>: the most its page holds, or null for <see cref="HelpConversations.PageLimit"/>.</summary>
    public int? Limit { get; init; }

    /// <summary>A listing's <c>--workspace</c>: the workspace whose conversations it lists, or null for every one's.</summary>
    public string? Workspace { get; init; }

    /// <summary>A listing's <c>--all</c>: every workspace's, said outright, as a listing that names none is.</summary>
    public bool All { get; init; }

    /// <summary>A listing's <c>--about</c>: the place, as the terminal spells it, whose conversations it lists (<see cref="HelpFoci.About"/>).</summary>
    public string? About { get; init; }

    /// <summary>A rename's <c>--clear</c>: its first question is its title again.</summary>
    public bool Clear { get; init; }

    /// <summary>A delete's <c>--yes</c>: delete what the first ask listed.</summary>
    public bool Yes { get; init; }
}

/// <summary>
/// <c>daoris help</c>: Ask Daoris's history from a terminal, the panel's other door. It lists and searches
/// this machine's Ask Daoris conversations through <see cref="HelpConversations"/>, the one reader the panel's route reads; names,
/// pins and unpins one; and deletes one through <c>sessions delete</c>'s owner, listed first and pressed with <c>--yes</c>, as the panel asks once.
/// </summary>
/// <remarks>
/// <para><b>Only Ask Daoris's own conversations of this machine's</b>: an id that names any other session is refused, exit 1,
/// naming the <c>sessions</c> verb that acts on it. Words to one are <c>session say</c>'s, and <c>chat --session</c> holds one.</para>
///
/// <para><b>Not here: a new conversation from an earlier one.</b> A conversation with Ask Daoris runs in the window's panel, and
/// no terminal door opens one; the panel's history is where one starts from another's words.</para>
///
/// <para>Exit codes are the family's: 0 done or listed · 1 refused · 2 could not, the usage among them.</para>
/// </remarks>
[CommandGroup("ask-daoris", "list", "rename", "pin", "unpin", "delete", "mode", "focus")]
public static class HelpCommand
{
    public const string Usage =
        """
        usage: daoris ask-daoris list [--search "…"] [--json] [--offset <n>] [--limit <n>] [--workspace <name> | --all | --about <place>]
               daoris ask-daoris rename <id> "…" | --clear  ·  daoris ask-daoris pin <id>  ·  daoris ask-daoris unpin <id>
               daoris ask-daoris focus <id> machine | <workspace> | <workspace>/<repository>
               daoris ask-daoris delete <id> [--yes]
               daoris ask-daoris mode [restricted|full]
        """;

    /// <summary>The verbs this door answers; <c>help</c> alone, or anything else after it, is the host's usage.</summary>
    public static IReadOnlySet<string> Verbs { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "list", "rename", "pin", "unpin", "delete", "mode", "focus" };

    /// <summary>
    /// The fields of a conversation in <c>--json</c>, in order: <c>HELP_CONVERSATIONS</c>' row, field for field.
    /// Its <c>workspace</c>, the one it started in, follows <c>mode</c>, and its <c>focus</c> and <c>focusNext</c> follow that.
    /// </summary>
    public static IReadOnlyList<string> JsonFields { get; } =
    [
        "session", "title", "name", "opening", "about", "created", "last", "pinned", "live", "mode", "workspace", "focus", "focusNext", "marks",
        "resumable", "from", "handed", "found", "foundLine", "parts",
    ];

    /// <summary>
    /// How much of a conversation's line the terminal's listing shows. The terminal shows it raw, so it cuts it to a
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
            case ["rename", var id, "--clear"] when SessionEventText.IsId(id):
                return new HelpVerbAsk("rename") { Id = id, Clear = true };
            case ["rename", var id, _, ..] when SessionEventText.IsId(id) && words.Length > 0 && !args.Skip(2).Contains("--clear"):
                return new HelpVerbAsk("rename") { Id = id, Text = words };
            case ["rename", ..]:
                problem = "`rename` takes one conversation's id, then its name, or `--clear`.";
                return null;
            case ["pin" or "unpin", var id] when SessionEventText.IsId(id):
                return new HelpVerbAsk(args[0]) { Id = id };
            case ["pin" or "unpin", ..]:
                problem = $"`{args[0]}` takes one conversation's id.";
                return null;
            case ["delete", var id] when SessionEventText.IsId(id):
                return new HelpVerbAsk("delete") { Id = id };
            case ["delete", var id, "--yes"] when SessionEventText.IsId(id):
                return new HelpVerbAsk("delete") { Id = id, Yes = true };
            case ["delete", ..]:
                problem = "`delete` takes one conversation's id, and `--yes` once you have read what it takes.";
                return null;
            case ["mode"]:
                return new HelpVerbAsk("mode");
            case ["mode", var mode] when HelpModes.Named(mode) is { } named:
                return new HelpVerbAsk("mode") { Text = named };
            case ["mode", ..]:
                problem = $"`mode` takes `{HelpModes.Restricted}` or `{HelpModes.Full}`, or nothing to say which Ask Daoris runs in.";
                return null;
            case ["focus", var id, var place] when SessionEventText.IsId(id) && HelpFocus.FromSaid(place) is { } focus:
                return new HelpVerbAsk("focus") { Id = id, Text = focus.Said };
            case ["focus", ..]:
                problem = "`focus` takes one conversation's id, then `machine`, a workspace, or a workspace's repository as `<workspace>/<repository>`.";
                return null;
        }

        problem = "`help` takes `list`, `rename`, `pin`, `unpin`, `focus`, `delete` or `mode`.";
        return null;
    }

    /// <summary>
    /// <c>help mode [restricted|full]</c>: which access mode Ask Daoris runs in, or the person's choice
    /// of one, written to the driver's file at <paramref name="configPath"/> and taken at its next start. Asks no service.
    /// </summary>
    /// <remarks>
    /// A conversation already open keeps the mode it started in until its next start: words to it once it ended resume it
    /// under the mode chosen then, and its record and transcript say the change.
    /// </remarks>
    /// <exception cref="DriverConfigUnreadableException">The file does not read, so nothing is written.</exception>
    public static int Mode(HelpVerbAsk ask, string configPath, TextWriter output)
    {
        var config = DriverConfig.Load(configPath);
        if (ask.Text is not { } mode)
        {
            output.WriteLine($"help: {Says(config.HelpMode)}");
            return 0;
        }

        if (mode == config.HelpMode)
        {
            output.WriteLine($"help: Ask Daoris already runs {Named(mode)}.");
            return 0;
        }

        config.WithHelpMode(mode).Save(configPath);
        output.WriteLine($"help: from its next start, {Says(mode)}");
        // Written from another process, the mode reaches no running conversation, unlike the window's press.
        output.WriteLine("help: A conversation running now keeps its mode until its next start; the window's choice ends one at its turn's end.");
        return 0;

        static string Named(string mode) => mode == HelpModes.Full ? "with full access" : "restricted";

        static string Says(string mode) => mode == HelpModes.Full
            ? "Ask Daoris runs with full access: it acts on this machine itself, with a shell, file edits, Daoris's commands and every "
              + "plugin server, and asks nothing; your own denies still hold. `daoris ask-daoris mode restricted` takes it back."
            : "Ask Daoris runs restricted: it reads, advises and proposes, and you apply. `daoris ask-daoris mode full` lets it act "
              + "on this machine itself.";
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
                // One workspace's, every one's, or one place's; never two, and each said once.
                case "--workspace" or "--all" or "--about" when ask.Workspace is not null || ask.All || ask.About is not null:
                    problem = "`help list` takes one of `--workspace <name>`, `--all` and `--about <place>`, once.";
                    return null;
                case "--about" when at + 1 < rest.Count && !rest[at + 1].StartsWith('-') && HelpFocus.FromSaid(rest[at + 1]) is { } about:
                    ask = ask with { About = about.Said };
                    at++;
                    break;
                case "--about":
                    problem = "`--about` takes one place: `machine`, a workspace, or `<workspace>/<repository>`.";
                    return null;
                case "--workspace" when at + 1 < rest.Count && !rest[at + 1].StartsWith('-') && rest[at + 1].Trim() is { Length: > 0 } workspace:
                    ask = ask with { Workspace = workspace };
                    at++;
                    break;
                case "--workspace":
                    problem = "`--workspace` takes one workspace's name.";
                    return null;
                case "--all":
                    ask = ask with { All = true };
                    break;
                default:
                    problem = $"`{rest[at]}` is not a word `help list` takes.";
                    return null;
            }
        }

        return ask;
    }

    /// <summary>
    /// The verb, as the running Daoris's client where its world names an instance to look for:
    /// <c>rename</c>, <c>pin</c>, <c>unpin</c> and <c>delete --yes</c> are the person's presses, which the window confirms, and <c>list</c> a
    /// read; each route asked for its words runs this verb's own path in that Daoris, so the lines are the alone path's. A delete's first
    /// ask reads here; alone where none runs.
    /// </summary>
    public static async Task<int> RunAsync(HelpVerbAsk ask, SessionsWorld world, Func<string, bool> resumes, TextWriter output, CancellationToken ct = default)
    {
        (InstanceRoute Route, Dictionary<string, object?> Payload)? press = ask.Verb switch
        {
            "list" => (OpenRoutes.HelpConversations, InstanceDoor.Said(
                ("q", ask.Search), ("offset", ask.Offset == 0 ? null : ask.Offset), ("limit", ask.Limit), ("workspace", ask.Workspace),
                ("all", ask.All ? true : null), ("about", HelpFocus.FromSaid(ask.About)?.Wire()), ("json", ask.Json ? true : null))),
            "rename" => (PersonPresses.HelpRename, InstanceDoor.Said(("id", ask.Id), ("name", ask.Clear ? null : ask.Text))),
            "pin" or "unpin" => (PersonPresses.HelpPin, InstanceDoor.Said(("id", ask.Id), ("pinned", ask.Verb == "pin"))),
            "focus" => (PersonPresses.HelpFocus, InstanceDoor.Said(("session", ask.Id), ("focus", HelpFocus.FromSaid(ask.Text)?.Wire()))),
            // SESSION_DELETE's own delete, asked to say it as `help delete` does: its checks first, then `sessions delete`'s words.
            "delete" when ask.Yes => (PersonPresses.SessionDelete, InstanceDoor.Said(("id", ask.Id), ("help", true))),
            _ => null,
        };

        // Alone, nothing this verb runs looks for the instance again.
        var alone = world with { Instance = null };
        return press is { } sent
            ? await InstanceDoor.ThroughAsync(world.Instance, sent.Route, sent.Payload, () => AloneAsync(ask, alone, resumes, output, ct), output, ct)
                .ConfigureAwait(false)
            : await AloneAsync(ask, alone, resumes, output, ct).ConfigureAwait(false);
    }

    private static async Task<int> AloneAsync(HelpVerbAsk ask, SessionsWorld world, Func<string, bool> resumes, TextWriter output, CancellationToken ct)
    {
        var json = await world.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var records = SessionRecords.Parse(json);
        var events = new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions));
        if (ask.Verb == "list")
        {
            // The workspace named, or every one's; a terminal stands in no workspace, so naming none is every one's.
            var listing = new HelpConversations(world.Home).List(
                records, events, resumes, ask.Search, ask.Offset, ask.Limit ?? HelpConversations.PageLimit, ask.Workspace,
                HelpFocus.FromSaid(ask.About));
            output.WriteLine(ask.Json ? Json(listing) : Listed(listing, ask, DateTimeOffset.UtcNow));
            return 0;
        }

        var id = ask.Id!;
        if (ask.Verb == "focus")
        {
            var registry = await world.Service.RegistryAsync(ct).ConfigureAwait(false);
            var chosen = new HelpFocusing(world.Home).Choose(id, records, registry, events, resumes, HelpFocus.FromSaid(ask.Text)!);
            output.WriteLine(chosen.Line);
            return chosen.Refused is null ? 0 : 1;
        }

        var record = records.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null || record.Teammate || !WordsNever.IsHelp(record))
        {
            output.WriteLine(record is null
                ? $"help: no session here is {id}."
                : $"help: {id} is no Ask Daoris conversation of this machine's; `daoris session list` acts on the others.");
            return 1;
        }

        var kept = new HelpConversations(world.Home);
        switch (ask.Verb)
        {
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
                        + "its files, its name and its pin. It cannot be undone. `daoris ask-daoris delete " + id + " --yes` deletes it.");
                    return 0;
                }

                return await SessionsCommand.RunAsync(new SessionsAsk("delete") { Ids = [id] }, world, output, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The listing in the terminal's words: each conversation's id, when last spoken in, its title and marks, then its line;
    /// and, for a page that does not hold them all, which it holds of how many and the words that list the next.
    /// </summary>
    /// <remarks>
    /// Row for row the panel's: a listing of every workspace's labels each row with its workspace, last among its
    /// marks, as the panel's *All workspaces* does; one workspace's says it where it holds none, and the next page's words keep it.
    /// </remarks>
    internal static string Listed(HelpListing listing, HelpVerbAsk ask, DateTimeOffset now)
    {
        var search = ask.Search;
        var circle = ask.Workspace;
        if (listing.Conversations.Count == 0)
        {
            return listing.Total > 0 ? $"help: the list holds {listing.Total}, so none from {ask.Offset + 1} on."
                : search is { Length: > 0 } words ? circle is null
                    ? $"help: no Ask Daoris conversation on this machine holds “{words}”."
                    : $"help: no Ask Daoris conversation in the workspace `{circle}` holds “{words}”."
                : circle is null ? "help: Ask Daoris has no conversations on this machine yet."
                : $"help: Ask Daoris has no conversations in the workspace `{circle}` yet; `--all` lists every workspace's.";
        }

        var text = new StringBuilder();
        foreach (var row in listing.Conversations)
        {
            var marks = new List<string>();
            if (row.Pinned is not null) marks.Add("pinned");
            if (row.Live) marks.Add("running");
            else if (!row.Resumable) marks.Add("starts anew");
            // The mode it ran under: full access marked, restricted, the default, not.
            if (row.Mode == HelpModes.Full) marks.Add("full access");
            if (row.From is { } from) marks.Add($"from {from}");
            if (circle is null) marks.Add($"in {row.Workspace}");
            if (!row.Focus.Equals(HelpFocus.Home(row.Workspace))) marks.Add($"focus {row.Focus.Said}");
            if (row.FocusNext is { } next) marks.Add($"focus {next.Said} next");
            text.Append(row.Session).Append("  ").Append(Ago(row.Last, now)).Append("  ").Append(row.Title);
            if (marks.Count > 0) text.Append("  · ").Append(string.Join(" · ", marks));
            text.Append('\n');
            if (row.About is { } about) text.Append("    ").Append(TextBounds.Clip(about, LineLimit)).Append('\n');
            if (row.Found is { } found && found != row.Title) text.Append("    found: ").Append(found).Append('\n');
        }

        if (ask.Offset > 0 || listing.Next is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"{ask.Offset + 1}–{ask.Offset + listing.Conversations.Count} of {listing.Total} are listed");
            if (listing.Next is { } next)
            {
                text.Append("; `daoris ask-daoris list")
                    .Append(circle is not null ? $" --workspace {Word(circle)}" : ask.All ? " --all" : "")
                    .Append(search is { Length: > 0 } words ? $" --search \"{words}\"" : "")
                    .Append(CultureInfo.InvariantCulture, $" --offset {next}")
                    .Append(ask.Limit is { } limit ? string.Create(CultureInfo.InvariantCulture, $" --limit {limit}") : "")
                    .Append("` lists the next");
            }

            text.Append(".\n");
        }

        return text.ToString().TrimEnd('\n');

        // A workspace's name as the next line types it: quoted where a space or a quote would split it.
        static string Word(string name) => name.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'') ? $"\"{name.Replace("\"", "\\\"")}\"" : name;
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
        // The answer is printed, not kept: the file form's final newline is left off (JsonFile).
        return JsonFile.Serialize(writer =>
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
                writer.WriteString("created", Moments.Write(row.Created));
                writer.WriteString("last", Moments.Write(row.Last));
                Text(writer, "pinned", Moments.Write(row.Pinned));
                writer.WriteBoolean("live", row.Live);
                writer.WriteString("mode", row.Mode);
                writer.WriteString("workspace", row.Workspace);
                writer.WritePropertyName("focus");
                row.Focus.Write(writer);
                writer.WritePropertyName("focusNext");
                if (row.FocusNext is { } waiting) waiting.Write(writer);
                else writer.WriteNullValue();
                writer.WriteStartArray("marks");
                foreach (var mark in row.Marks) mark.Write(writer);
                writer.WriteEndArray();
                writer.WriteBoolean("resumable", row.Resumable);
                Text(writer, "from", row.From);
                Text(writer, "handed", row.Handed);
                Text(writer, "found", row.Found);
                Text(writer, "foundLine", row.FoundLine);
                writer.WriteStartArray("parts");
                foreach (var part in row.Parts) writer.WriteStringValue(part);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteBoolean("cut", listing.Cut);
            writer.WriteNumber("total", listing.Total);
            if (listing.Next is { } next) writer.WriteNumber("next", next);
            else writer.WriteNull("next");
            writer.WriteEndObject();
        }).TrimEnd('\n');

        static void Text(Utf8JsonWriter writer, string name, string? value)
        {
            if (value is null) writer.WriteNull(name);
            else writer.WriteString(name, value);
        }
    }
}
