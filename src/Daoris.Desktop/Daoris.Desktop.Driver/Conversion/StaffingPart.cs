using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>One role the staffing part writes: its order as it runs now, and whether it ran <i>as before</i> (else it was off for want of its key).</summary>
public sealed record StaffingWrite(string Role, IReadOnlyList<ActorChoice> Order, bool WasBefore);

/// <summary>
/// The staffing part's plan for its review and its receipt: the roles it writes, the roles whose entry does not read (kept, never forced),
/// the keys it takes out of <c>driver.json</c> with their text, the found changes it clears, and the rows it was read from.
/// </summary>
public sealed record StaffingPartPlan(
    IReadOnlyList<StaffingWrite> Writes, IReadOnlyList<(string Role, string Why)> Unread, StaffingPaths Keys, IReadOnlyList<StaffingFound> Found)
{
    internal IReadOnlyList<StaffingRow> Rows { get; init; } = [];
}

/// <summary>A role's row as the receipt keeps it, before and after: its state, choices, why and first-agent key; none where it had no row.</summary>
internal sealed record StaffingKept(string Role, StaffingRow? Row);

/// <summary>What the staffing part keeps in its receipt entry: each written role's row before and after, and the four keys as the file held them.</summary>
internal sealed record StaffingBody(IReadOnlyList<StaffingKept> Before, IReadOnlyList<StaffingKept> Wrote, StaffingPaths Keys);

