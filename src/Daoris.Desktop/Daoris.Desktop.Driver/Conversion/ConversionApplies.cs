using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>What an apply reads and writes: the workflow part's world and its one edit of <c>driver.json</c>, and what the door composes beside it.</summary>
/// <param name="World">The world as it stands now: the file, the registry, the workflows and the bound runs with their units' state.</param>
/// <param name="OpenCurrent">
/// Inside the edit, under the file's lock: the places (<see cref="WorkflowLegacy.Held"/>'s keys) with a Current unit open now, read from the run
/// files as they are then, so a start that bound after the plan is seen.
/// </param>
public sealed record ConvertDoors(
    string Home, Func<CancellationToken, Task<ConvertWorld>> World, Action<Func<DriverConfig, DriverConfig>> Change, Func<DriverConfig> Config,
    Func<IReadOnlySet<string>> OpenCurrent)
{
    /// <summary>The parts beside the workflow part, each an area's; the door orders them (<see cref="ConversionParts.Order"/>).</summary>
    public IReadOnlyList<IConversionPart> Others { get; init; } = [];

    /// <summary>The build a receipt is stamped with (<see cref="InstallRollback.This"/>), the floor's source.</summary>
    public string? Build { get; init; } = InstallRollback.This;

    /// <summary>The first build that deleted a read the floor ended (the register's <c>FloorNeeded</c>): from it on Undo is refused.</summary>
    public string? FloorNeeded { get; init; }

    /// <summary>The plans this Daoris answered, by digest: what a press of a plan with a part that no longer answers is compared against.</summary>
    public AnsweredPlans? Answered { get; init; }
}

/// <summary>What an apply or an undo came to: whether it is done, a code where it was refused, the sentence, and each operation as the receipt keeps it.</summary>
public sealed record ConvertResult(bool Done, string? Refused, string Message, string Digest, IReadOnlyList<ConversionOp> Operations, HomePlan? Plan = null)
{
    /// <summary>Each part's entry as the receipt keeps it, with its outcome and sentence.</summary>
    public IReadOnlyList<PartReceipt> Parts { get; init; } = [];
}

/// <summary>The codes a conversion's press is refused by, each with nothing written.</summary>
public static class ConversionRefusals
{
    /// <summary>The plan the person read no longer holds; the answer carries the plan now.</summary>
    public const string Moved = "moved";

    public const string Digest = "digest";
    public const string Receipt = "receipt";
    public const string Choices = "choices";

    /// <summary>The plan writes nothing: what is left is kept, held or counted.</summary>
    public const string Empty = "empty";

    /// <summary>No conversion stands to undo, or a later one does.</summary>
    public const string NoneStands = "none-stands";

    /// <summary><i>Leave it</i> named a part the receipt does not hold failed.</summary>
    public const string Part = "part";
}

/// <summary>
/// The home's conversion plan: each part that reads, with its own plan and digest; each part left out because its source did not answer; and the
/// digest the press names, over each part that writes something (a plan of one part is that part's digest, so a receipt written before parts is one).
/// </summary>
public sealed record HomePlan(string Digest, ConversionChoices Choices, IReadOnlyList<PartPlan> Parts, IReadOnlyList<PartUnread> Unread)
{
    /// <summary>Whether no part that reads writes anything.</summary>
    public bool Empty => Parts.All(each => each.Empty);

    /// <summary>Whether every part read and its census has ended.</summary>
    public bool Ended => Unread.Count == 0 && Parts.All(each => each.Census.Ended);

    /// <summary>The old records every part that read holds.</summary>
    public int Count => Parts.Sum(each => each.Census.Count);

    public PartPlan? Of(string part) => Parts.FirstOrDefault(each => each.Part == part);

    /// <summary>The workflow part's own plan, where it read.</summary>
    public ConversionPlan? Workflows => Of(ConversionParts.Workflows)?.Detail is WorkflowPartPlan planned ? planned.Plan : null;

    /// <summary>Each part that writes something, by name, with its digest: what a press of this plan names.</summary>
    public IReadOnlyDictionary<string, string> Pressed => Parts.Where(each => !each.Empty).ToDictionary(each => each.Part, each => each.Digest, StringComparer.Ordinal);
}

/// <summary>
/// The plans this Daoris answered, kept in memory and never written: a part that does not answer at Apply cannot be planned again, so its digest is
/// read from the plan the person pressed. A restart forgets them, and such a press is refused <c>moved</c>.
/// </summary>
public sealed class AnsweredPlans
{
    private const int Kept = 16;
    private readonly Lock _gate = new();
    private readonly LinkedList<(string Digest, IReadOnlyDictionary<string, string> Parts)> _plans = new();

