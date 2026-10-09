using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A session whose work a second opinion is owed on by itself (XAGENT1f, D155 point 2; the second-agent design §2.1): its quest
/// done in a tree of its own under a rule whose occasions read it. The look asks the pass, delivers it, and closes the entry once
/// the work landed, its tree went, or the rule no longer asks.
/// </summary>
/// <param name="Occasion"><c>landing</c>, or <c>steps</c> where only the chain's next step waits for it.</param>
/// <param name="Tree">The tree its record names, under this home's trees (D51): machine-local, like the file that keeps it.</param>
public sealed record OpinionDue(
    string Session, string Quest, string Repository, string Workspace, string Tree, string Occasion, DateTimeOffset At)
{
    /// <summary>When the look closed it, and why; null while it is owed.</summary>
    public DateTimeOffset? Closed { get; init; }

    public string? Why { get; init; }
}

/// <summary>Why the look closed an opinion owed (<see cref="OpinionDue"/>), each a code the file keeps.</summary>
public static class OpinionDueClosed
{
    /// <summary>The work landed, by any door.</summary>
    public const string Landed = "landed";

    /// <summary>Its tree is gone.</summary>
    public const string Gone = "gone";

    /// <summary>Its quest is no longer done.</summary>
    public const string Undone = "undone";

    /// <summary>The rule standing for its repository no longer reads at this occasion.</summary>
    public const string Off = "off";

    /// <summary>The chain's next step started: what <c>steps</c> held is past.</summary>
    public const string Past = "past";
}

/// <summary>
/// The opinions owed by themselves on this machine (XAGENT1f): <c>&lt;home&gt;/opinions/due.json</c>. Written at a driven record's
/// conclusion, read and closed by the look. Machine-local (D47 §4), as the opinions are.
/// </summary>
public sealed class OpinionDues(string home)
{
    public const string FileName = "due.json";

    /// <summary>How many closed entries are kept, the newest by when they closed; an open entry is never dropped.</summary>
    public const int ClosedKept = 200;

    // One writer at a time in this process: the look and a conclusion may write at once.
    private static readonly object Gate = new();

    public string FilePath => Path.Combine(home, "opinions", FileName);

