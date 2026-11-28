using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One item waiting in one of the machine's queues, as the reader of the waiting items gives it: which queue, which item, the sort
/// inputs of the one order (its class as ordered, its stage, its first arrival and its queue's own tie), where its class comes from,
/// and why it cannot go now. A row never says its place: the reader that shows it composes the place (<see cref="QueueRows.Placed"/>,
/// and the page's twin, <c>queuePlaces.ts</c>), so a row read once gives one place in the window and the terminal.
/// </summary>
/// <param name="Queue"><see cref="QueueRows.Starts"/> or <see cref="QueueRows.Checks"/>.</param>
/// <param name="Item">The quest a start is of, or the run a check is of, with no <c>#</c>: the id a class is set on.</param>
/// <param name="Class">The class it is ordered by at the look, aged where its wait passed the bound (<see cref="QueueOrder.Age"/>).</param>
/// <param name="Arrived">When it began to wait; null where that is not known, which sorts after every known one.</param>
/// <param name="Order">Its place in its queue's own order where the comparator leaves it equal: the plan's walk, or the slots' waiters.</param>
public sealed record QueueRow(string Queue, string Item, string Class, QueueStage Stage, DateTimeOffset? Arrived, int Order)
{
    /// <summary>The class it is promoted from where its wait passed that class's bound; null otherwise.</summary>
    public string? AgedFrom { get; init; }

    /// <summary>Where its class comes from: <see cref="PriorityBook.Quest"/>, <see cref="PriorityBook.Ask"/> or <see cref="PriorityReading.Unset"/>.</summary>
    public string Source { get; init; } = PriorityReading.Unset;

    /// <summary>Who set its class, as a wait line names them; null where none is set.</summary>
    public string? By { get; init; }

    public string? Why { get; init; }

    /// <summary>The ask whose class it reads where its class comes from its ask.</summary>
    public string? AskId { get; init; }

    public string Repository { get; init; } = "";

    /// <summary>
    /// Why it cannot go now, as a code with its facts, so the page words it: a start's verdict and its sitting code
    /// (<see cref="SittingFact"/>), a check's slot wait (<see cref="CheckSlotWaits"/>).
    /// </summary>
    public string Code { get; init; } = "";

    /// <summary>A start's verdict (<see cref="StartVerdict"/>), whose sentence its code is; null for a check.</summary>
    public string? Verdict { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The same reason in English, the terminal's words.</summary>
    public string Reason { get; init; } = "";

    /// <summary>The row's place in the one order, an arrival not known sorting last.</summary>
    public QueuePlace Place => new(Class, Stage, Arrived ?? DateTimeOffset.MaxValue);
}

/// <summary>A row with the place composed for it: its place in its queue, how many wait there, and the rows ahead of it, first first.</summary>
public sealed record QueueSpot(QueueRow Row, int Position, int Of, IReadOnlyList<QueueRow> Ahead);

/// <summary>
/// The one reader of the machine's waiting items, which <c>daoris queue</c> and the window's queues view (<c>QUEUES</c>) both read:
/// the starts and resumes the last plan left waiting (<see cref="StartVerdict.AtCapacity"/>, and <see cref="StartVerdict.RepositoryBusy"/>
/// behind another's start or work) and the sent checks waiting for a slot (<see cref="VerifySlot.Look"/>). Each row carries the sort
/// inputs; its place is composed by the comparator (<see cref="QueueOrder"/>), here for the terminal and by its twin on the page, which
/// reads the shared table <c>fixtures/queue-places.json</c> as <c>QueueRowsTwinTests</c> does.
/// </summary>
public static class QueueRows
{
    /// <summary>The planner's starts and resumes waiting for a place under the cap or in their repository.</summary>
    public const string Starts = "starts";

    /// <summary>The sent checks waiting for one of the machine's check slots.</summary>
    public const string Checks = "checks";

    /// <summary>The queues in the order they are shown.</summary>
    public static IReadOnlyList<string> Queues { get; } = [Starts, Checks];

    /// <summary>
    /// A repository-busy start that is not waiting for a place: its quest is being worked on or opening, its landing is owed, or it is
    /// held, which stands it aside with no place and no turn.
    /// </summary>
    private static readonly string[] Aside = [SittingFact.Serving, SittingFact.Opening, SittingFact.LandingOwed, SittingFact.HoldWaiting];