/// <summary>
/// The conversion's staffing part (ONEWAY1b; the oneway design §2 S2's records and S3): each of the five roles with an order of its own that
/// reads <i>as before</i> written as it runs, at account default, and Intake or Ask Daoris with no key written off; an entry that does not
/// read kept and said; Second opinion, Partner and Helper left to their caller's rule. Then <c>driver.json</c>'s four staffing keys taken out
/// and kept for Undo, and the found changes of the copy cleared. The receipt standing, the copy ends (ONEWAY1c-ii). Undo puts the rows and the
/// keys back, refused once a role or the keys moved since.
/// </summary>
public sealed class StaffingPart(RoleStaffing staffing) : IConversionPart
{
    public const string Part = "staffing";
    private const string Orders = "staffing:orders";
    private const string Keys = "staffing:keys";
    private const string Found = "staffing:found";
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);
    private static readonly StaffingPaths None = new(null, null, null, null);

    public string Name => Part;

    public Task<PartCensus> CensusAsync(CancellationToken ct) => Task.FromResult(Now() is { } plan ? CensusOf(plan) : new PartCensus(Part, 0, true));

    public Task<PartPlan> PlanAsync(ConversionChoices choices, CancellationToken ct)
    {
        // Before the staffing's import there is nothing in the store to convert: the start imports it, and the next plan reads it.
        if (Now() is not { } plan)
        {
            return Task.FromResult(new PartPlan(Part, System.Convert.ToHexStringLower(SHA256.HashData("staffing\nnot imported\n"u8)), true, new PartCensus(Part, 0, true),
                ["  the roles' staffing is not in this home's store yet: the next start imports it, and the next plan reads it."], []));
        }

        var census = CensusOf(plan);
        return Task.FromResult(new PartPlan(Part, Digest(plan), census.Ended, census, Lines(plan),
            plan.Found.Count == 0 ? [] : [new PartClear("changes found in driver.json's staffing copy, each an older build's or a hand's (Take it stays offered until Apply)", plan.Found.Count)])
        {
            Detail = plan,
        });
    }

    /// <summary>Whether each of the five roles holds a written order in <paramref name="home"/>'s store: the old keys' reading is reached no more.</summary>
    public static bool EveryOrderWritten(string home)
    {
        try
        {
            var rows = new RoleStaffing(home).Rows();
            return ActorChoices.Ordered.All(role => rows.Any(row => row.Role == role && row.State is "set" or "unreadable"));
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or DesktopStoreException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether a conversion not undone took <paramref name="home"/>'s staffing keys out, or may have (its operation uncertain): from then on
    /// nothing is copied onto the file, whatever the receipt's other parts came to, so a check never reads the missing keys as a change.
    /// </summary>
    public static bool KeysTakenOut(string home)
    {
        try
        {
            return new HomeReceipts(home).All().Any(each => !each.Undone
                && each.Of(Part)?.Of(Keys)?.Outcome is ConversionOutcomes.Done or ConversionOutcomes.Uncertain);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return false;
        }
    }

    private static PartCensus CensusOf(StaffingPartPlan plan) =>
        new(Part, plan.Writes.Count + (plan.Keys == None ? 0 : 1) + plan.Found.Count, plan.Writes.Count == 0 && plan.Keys == None && plan.Found.Count == 0);

    /// <summary>The staffing now: the store's rows and found changes, and the file's four keys; null before the import. Unread where either does not answer.</summary>
    private StaffingPartPlan? Now()
    {
        StaffingRead read;
        IReadOnlyList<StaffingRow> rows;
        IReadOnlyList<StaffingFound> found;
        try
        {
            if (staffing.Read() is not { } held) return null;
            read = held;
            rows = staffing.Rows();
            found = staffing.Found();
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or DesktopStoreException)
        {
            throw new ConversionUnread($"the store did not read ({error.Message.TrimEnd('.', ' ')})");
        }

        var config = StaffingProjection.Config(read);
        var writes = new List<StaffingWrite>();
        var unread = new List<(string Role, string Why)>();
        foreach (var role in ActorChoices.Ordered)
        {
            if (config.ActorErrors.TryGetValue(role, out var why)) unread.Add((role, why));
            else if (!config.Actors.ContainsKey(role))
            {
                var order = ActorChoices.Effective(role, config).Select(each => new ActorChoice(each.Agent)).ToList();
                writes.Add(new StaffingWrite(role, order, order.Count > 0));
            }
        }

        return new StaffingPartPlan(writes, unread, FileKeys(), found) { Rows = rows };
    }

    /// <summary>The file's four staffing keys as their text; none where the file is gone. Unread where it does not read, since it is never written then.</summary>
    private StaffingPaths FileKeys()
    {
        var file = staffing.FileOfHome;
        try
        {
            return File.Exists(file) ? StaffingFile.PathsOf(AtomicFile.ReadText(file)) : None;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or DecoderFallbackException or InvalidOperationException)
        {
            throw new ConversionUnread($"{Path.GetFileName(file)} does not read ({error.Message.TrimEnd('.', ' ')}), so its staffing keys are not taken out");
        }
    }

    private static string Digest(StaffingPartPlan plan)
    {
        var text = new StringBuilder("staffing\n");
        foreach (var row in plan.Rows) text.Append($"row {row.Role}|{row.State}|{row.Choices}|{row.Why}|{row.First}\n");
        text.Append($"keys {plan.Keys.Actors}|{plan.Keys.Adapter}|{plan.Keys.IntakeAdapter}|{plan.Keys.HelperAdapter}\n");
        foreach (var each in plan.Found) text.Append($"found {each.Id}\n");
        return System.Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>The plan in the terminal's words, which the review reads too.</summary>
    private static IReadOnlyList<string> Lines(StaffingPartPlan plan)
    {
        var lines = new List<string>();
        foreach (var write in plan.Writes)
        {
            var now = write.Order.Count == 0 ? "off" : string.Join(", ", write.Order.Select(each => each.Agent));
            lines.Add($"  {ActorWords.Name(write.Role)}: {(write.WasBefore ? $"{now} (as before)" : "off (no key)")} → {now}, written");
        }

        foreach (var (role, why) in plan.Unread)
        {
            lines.Add($"  {ActorWords.Name(role)}: does not read ({why}): kept as it is, never forced; set it with `daoris role set {role} …`");
        }

        if (plan.Writes.Count > 0)
        {
            lines.Add("  Second opinion, Partner and Helper stay by their caller's rule (another maker than the asker's): nothing is written for them.");
            lines.Add("  nothing lowers and nothing spends differently: each role runs the agent it ran, at its account default.");
        }

        if (plan.Keys != None)
        {
            lines.Add($"  driver.json's staffing keys ({string.Join(", ", Named(plan.Keys).Select(each => $"`{each}`"))}) taken out, kept in the receipt for Undo.");
        }

        foreach (var each in plan.Found)
        {
            var changes = string.Join("; ", each.Changes.Select(change => $"{ActorWords.Name(change.Role)} {ActorPress.Said(change.Was)} → {ActorPress.Said(change.Now)}"));
            lines.Add($"  found #{each.Id} ({each.At}): {changes}");
        }

        return lines;
    }

    private static IEnumerable<string> Named(StaffingPaths keys) =>
        new (string Name, string? Text)[] { ("actors", keys.Actors), ("adapter", keys.Adapter), ("intakeAdapter", keys.IntakeAdapter), ("helperAdapter", keys.HelperAdapter) }
            .Where(each => each.Text is not null).Select(each => each.Name);

    // ---- The receipt entry ------------------------------------------------------------------------------------------------------------------------------

    public PartReceipt Begin(PartPlan plan)
    {
        var planned = (StaffingPartPlan)plan.Detail!;
        var before = planned.Writes.Select(each => new StaffingKept(each.Role, planned.Rows.FirstOrDefault(row => row.Role == each.Role))).ToList();
        var wrote = planned.Writes.Select(each => new StaffingKept(each.Role, Written(each))).ToList();
        var operations = new List<ConversionOp>();
        if (wrote.Count > 0)
        {
            operations.Add(new(Orders, "staffing-orders", $"{string.Join(", ", planned.Writes.Select(each => ActorWords.Name(each.Role)))}: the orders they run, written", ConversionOutcomes.Pending));
        }

        if (planned.Keys != None) operations.Add(new(Keys, "staffing-keys", "driver.json's staffing keys taken out, kept here for Undo", ConversionOutcomes.Pending));
        if (planned.Found.Count > 0) operations.Add(new(Found, "clear", $"{planned.Found.Count} found change{(planned.Found.Count == 1 ? "" : "s")} of the copy cleared", ConversionOutcomes.Pending) { Clear = true });
        return new PartReceipt(Part, plan.Digest, ConversionOutcomes.Pending, operations) { Body = Node(new StaffingBody(before, wrote, planned.Keys)) }.Derived();
    }

    /// <summary>The row a role's written order is kept as: set, with its first-agent key where the role has one.</summary>
    private static StaffingRow Written(StaffingWrite write) =>
        new(write.Role, "set", RoleStaffing.Write(write.Order), null, write.Role is "build" or "intake" or "ask-daoris" ? write.Order.FirstOrDefault()?.Agent : null);

    public PartReceipt Settle(PartReceipt entry)
    {
        var body = Body(entry);
        var next = entry;
        if (entry.Of(Orders) is { Outcome: ConversionOutcomes.Uncertain })
        {
            next = next.With(Orders, op => op with { Outcome = RowsAre(body.Wrote) ? ConversionOutcomes.Done : ConversionOutcomes.Pending });
        }

        if (entry.Of(Keys) is { Outcome: ConversionOutcomes.Uncertain })
        {
            next = next.With(Keys, op => op with { Outcome = KeysNow() == None ? ConversionOutcomes.Done : ConversionOutcomes.Pending });
        }

        return next;
    }

    /// <summary>
    /// The orders, then the keys, under the staffing's lock, so no write of the staffing lands between them; the entry kept done inside it, so
    /// the copy has ended before any other write reads the file without its keys.
    /// </summary>
    public (PartReceipt Entry, string? Refused) Convert(PartReceipt entry, Action<PartReceipt> keep)
    {
        var body = Body(entry);
        using var held = staffing.Lock();
        if (entry.Of(Orders) is { Outcome: ConversionOutcomes.Pending })
        {
            entry = entry.With(Orders, op => op with { Outcome = ConversionOutcomes.Uncertain });
            keep(entry);
            try
            {
                staffing.Replace([.. body.Wrote.Select(each => (each.Role, each.Row))]);
                entry = entry.With(Orders, op => op with { Outcome = ConversionOutcomes.Done });
            }
            catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
            {
                return (entry.With(Orders, op => op with { Outcome = ConversionOutcomes.Failed, Note = error.Message }), null);
            }

            keep(entry);
        }

        if (entry.Of(Keys) is { Outcome: ConversionOutcomes.Pending })
        {
            entry = entry.With(Keys, op => op with { Outcome = ConversionOutcomes.Uncertain });
            keep(entry);
            var filed = StaffingFile.Project(staffing.FileOfHome, body.Keys, None);
            entry = filed.How is StaffingFiled.Written or StaffingFiled.Unchanged
                ? entry.With(Keys, op => op with { Outcome = ConversionOutcomes.Done })
                : entry.With(Keys, op => op with { Outcome = ConversionOutcomes.Failed, Note = filed.Said });
            keep(entry);
        }

        return (entry, null);
    }

    public PartReceipt Clear(PartReceipt entry, Action<PartReceipt> keep)
    {
        try
        {
            staffing.ClearFound();
            return entry.With(Found, op => op with { Outcome = ConversionOutcomes.Done });
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return entry.With(Found, op => op with { Outcome = ConversionOutcomes.Failed, Note = error.Message });
        }
    }

    public PartUndo UndoPlan(PartReceipt entry)
    {
        var body = Body(entry);
        if (Moved(entry, body) is { } moved) return new($"moved: {moved}", []);
        var lines = new List<string>();
        foreach (var each in body.Before)
        {
            lines.Add($"{ActorWords.Name(each.Role)}: back to {(each.Row is { State: "before" } ? "as before" : each.Row is null ? "no stored order" : "its stored order")}");
        }

        if (entry.Of(Keys) is { Outcome: ConversionOutcomes.Done }) lines.Add($"driver.json's staffing keys come back ({string.Join(", ", Named(body.Keys).Select(each => $"`{each}`"))})");
        if (entry.Of(Found) is not null) lines.Add("the found changes it cleared stay cleared.");
        return new(null, lines) { Said = "the roles' orders and driver.json's staffing keys are as they were" };
    }

    /// <summary>The keys put back first, against a file that shows none of them, then the rows; a move since is refused with nothing written.</summary>
    public void Undo(PartReceipt entry)
    {
        var body = Body(entry);
        using var held = staffing.Lock();
        if (Moved(entry, body) is { } moved) throw new DriverException($"moved: {moved}");
        if (entry.Of(Keys) is { Outcome: ConversionOutcomes.Done })
        {
            var filed = StaffingFile.Project(staffing.FileOfHome, None, body.Keys);
            if (filed.How is not (StaffingFiled.Written or StaffingFiled.Unchanged)) throw new DriverException($"moved: {filed.Said}");
        }

        if (entry.Of(Orders) is { Outcome: ConversionOutcomes.Done }) staffing.Replace([.. body.Before.Select(each => (each.Role, each.Row))]);
    }

    /// <summary>Why the conversion's writes no longer stand: a role it wrote set since, or the keys written back to the file; null where they stand.</summary>
    private string? Moved(PartReceipt entry, StaffingBody body)
    {
        if (entry.Of(Orders) is { Outcome: ConversionOutcomes.Done })
        {
            var rows = staffing.Rows();
            if (body.Wrote.FirstOrDefault(each => rows.FirstOrDefault(row => row.Role == each.Role) != each.Row) is { } edited)
            {
                return $"{ActorWords.Name(edited.Role)}'s order was set since the conversion wrote it, so Undo would take that away; it is not offered.";
            }
        }

        if (entry.Of(Keys) is { Outcome: ConversionOutcomes.Done } && KeysNow() != None)
        {
            return "driver.json holds staffing keys again since the conversion took them out, so Undo would write over them; it is not offered.";
        }

        return null;
    }

    private bool RowsAre(IReadOnlyList<StaffingKept> kept)
    {
        var rows = staffing.Rows();
        return kept.All(each => rows.FirstOrDefault(row => row.Role == each.Role) == each.Row);
    }

    private StaffingPaths KeysNow()
    {
        try
        {
            return FileKeys();
        }
        catch (ConversionUnread)
        {
            return new StaffingPaths("unread", null, null, null);
        }
    }

    private static StaffingBody Body(PartReceipt entry) => entry.Body?.Deserialize<StaffingBody>(Format)
        ?? throw new DriverException($"the staffing part of the conversion `{entry.Digest[..Math.Min(12, entry.Digest.Length)]}` keeps no plan: it is read by hand.");

    private static JsonObject? Node(StaffingBody body) => JsonSerializer.SerializeToNode(body, Format) as JsonObject;
}