    /// <summary>Every entry, in the order they became due.</summary>
    public IReadOnlyList<OpinionDue> All()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (!document.RootElement.TryGetProperty("due", out var due) || due.ValueKind != JsonValueKind.Array) return [];
            return [.. due.EnumerateArray().Select(Read).OfType<OpinionDue>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The entries still owed.</summary>
    public IReadOnlyList<OpinionDue> Open() => [.. All().Where(entry => entry.Closed is null)];

    /// <summary>A session's work owed an opinion: one owed before for the same session and occasion is opened again.</summary>
    public void Due(OpinionDue entry) => Edit(all =>
    {
        all.RemoveAll(each => Same(each.Session, entry.Session) && each.Occasion == entry.Occasion);
        all.Add(entry);
    });

    /// <summary>Close a session's entries for an occasion, with why.</summary>
    public void Close(string session, string occasion, string why, DateTimeOffset at) => Edit(all =>
    {
        for (var i = 0; i < all.Count; i++)
        {
            if (Same(all[i].Session, session) && all[i].Occasion == occasion && all[i].Closed is null)
            {
                all[i] = all[i] with { Closed = at, Why = why };
            }
        }
    });

    private void Edit(Action<List<OpinionDue>> change)
    {
        lock (Gate)
        {
            var all = All().ToList();
            change(all);
            var closed = all.Where(entry => entry.Closed is not null).OrderByDescending(entry => entry.Closed).Skip(ClosedKept).ToHashSet();
            all.RemoveAll(closed.Contains);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            AtomicFile.WriteText(FilePath, Json(all));
        }
    }

    private static string Json(IReadOnlyList<OpinionDue> all)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("due");
            foreach (var entry in all)
            {
                writer.WriteStartObject();
                writer.WriteString("session", entry.Session);
                writer.WriteString("quest", entry.Quest);
                writer.WriteString("repository", entry.Repository);
                writer.WriteString("workspace", entry.Workspace);
                writer.WriteString("tree", entry.Tree);
                writer.WriteString("occasion", entry.Occasion);
                writer.WriteString("at", OpinionViews.Moment(entry.At));
                if (entry.Closed is { } closed) writer.WriteString("closed", OpinionViews.Moment(closed));
                if (entry.Why is not null) writer.WriteString("why", entry.Why);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        // LF on every platform, as every file under the home is written.
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    private static OpinionDue? Read(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && Kept.Text(element, "session") is { } session && Kept.Text(element, "quest") is { } quest
        && Kept.Text(element, "repository") is { } repository && Kept.Text(element, "tree") is { } tree
        && Kept.Text(element, "occasion") is { } occasion && Kept.Moment(element, "at") is { } at
            ? new OpinionDue(session, quest, repository, Kept.Text(element, "workspace") ?? RemoteTarget.DefaultWorkspace, tree, occasion, at)
            {
                Closed = Kept.Moment(element, "closed"),
                Why = Kept.Text(element, "why"),
            }
            : null;

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

/// <summary>What the driver keeps of a chain's work in one repository at the gate (XAGENT1f): each pass asked, each the person asked
/// for and not yet started, and each answer of theirs.</summary>
public sealed record OpinionGateKept
{
    public IReadOnlyList<OpinionAskKept> Asks { get; init; } = [];

    public IReadOnlyList<OpinionRequest> Requests { get; init; } = [];

    public IReadOnlyList<OpinionPersonWord> Person { get; init; } = [];

    public static OpinionGateKept None { get; } = new();
}

/// <summary>
/// The gate's own record (XAGENT1f; the second-agent design §8.5–§8.6): <c>&lt;home&gt;/opinions/gates/&lt;chain&gt;.json</c>, one per
/// chain, named by its first quest, with each repository's part apart. What the person said at the gate is kept here, on this
/// machine (D47 §4), and on the landing record when the work lands: the opinion itself is the local host's, which no door here
/// writes the person's answer into.
/// </summary>
public sealed class OpinionGates(string home)
{
    // One writer at a time in this process: the look, a press and the terminal's verbs may write at once.
    private static readonly object Gate = new();

    /// <summary>Where a chain's record is kept; null for a name that is no plain id.</summary>
    public string? PathOf(string chain) =>
        Regex.IsMatch(chain, "^[A-Za-z0-9_-]{1,64}$") ? Path.Combine(home, "opinions", "gates", $"{chain}.json") : null;

    /// <summary>What is kept for the chain's work in a repository; none where nothing reads.</summary>
    public OpinionGateKept Read(string chain, string repository) =>
        ReadAll(chain).TryGetValue(repository, out var kept) ? kept : OpinionGateKept.None;

    /// <summary>A pass asked, kept in the order asked.</summary>
    public void Asked(string chain, string repository, OpinionAskKept ask) =>
        Edit(chain, repository, kept => kept with { Asks = [.. kept.Asks, ask] });

    /// <summary>A pass the person asked for, kept until the next look starts it.</summary>
    public void Requested(string chain, string repository, OpinionRequest request) =>
        Edit(chain, repository, kept => kept with { Requests = [.. kept.Requests, request] });

    /// <summary>One session's requests, taken by the look, which starts the newest; the others stay for their own sessions.</summary>
    public IReadOnlyList<OpinionRequest> Take(string chain, string repository, string session)
    {
        IReadOnlyList<OpinionRequest> taken = [];
        Edit(chain, repository, kept =>
        {
            taken = [.. kept.Requests.Where(request => string.Equals(request.Session, session, StringComparison.OrdinalIgnoreCase))];
            return kept with { Requests = [.. kept.Requests.Where(request => !taken.Contains(request))] };
        });
        return taken;
    }

    /// <summary>An answer of the person's at the gate.</summary>
    public void Said(string chain, string repository, OpinionPersonWord word) =>
        Edit(chain, repository, kept => kept with { Person = [.. kept.Person, word] });

    private IReadOnlyDictionary<string, OpinionGateKept> ReadAll(string chain)
    {
        var all = new Dictionary<string, OpinionGateKept>(StringComparer.OrdinalIgnoreCase);
        if (PathOf(chain) is not { } path || !File.Exists(path)) return all;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("repositories", out var repositories) || repositories.ValueKind != JsonValueKind.Object) return all;
            foreach (var repository in repositories.EnumerateObject())
            {
                if (repository.Value.ValueKind != JsonValueKind.Object) continue;
                all[repository.Name] = new OpinionGateKept
                {
                    Asks = [.. Kept.Items(repository.Value, "asks").Select(AskOf).OfType<OpinionAskKept>()],
                    Requests = [.. Kept.Items(repository.Value, "requests").Select(RequestOf).OfType<OpinionRequest>()],
                    Person = [.. Kept.Items(repository.Value, "person").Select(WordOf).OfType<OpinionPersonWord>()],
                };
            }
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // A record that does not read is nothing kept: the next ask and the next answer write it again.
        }

        return all;
    }

    private void Edit(string chain, string repository, Func<OpinionGateKept, OpinionGateKept> change)
    {
        if (PathOf(chain) is not { } path) throw new DriverException($"`{chain}` names no chain this machine keeps a gate for.");
        lock (Gate)
        {
            var all = ReadAll(chain).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            var key = all.Keys.FirstOrDefault(name => string.Equals(name, repository, StringComparison.OrdinalIgnoreCase)) ?? repository;
            all[key] = change(all.GetValueOrDefault(key) ?? OpinionGateKept.None);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, Json(chain, all));
        }
    }

    private static string Json(string chain, IReadOnlyDictionary<string, OpinionGateKept> all)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("chain", chain);
            writer.WriteStartObject("repositories");
            foreach (var (repository, kept) in all.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(repository);
                writer.WriteStartArray("asks");
                foreach (var ask in kept.Asks)
                {
                    writer.WriteStartObject();
                    writer.WriteString("at", OpinionViews.Moment(ask.At));
                    writer.WriteString("occasion", ask.Occasion);
                    writer.WriteString("working", ask.Working);
                    writer.WriteString("tip", ask.Tip);
                    writer.WriteString("by", ask.By);
                    if (ask.Opinion is not null) writer.WriteString("opinion", ask.Opinion);
                    if (ask.Code is not null) writer.WriteString("code", ask.Code);
                    if (ask.Until is { } until) writer.WriteString("until", OpinionViews.Moment(until));
                    if (ask.Reviewer is not null) writer.WriteString("reviewer", ask.Reviewer);
                    if (ask.Label is not null) writer.WriteString("label", ask.Label);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("requests");
                foreach (var request in kept.Requests)
                {
                    writer.WriteStartObject();
                    writer.WriteString("at", OpinionViews.Moment(request.At));
                    writer.WriteString("session", request.Session);
                    writer.WriteString("occasion", request.Occasion);
                    if (request.Reviewer is not null) writer.WriteString("reviewer", request.Reviewer);
                    if (request.SameAgent) writer.WriteBoolean("sameAgent", true);
                    if (request.Words is not null) writer.WriteString("words", request.Words);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("person");
                foreach (var word in kept.Person)
                {
                    writer.WriteStartObject();
                    writer.WriteString("said", word.Said);
                    writer.WriteString("tip", word.Tip);
                    writer.WriteString("at", OpinionViews.Moment(word.At));
                    if (word.Words is not null) writer.WriteString("words", word.Words);
                    if (word.Door is not null) writer.WriteString("door", word.Door);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    private static OpinionAskKept? AskOf(JsonElement element) =>
        Kept.Moment(element, "at") is { } at && Kept.Text(element, "occasion") is { } occasion && Kept.Text(element, "working") is { } working
        && Kept.Text(element, "tip") is { } tip
            ? new OpinionAskKept(at, occasion, working, tip)
            {
                Opinion = Kept.Text(element, "opinion"),
                Code = Kept.Text(element, "code"),
                Until = Kept.Moment(element, "until"),
                By = Kept.Text(element, "by") ?? OpinionAskers.Look,
                Reviewer = Kept.Text(element, "reviewer"),
                Label = Kept.Text(element, "label"),
            }
            : null;

    private static OpinionRequest? RequestOf(JsonElement element) =>
        Kept.Moment(element, "at") is { } at && Kept.Text(element, "session") is { } session && Kept.Text(element, "occasion") is { } occasion
            ? new OpinionRequest(at, session, occasion)
            {
                Reviewer = Kept.Text(element, "reviewer"),
                SameAgent = element.TryGetProperty("sameAgent", out var same) && same.ValueKind == JsonValueKind.True,
                Words = Kept.Text(element, "words"),
            }
            : null;

    private static OpinionPersonWord? WordOf(JsonElement element) =>
        Kept.Text(element, "said") is { } said && Kept.Text(element, "tip") is { } tip && Kept.Moment(element, "at") is { } at
            ? new OpinionPersonWord(said, tip, at) { Words = Kept.Text(element, "words"), Door = Kept.Text(element, "door") }
            : null;
}

/// <summary>Reading a kept file's fields without trusting its shape: a field of another type is absent.</summary>
internal static class Kept
{
    public static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static DateTimeOffset? Moment(JsonElement element, string name) =>
        Text(element, name) is { } when && DateTimeOffset.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;

    public static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];
}