    /// <summary>The waiting items of a plan and the check slots as they stand: the starts in the walk's order, then the checks in the slots'.</summary>
    public static IReadOnlyList<QueueRow> Read(IReadOnlyList<Consideration> plan, VerifyLook slots)
    {
        var rows = new List<QueueRow>();
        var order = 0;
        foreach (var each in plan)
        {
            if (Waits(each) && each.Place is { } place) rows.Add(Start(each, place, order++));
        }

        rows.AddRange(slots.Waiters.Where(waiter => !waiter.Held).Select((waiter, at) => Check(waiter, at)));
        return rows;
    }

    private static bool Waits(Consideration each) =>
        each.Verdict == StartVerdict.AtCapacity
        || (each.Verdict == StartVerdict.RepositoryBusy && each.Said is { Code: { } code } && !Aside.Contains(code));

    private static QueueRow Start(Consideration each, QueuePlace place, int order)
    {
        var reading = each.Priority ?? PriorityReading.None;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (each.Said is { } said)
        {
            void Put(string name, object? value)
            {
                if (value is not null) values[name] = Convert.ToString(value, CultureInfo.InvariantCulture)!;
            }

            Put("cap", said.Cap);
            Put("session", said.Session);
            Put("quest", said.Quest);
            Put("repository", said.Repository);
            if (said.Paths is { Count: > 0 } paths) values["paths"] = string.Join("\n", paths);
        }

        return new QueueRow(Starts, each.Quest.Id, place.Class, place.Stage, place.Arrived == DateTimeOffset.MaxValue ? null : place.Arrived, order)
        {
            AgedFrom = each.AgedFrom, Repository = each.Quest.To, Code = each.Said?.Code ?? "", Verdict = each.Verdict.ToString(), Values = values,
            Reason = each.Reason,
        }.From(reading);
    }

    private static QueueRow Check(VerifyWaiter waiter, int order)
    {
        var (code, values) = CheckSlotWaits.Reason(waiter);
        return new QueueRow(Checks, waiter.Run, waiter.Class, QueueStage.FirstChecks, waiter.FirstArrival, order)
        {
            Source = waiter.Source, By = waiter.By, Why = waiter.Why, Repository = waiter.Repository, Code = code, Values = values,
            Reason = CheckSlotWaits.Why(code, values),
        };
    }

    private static QueueRow From(this QueueRow row, PriorityReading reading) =>
        row with
        {
            Source = reading.Source, By = reading.Event?.By, Why = reading.Event?.Why,
            AskId = reading.Source == PriorityBook.Ask ? reading.Event?.Id : null,
        };

    /// <summary>
    /// Each row's place: in its queue, ordered by the one comparator (class, stage, first arrival), then its queue's own order, the
    /// queues in <see cref="Queues"/>' order. The page's twin composes the same from the same rows.
    /// </summary>
    public static IReadOnlyList<QueueSpot> Placed(IEnumerable<QueueRow> rows)
    {
        var spots = new List<QueueSpot>();
        foreach (var queue in rows.GroupBy(row => row.Queue, StringComparer.Ordinal).OrderBy(group => Rank(group.Key)))
        {
            var ordered = QueueOrder.Of(queue, row => row.Place).ThenBy(row => row.Order).ToList();
            spots.AddRange(ordered.Select((row, at) => new QueueSpot(row, at + 1, ordered.Count, ordered[..at])));
        }

        return spots;
    }

    private static int Rank(string queue) => Queues.ToList().IndexOf(queue) is var at and >= 0 ? at : Queues.Count;

    /// <summary>The stage as the rows and the page spell it: <c>landing</c>, <c>back-at-checks</c>, <c>resume</c>, <c>first-checks</c>, <c>new-start</c>.</summary>
    public static string Word(QueueStage stage) => stage switch
    {
        QueueStage.Landing => "landing",
        QueueStage.BackAtChecks => "back-at-checks",
        QueueStage.Resume => "resume",
        QueueStage.FirstChecks => "first-checks",
        _ => "new-start",
    };

    /// <summary>The stage a word spells, or null where it spells none.</summary>
    public static QueueStage? Stage(string? word) =>
        Enum.GetValues<QueueStage>().Select(stage => (QueueStage?)stage).FirstOrDefault(stage => Word(stage!.Value) == word);