    public void Keep(HomePlan plan)
    {
        lock (_gate)
        {
            if (_plans.Any(each => each.Digest == plan.Digest)) return;
            _plans.AddLast((plan.Digest, plan.Pressed));
            while (_plans.Count > Kept) _plans.RemoveFirst();
        }
    }

    /// <summary>The parts' digests of the plan <paramref name="digest"/> names, or null where this Daoris did not answer it.</summary>
    public IReadOnlyDictionary<string, string>? Of(string digest)
    {
        lock (_gate) return _plans.FirstOrDefault(each => each.Digest == digest).Parts;
    }
}

/// <summary>
/// The home's one conversion door (ONEWAY1ae; the oneway design §1.1, the cleanup design §4.4-§4.5): every part planned together under one digest;
/// the person's press planned again and refused <c>moved</c> where a part that reads differs, a part that does not answer written <c>failed</c> into
/// the receipt; a receipt written before the first effect; each part's converting operations in apply order, then every part's clears once none is
/// unsettled; a failed part retried inside its receipt; <i>Leave it</i>; and Undo of the latest receipt once it is settled.
/// </summary>
public static class ConversionApplies
{
    /// <summary>Every part's plan now, each part that does not answer left out and said; the plan is kept in <see cref="ConvertDoors.Answered"/>.</summary>
    public static async Task<HomePlan> PlanAsync(ConvertDoors doors, ConversionChoices choices, CancellationToken ct = default)
    {
        var plans = new List<PartPlan>();
        var unread = new List<PartUnread>();
        foreach (var part in ConversionParts.Of(doors))
        {
            try
            {
                plans.Add(await part.PlanAsync(choices, ct).ConfigureAwait(false));
            }
            catch (ConversionUnread not)
            {
                unread.Add(new(part.Name, not.Message));
            }
        }

        var plan = new HomePlan(DigestOf(plans), choices, plans, unread);
        doors.Answered?.Keep(plan);
        return plan;
    }

    /// <summary>Every part's census now, and the parts that did not answer: what <i>What needs you</i>'s row counts.</summary>
    public static async Task<(IReadOnlyList<PartCensus> Parts, IReadOnlyList<PartUnread> Unread)> CensusAsync(ConvertDoors doors, CancellationToken ct = default)
    {
        var parts = new List<PartCensus>();
        var unread = new List<PartUnread>();
        foreach (var part in ConversionParts.Of(doors))
        {
            try
            {
                parts.Add(await part.CensusAsync(ct).ConfigureAwait(false));
            }
            catch (ConversionUnread not)
            {
                unread.Add(new(part.Name, not.Message));
            }
        }

        return (parts, unread);
    }