    /// <summary>A row as the route answers it, camel-cased, a fact none holds left out.</summary>
    public static JsonObject Json(QueueRow row)
    {
        var wire = new JsonObject
        {
            ["queue"] = row.Queue, ["item"] = row.Item, ["class"] = row.Class, ["stage"] = Word(row.Stage),
            ["arrived"] = row.Arrived is { } arrived ? arrived.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : null,
            ["order"] = row.Order, ["source"] = row.Source, ["repository"] = row.Repository, ["code"] = row.Code,
        };
        if (row.AgedFrom is { } from) wire["agedFrom"] = from;
        if (row.By is { } by) wire["by"] = by;
        if (row.Why is { } why) wire["why"] = why;
        if (row.AskId is { } ask) wire["ask"] = ask;
        if (row.Verdict is { } verdict) wire["verdict"] = verdict;
        wire["values"] = new JsonObject([.. row.Values.Select(each => KeyValuePair.Create(each.Key, (JsonNode?)each.Value))]);
        wire["reason"] = row.Reason;
        return wire;
    }

    /// <summary>A row as <see cref="Json"/> wrote it, or null where it does not read as one.</summary>
    public static QueueRow? Parse(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object) return null;
        string? Text(string name) => raw.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (Text("queue") is not { } queue || Text("item") is not { } item || Stage(Text("stage")) is not { } stage) return null;
        DateTimeOffset? arrived = raw.TryGetProperty("arrived", out var at) && at.ValueKind == JsonValueKind.String && at.TryGetDateTimeOffset(out var when) ? when : null;
        var order = raw.TryGetProperty("order", out var number) && number.TryGetInt32(out var tie) ? tie : 0;
        var values = raw.TryGetProperty("values", out var facts) && facts.ValueKind == JsonValueKind.Object
            ? facts.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.String).ToDictionary(each => each.Name, each => each.Value.GetString()!, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        return new QueueRow(queue, item, PriorityClasses.Parse(Text("class")) ?? PriorityClasses.Normal, stage, arrived, order)
        {
            AgedFrom = Text("agedFrom"), Source = Text("source") ?? PriorityReading.Unset, By = Text("by"), Why = Text("why"), AskId = Text("ask"),
            Repository = Text("repository") ?? "", Code = Text("code") ?? "", Verdict = Text("verdict"), Values = values, Reason = Text("reason") ?? "",
        };
    }

    /// <summary>
    /// The terminal's words for every queue: a line per waiting item, its queue, its place, its class with where it comes from, its stage,
    /// why it waits and each item ahead with its class and stage; a machine with nothing waiting says so.
    /// </summary>
    public static string Said(IReadOnlyList<QueueRow> rows)
    {
        var spots = Placed(rows);
        if (spots.Count == 0) return "Nothing waits in the machine's queues.\n";
        var said = new System.Text.StringBuilder();
        foreach (var spot in spots) said.Append(Said(spot)).Append('\n');
        return said.ToString();
    }

    /// <summary>One item's line: <c>starts 2 of 3: quest #q, high (set by the person: the fix), a new start; waits: …; ahead: quest #a, urgent (…), a resume.</c></summary>
    public static string Said(QueueSpot spot)
    {
        var row = spot.Row;
        var ahead = spot.Ahead.Count == 0 ? "" : "; ahead: " + string.Join("; ", spot.Ahead.Select(each => $"{Name(each)}, {Class(each)}, {Staged(each.Stage)}"));
        var why = row.Reason.Trim().TrimEnd('.');
        return $"{row.Queue} {spot.Position} of {spot.Of}: {Name(row)}, {Class(row)}, {Staged(row.Stage)}{(why.Length > 0 ? $"; waits: {why}" : "")}{ahead}.";
    }

    private static string Name(QueueRow row) => row.Queue == Checks ? $"the checks of run #{row.Item}" : $"quest #{row.Item}";

    /// <summary>A class as the terminal says it, with where it comes from and whether it is aged: <c>high (aged from normal; from its ask #a, set by the person: why)</c>.</summary>
    public static string Class(QueueRow row)
    {
        var why = new List<string>();
        if (row.AgedFrom is { } from) why.Add($"aged from {from}");
        var asked = row.AskId is { } id ? $"from its ask #{id}, " : "from its ask, ";
        if (row.By is { } by) why.Add((row.Source == PriorityBook.Ask ? asked : "") + $"set by {by}: {row.Why}");
        return why.Count == 0 ? row.Class : $"{row.Class} ({string.Join("; ", why)})";
    }

    private static string Staged(QueueStage stage) => stage switch
    {
        QueueStage.Landing => "a landing",
        QueueStage.BackAtChecks => "back at its checks",
        QueueStage.Resume => "a resume",
        QueueStage.FirstChecks => "first checks",
        _ => "a new start",
    };
}