    /// <summary>The digest a press names: over each part that writes something, else over each part that read; one part's is its own.</summary>
    private static string DigestOf(IReadOnlyList<PartPlan> plans)
    {
        var named = plans.Where(each => !each.Empty).ToList() is { Count: > 0 } writing ? writing : [.. plans];
        if (named.Count == 1) return named[0].Digest;
        var text = "conversion\n" + string.Concat(named.OrderBy(each => ConversionParts.Rank(each.Part)).Select(each => $"{each.Part}:{each.Digest}\n"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>The conversion plan <paramref name="digest"/> names, applied. A digest that holds a receipt goes on from it: a failed part is retried.</summary>
    public static async Task<ConvertResult> ApplyAsync(ConvertDoors doors, ConversionChoices choices, string digest, CancellationToken ct = default)
    {
        if (!ConversionOutcomes.IsDigest(digest)) return Refuse(digest, ConversionRefusals.Digest, "name the plan's digest, as its plan says it: 64 lower-case hex.");
        if (choices.Problem is { } problem) return Refuse(digest, ConversionRefusals.Choices, problem);
        var receipts = new HomeReceipts(doors.Home);
        HomeReceipt? receipt;
        try
        {
            receipt = receipts.Read(digest);
        }
        catch (DriverException unread)
        {
            return Refuse(digest, ConversionRefusals.Receipt, unread.Message);
        }

        var parts = ConversionParts.Of(doors);
        if (receipt is null || receipt.Undone)
        {
            var plan = await PlanAsync(doors, choices, ct).ConfigureAwait(false);
            var pressed = plan.Unread.Count == 0 ? null : doors.Answered?.Of(digest);
            if (MovedSince(plan, digest, pressed) is { } moved) return new(false, ConversionRefusals.Moved, moved, digest, [], plan);
            var failing = plan.Unread.Where(each => pressed?.ContainsKey(each.Part) == true).ToList();
            if (plan.Empty && failing.Count == 0) return new(false, ConversionRefusals.Empty, "the plan writes nothing: what it lists is kept as it is, so there is nothing to apply.", digest, [], plan);
            var entries = new List<PartReceipt>();
            foreach (var part in parts)
            {
                if (plan.Of(part.Name) is { Empty: false } own) entries.Add(part.Begin(own));
                else if (failing.FirstOrDefault(each => each.Part == part.Name) is { } unread)
                {
                    entries.Add(new PartReceipt(part.Name, pressed![part.Name], ConversionOutcomes.Failed, []) { Said = $"not read: {unread.Said}" });
                }
            }

            receipt = new HomeReceipt(digest, DateTimeOffset.UtcNow, choices, entries) { Build = doors.Build };
            receipts.Write(receipt);
        }
        else
        {
            receipt = await RetryAsync(parts, receipts, receipt, ct).ConfigureAwait(false);
        }

        return Continue(parts, receipts, receipt);
    }

    /// <summary>Why the plan read now is not the one pressed, or null: the whole digest where every part read, else each reading part against its pressed digest.</summary>
    private static string? MovedSince(HomePlan plan, string digest, IReadOnlyDictionary<string, string>? pressed)
    {
        var again = $"so nothing was written. Plan it again (now `{plan.Digest[..12]}`).";
        if (plan.Unread.Count == 0)
        {
            return plan.Digest == digest ? null : $"the plan you read no longer holds: a rule, a workflow or a run moved since, {again}";
        }

        if (pressed is null)
        {
            return $"`{plan.Unread[0].Part}` was not read ({plan.Unread[0].Said}), and the plan you read is not one this Daoris answered since it started, so it cannot be compared: {again}";
        }

        foreach (var each in plan.Parts)
        {
            var was = pressed.GetValueOrDefault(each.Part);
            if (each.Empty ? was is not null : was != each.Digest) return $"the plan you read no longer holds: `{each.Part}` moved since, {again}";
        }

        return pressed.Keys.FirstOrDefault(name => plan.Of(name) is null && plan.Unread.All(each => each.Part != name)) is { } gone
            ? $"the plan you read no longer holds: `{gone}` is no part here now, {again}"
            : null;
    }

    /// <summary>
    /// A receipt pressed again: each part settled by its postconditions; a failed part with operations runs them again; a failed part with none
    /// plans itself alone, its operations written where its digest is the one pressed and closed <c>moved</c> where it is not.
    /// </summary>
    private static async Task<HomeReceipt> RetryAsync(IReadOnlyList<IConversionPart> parts, HomeReceipts receipts, HomeReceipt receipt, CancellationToken ct)
    {
        foreach (var entry in receipt.Parts)
        {
            if (parts.FirstOrDefault(each => each.Name == entry.Part) is not { } part || entry.Outcome is ConversionOutcomes.Moved or ConversionOutcomes.Left) continue;
            var next = entry.Operations.Count == 0 ? entry : part.Settle(entry);
            if (next.Derived().Outcome == ConversionOutcomes.Failed && next.Operations.Count > 0)
            {
                next = next with { Operations = [.. next.Operations.Select(each => each.Outcome == ConversionOutcomes.Failed ? each with { Outcome = ConversionOutcomes.Pending } : each)], Said = null };
            }
            else if (next.Outcome == ConversionOutcomes.Failed && next.Operations.Count == 0)
            {
                try
                {
                    var alone = await part.PlanAsync(receipt.Choices, ct).ConfigureAwait(false);
                    next = alone.Digest == entry.Digest && !alone.Empty
                        ? part.Begin(alone)
                        : entry with { Outcome = ConversionOutcomes.Moved, Said = "it moved since you read it, so nothing of it was written: the next plan offers it again." };
                }
                catch (ConversionUnread not)
                {
                    next = entry with { Said = $"not read: {not.Message}" };
                }
            }

            receipt = receipt.With(next.Derived());
        }

        receipts.Write(receipt);
        return receipt;
    }

    /// <summary>Every part's converting operations in apply order, then every part's clears where no converting operation is unsettled.</summary>
    private static ConvertResult Continue(IReadOnlyList<IConversionPart> parts, HomeReceipts receipts, HomeReceipt receipt)
    {
        void Keep(PartReceipt entry)
        {
            receipt = receipt.With(entry.Derived());
            receipts.Write(receipt);
        }

        string? refused = null;
        foreach (var part in parts)
        {
            if (receipt.Of(part.Name) is not { } entry || !entry.Operations.Any(each => !each.Clear && each.Outcome == ConversionOutcomes.Pending)) continue;
            var (after, why) = part.Convert(entry, Keep);
            refused ??= why;
            Keep(after);
        }

        if (!receipt.Parts.Any(each => each.Converting))
        {
            foreach (var part in parts)
            {
                if (receipt.Of(part.Name) is not { } entry || !entry.Operations.Any(each => each.Clear && each.Outcome == ConversionOutcomes.Pending)) continue;
                Keep(part.Clear(entry, Keep));
            }
        }

        return Answer(receipt, refused);
    }

    private static ConvertResult Answer(HomeReceipt receipt, string? refused)
    {
        var done = receipt.Done;
        var notes = string.Concat(receipt.Parts.Where(each => each.Outcome == ConversionOutcomes.Done && each.Said is { Length: > 0 }).Select(each => " " + each.Said));
        var message = done
            ? $"the conversion is applied.{notes}"
            : "the conversion is not done: " + string.Join("; ", receipt.Parts.Where(each => each.Outcome != ConversionOutcomes.Done).SelectMany(Unsettled));
        return new(done, refused, message, receipt.Digest, [.. receipt.Parts.SelectMany(each => each.Operations)]) { Parts = receipt.Parts };
    }

    private static IEnumerable<string> Unsettled(PartReceipt entry) =>
        entry.Operations.Count == 0 || entry.Outcome is ConversionOutcomes.Moved or ConversionOutcomes.Left
            ? [$"`{entry.Part}` ({entry.Outcome}{(entry.Said is { } said ? $": {said}" : "")})"]
            : entry.Operations.Where(each => each.Outcome != ConversionOutcomes.Done).Select(Said);

    private static string Said(ConversionOp operation) => $"{operation.Said} ({operation.Outcome}{(operation.Note is { } note ? $": {note}" : "")})";

    private static ConvertResult Refuse(string digest, string code, string message) => new(false, code, message, digest, []);

    /// <summary>
    /// The person's <i>Leave it</i> on a failed part of the receipt <paramref name="digest"/> names: closed <c>left</c>, the next plan offering it
    /// again; the receipt's held clears then run where nothing else is unsettled.
    /// </summary>
    public static ConvertResult Leave(ConvertDoors doors, string digest, string part)
    {
        if (!ConversionOutcomes.IsDigest(digest)) return Refuse(digest, ConversionRefusals.Digest, "name the conversion's digest, as its receipt says it: 64 lower-case hex.");
        var receipts = new HomeReceipts(doors.Home);
        HomeReceipt? receipt;
        try
        {
            receipt = receipts.Read(digest);
        }
        catch (DriverException unread)
        {
            return Refuse(digest, ConversionRefusals.Receipt, unread.Message);
        }

        if (receipt is null || receipt.Undone) return Refuse(digest, ConversionRefusals.NoneStands, $"no conversion `{digest[..12]}` stands here, so nothing is left.");
        if (receipt.Of(part) is not { Outcome: ConversionOutcomes.Failed } entry)
        {
            return Refuse(digest, ConversionRefusals.Part, receipt.Of(part) is { } held
                ? $"`{part}` is {held.Outcome} in that conversion; only a failed part is left."
                : $"`{part}` is no part of that conversion.");
        }

        receipt = receipt.With(entry with { Outcome = ConversionOutcomes.Left, Said = "left as it is: the next plan offers it again." });
        receipts.Write(receipt);
        return Continue(ConversionParts.Of(doors), receipts, receipt);
    }

    /// <summary>A restart's read of every receipt not undone: each uncertain operation settled by its postcondition, nothing written but the receipt.</summary>
    public static IReadOnlyList<HomeReceipt> SettleAll(ConvertDoors doors)
    {
        var receipts = new HomeReceipts(doors.Home);
        var parts = ConversionParts.Of(doors);
        var settled = new List<HomeReceipt>();
        foreach (var receipt in receipts.All())
        {
            var next = receipt;
            if (!receipt.Undone)
            {
                foreach (var entry in receipt.Parts.Where(each => each.Operations.Any(op => op.Outcome == ConversionOutcomes.Uncertain)))
                {
                    if (parts.FirstOrDefault(each => each.Name == entry.Part) is { } part) next = next.With(part.Settle(entry).Derived());
                }

                if (!ReferenceEquals(next, receipt)) receipts.Write(next);
            }

            settled.Add(next);
        }

        return settled;
    }

    // ---- Undo (the oneway design §1.1, the cleanup design §4.5) ----------------------------------------------------------------------------------------

    /// <summary>
    /// What <i>Undo</i> would put back, or why it is not offered: the home's latest receipt, settled, not undone and with a part done; this build
    /// deleting no read the floor ended; and each done part's writes still reading as written.
    /// </summary>
    public static (HomeReceipt? Receipt, string? Refusal, string? Digest, IReadOnlyList<string> Lines) UndoPlan(ConvertDoors doors)
    {
        var latest = new HomeReceipts(doors.Home).Latest();
        if (latest is null) return (null, "no conversion has been applied here, so there is nothing to undo.", null, []);
        if (latest.Undone) return (null, "the latest conversion was undone already.", null, []);
        if (!latest.Settled) return (null, "the latest conversion is not settled: go on with it, or leave its failed part, first.", null, []);
        if (latest.Parts.All(each => each.Outcome != ConversionOutcomes.Done)) return (null, "the latest conversion wrote nothing to undo.", null, []);
        if (doors.FloorNeeded is { } floor)
        {
            return (latest, $"this build ({floor} on) deleted a read an older build still needs, so Undo would let an older build write what this one no longer reads: it is not offered.", null, []);
        }

        var parts = ConversionParts.Of(doors);
        var lines = new List<string>();
        foreach (var entry in Done(latest))
        {
            if (parts.FirstOrDefault(each => each.Name == entry.Part) is not { } part) return (latest, $"`{entry.Part}` is no part this build reads, so its Undo is not offered.", null, []);
            if (part.ReadsGone is { } gone) return (latest, gone, null, []);
            var undo = part.UndoPlan(entry);
            if (undo.Refusal is { } refusal) return (latest, refusal, null, []);
            lines.AddRange(undo.Lines);
        }

        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("undo\n" + latest.Digest + "\n" + string.Join('\n', lines))));
        return (latest, null, digest, lines);
    }

    /// <summary>The receipt's done parts, last applied first: the order Undo takes them in.</summary>
    private static IEnumerable<PartReceipt> Done(HomeReceipt receipt) =>
        receipt.Parts.Where(each => each.Outcome == ConversionOutcomes.Done).OrderByDescending(each => ConversionParts.Rank(each.Part));

    /// <summary>The undo pressed: each done part put back, last applied first, and the receipt marked undone. Refused <c>moved</c> where anything moved since.</summary>
    public static ConvertResult Undo(ConvertDoors doors, string digest)
    {
        if (!ConversionOutcomes.IsDigest(digest)) return Refuse(digest, ConversionRefusals.Digest, "name the undo plan's digest, as its plan says it: 64 lower-case hex.");
        var plan = UndoPlan(doors);
        if (plan.Receipt is null || plan.Refusal is not null)
        {
            return Refuse(digest, plan.Refusal?.StartsWith("moved", StringComparison.Ordinal) == true ? ConversionRefusals.Moved : ConversionRefusals.NoneStands, plan.Refusal ?? "nothing stands to undo.");
        }

        if (plan.Digest != digest) return Refuse(digest, ConversionRefusals.Moved, $"the undo you read no longer holds, so nothing was written. Plan it again (now `{plan.Digest![..12]}`).");
        var receipt = plan.Receipt;
        var parts = ConversionParts.Of(doors);
        var undone = new List<string>();
        var back = new List<string>();
        foreach (var entry in Done(receipt))
        {
            var part = parts.First(each => each.Name == entry.Part);
            var said = part.UndoPlan(entry).Said ?? $"`{entry.Part}`'s writes are back";
            try
            {
                part.Undo(entry);
            }
            catch (DriverException refused)
            {
                return Refuse(digest, ConversionRefusals.Moved, undone.Count == 0 ? refused.Message : $"{refused.Message} ({string.Join(", ", undone)} undone already)");
            }

            undone.Add($"`{entry.Part}`");
            back.Add(said);
        }

        new HomeReceipts(doors.Home).Write(receipt with { UndoneAt = DateTimeOffset.UtcNow });
        return new(true, null, $"the conversion is undone: {string.Join("; ", back)}.", digest, [.. receipt.Parts.SelectMany(each => each.Operations)]) { Parts = receipt.Parts };
    }
}
