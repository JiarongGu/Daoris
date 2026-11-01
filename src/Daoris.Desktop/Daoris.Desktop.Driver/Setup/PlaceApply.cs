namespace Daoris.Driver;

/// <summary>What an apply reads and writes: the place's world, the set-up press's world, the home, and the one edit of <c>driver.json</c>.</summary>
/// <param name="Config">The driver's file as it stands now.</param>
/// <param name="Change">The standing choice's one edit (<c>DriverContext.Change</c>): the file read, edited and written, then the loop nudged.</param>
public sealed record PlaceApplyDoors(
    IPlaceWorld Place, ISetupWorld Setup, string Home, IWorkplacePluginCatalog Plugins, SessionWire Door,
    Func<DriverConfig> Config, Action<Func<DriverConfig, DriverConfig>> Change, DateOnly Day);

/// <summary>What an apply came to: whether every operation is done, a code where it was refused, and each operation as the receipt keeps it.</summary>
public sealed record PlaceResult(bool Done, string? Refused, string Message, string Digest, IReadOnlyList<PlaceOperation> Operations);

/// <summary>The codes an apply is refused by, each with nothing written.</summary>
public static class PlaceApplyRefusals
{
    /// <summary>The plan the person read no longer holds: a value, a declaration or a tip moved since.</summary>
    public const string Moved = "moved";

    /// <summary>The plan refuses (<see cref="PlacePlanned.Applies"/>), or a tip could not be read.</summary>
    public const string Refused = "refused";

    public const string Unread = "unread";

    /// <summary>An acceptance chosen that the plan does not offer.</summary>
    public const string NotOffered = "not-offered";

    /// <summary>A word that is no plan's digest.</summary>
    public const string Digest = "digest";

    /// <summary>The receipt of this plan is there and does not read.</summary>
    public const string Receipt = "receipt";
}

/// <summary>
/// A place's recoverable apply (AGENTSPLACE1b2, <c>PLACE_APPLY</c>'s body; the Agents-place design §4): the plan's six steps, each operation's outcome
/// kept in a receipt written before its first effect. It plans again over the configuration as it stands and refuses <c>moved</c> where the
/// digest differs, reads the tips and refuses <c>moved</c> for a chosen blob that is no longer the tip's, writes every configuration change and
/// acceptance in one <see cref="PlaceApplyDoors.Change"/>, judged first, and asks each adoption quest last, keyed by the digest.
/// </summary>
/// <remarks>
/// <para><b>Steps and the stop between them.</b> An operation is <c>pending</c> until it begins, <c>uncertain</c> from its begin to its answer,
/// then <c>done</c> or <c>failed</c>; each change is written, so a stop at any boundary leaves the outcome the receipt names. Adoption is never
/// asked while the configuration is not <c>done</c>, which is what keeps the quest from starting early.</para>
/// <para><b>Settling.</b> An uncertain step is read, never replayed blind (<see cref="SettleAsync"/>): the configuration's values and
/// acceptances against the plan's, the quest found by its key (<see cref="KeyOf"/>, a line of its body). What is found is <c>done</c>; what is
/// not is <c>pending</c> again, since nothing of it landed, and a press of the same digest goes on from there.</para>
/// <para><b>No named workflow is saved here:</b> every preset is Current's <c>land</c>, so a plan carries none; the receipt's operations are
/// open to the step when one does. The <c>keep</c> ask writes a subset of that (no landing, no workflow choice), so a saved workflow survives.</para>
/// <para><b>The write guard.</b> The configuration is judged again inside the edit, over the file as that edit reads it (each repository's
/// changes must be the plan's), and written under <see cref="DriverConfig.Save"/>'s own lock, which keeps another writer's keys. The bound is
/// the lock's: a change made between that read and the rename is kept beside this one, not refused.</para>
/// </remarks>
public static class PlaceApplies
{
    /// <summary>The line of an adoption quest's body that names the plan it was asked by, so a retry finds it.</summary>
    public static string KeyOf(string digest) => $"<!-- daoris place-apply {digest} -->";

    /// <summary>
    /// Apply the plan <paramref name="digest"/> names, <paramref name="accepted"/> the acceptances chosen. A digest already holding a receipt goes on
    /// from it, whatever <paramref name="ask"/> and <paramref name="accepted"/> say: the receipt is what was authorized.
    /// </summary>
    public static async Task<PlaceResult> ApplyAsync(
        PlaceApplyDoors doors, PlaceAsk ask, string digest, IReadOnlyList<PlaceAccept> accepted, CancellationToken ct = default)
    {
        if (!PlaceDigests.Is(digest))
        {
            return Refuse(digest, PlaceApplyRefusals.Digest, "name the plan's digest, as its plan says it: 64 lower-case hex.");
        }

        var receipts = new PlaceReceipts(doors.Home);
        PlaceReceipt? receipt;
        try
        {
            receipt = receipts.Read(digest);
        }
        catch (DriverException unread)
        {
            return Refuse(digest, PlaceApplyRefusals.Receipt, unread.Message);
        }

        PlacePlanned? planned = null;
        if (receipt is null)
        {
            var begun = await BeginAsync(doors, receipts, ask, digest, accepted, ct).ConfigureAwait(false);
            if (begun.Refusal is not null) return begun.Refusal;
            (receipt, planned) = (begun.Receipt!, begun.Plan);
        }
        else
        {
            receipt = await SettleAsync(doors, receipts, receipt, ct).ConfigureAwait(false);
        }

        return await ContinueAsync(doors, receipts, receipt, planned, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A restart's read of every receipt (the design §4): each uncertain step settled by its postcondition, nothing asked and nothing written to
    /// the configuration. Returns the receipts as they now stand.
    /// </summary>
    public static async Task<IReadOnlyList<PlaceReceipt>> SettleAllAsync(PlaceApplyDoors doors, CancellationToken ct = default)
    {
        var receipts = new PlaceReceipts(doors.Home);
        var settled = new List<PlaceReceipt>();
        foreach (var receipt in receipts.All()) settled.Add(await SettleAsync(doors, receipts, receipt, ct).ConfigureAwait(false));
        return settled;
    }

    private static PlaceResult Refuse(string digest, string code, string message) => new(false, code, message, digest, []);

    private sealed record Begun(PlaceReceipt? Receipt, PlacePlanned? Plan, PlaceResult? Refusal);

    /// <summary>Steps 2 and 3 for a plan with no receipt: planned again, its acceptances checked against the tips; then the receipt, written.</summary>
    private static async Task<Begun> BeginAsync(
        PlaceApplyDoors doors, PlaceReceipts receipts, PlaceAsk ask, string digest, IReadOnlyList<PlaceAccept> accepted, CancellationToken ct)
    {
        var plan = await PlacePlans.PlanAsync(doors.Place, doors.Config(), doors.Home, doors.Plugins, doors.Door, ask, ct).ConfigureAwait(false);
        if (plan.Digest != digest) return Stopped(digest, PlaceApplyRefusals.Moved, Moved(plan));
        if (!plan.Applies)
        {
            return Stopped(digest, PlaceApplyRefusals.Refused, $"the plan refuses, so nothing was written: {string.Join(" ", plan.Refusals.Select(each => each.Sentence))}");
        }

        var chosen = new List<PlaceAccept>();
        foreach (var accept in accepted.DistinctBy(each => (each.Repository.ToLowerInvariant(), each.Purpose)))
        {
            var row = plan.Repositories.FirstOrDefault(each => string.Equals(each.Repository, accept.Repository, StringComparison.OrdinalIgnoreCase));
            if (row?.Declaration is not { } declaration || declaration.Acceptances.All(each => each.Purpose != accept.Purpose))
            {
                return Stopped(digest, PlaceApplyRefusals.NotOffered,
                    $"the plan offers no `{accept.Purpose}` acceptance for `{accept.Repository}`, so nothing was written.");
            }

            chosen.Add(new PlaceAccept(row.Repository, accept.Purpose) { Blob = declaration.Blob });
        }

        var (_, _, refusal) = await ReadAsync(doors, plan, chosen, digest, ct).ConfigureAwait(false);
        if (refusal is not null) return new Begun(null, null, refusal);

        var operations = new List<PlaceOperation>();
        var configured = plan.Changes.Where(each => each.Change.Key != PlacePlans.SetupPressKey).Select(each => each.Change.Said).Distinct().ToList();
        if (configured.Count > 0 || chosen.Count > 0)
        {
            var said = string.Join("; ", configured.Concat(chosen.Select(each => $"accepts `{each.Repository}`'s declaration for {each.Purpose}")));
            operations.Add(new PlaceOperation(PlaceOperations.Configuration, PlaceOperations.Configuration, null, said, PlaceOutcomes.Pending));
        }

        if (plan.Asked.Adoption)
        {
            operations.AddRange(plan.Repositories.Select(row => new PlaceOperation(
                PlaceOperations.AdoptionOf(row.Repository), PlaceOperations.Adoption, row.Repository, "a set-up asked of its own session", PlaceOutcomes.Pending)));
        }

        var receipt = new PlaceReceipt(digest, DateTimeOffset.UtcNow, plan.Asked, chosen, operations);
        receipts.Write(receipt);
        return new Begun(receipt, plan, null);
    }

    private static Begun Stopped(string digest, string code, string message) => new(null, null, Refuse(digest, code, message));

    private static string Moved(PlacePlanned plan) =>
        $"the plan you read no longer holds: a value, a declaration or the line moved since, so nothing was written. Plan it again (now `{plan.Digest[..12]}`).";

    /// <summary>Step 3: each repository's tip read, and each chosen blob held against the tip's.</summary>
    private static async Task<(IReadOnlyDictionary<string, RepoView> Rows, IReadOnlyDictionary<string, PlaceTip> Tips, PlaceResult? Refusal)> ReadAsync(
        PlaceApplyDoors doors, PlacePlanned plan, IReadOnlyList<PlaceAccept> chosen, string digest, CancellationToken ct)
    {
        var registry = await doors.Place.RegistryAsync(ct).ConfigureAwait(false);
        var rows = new Dictionary<string, RepoView>(StringComparer.OrdinalIgnoreCase);
        var tips = new Dictionary<string, PlaceTip>(StringComparer.OrdinalIgnoreCase);
        var config = doors.Config();
        foreach (var each in plan.Repositories)
        {
            if (registry.FirstOrDefault(row => string.Equals(row.Repository, each.Repository, StringComparison.OrdinalIgnoreCase)) is not { } row)
            {
                return (rows, tips, Refuse(digest, PlaceApplyRefusals.Moved, Moved(plan)));
            }

            rows[each.Repository] = row;
            tips[each.Repository] = await doors.Place.TipAsync(row, config, ct).ConfigureAwait(false);
        }

        foreach (var accept in chosen)
        {
            var tip = tips[accept.Repository];
            if (tip.Gates is null)
            {
                return (rows, tips, Refuse(digest, PlaceApplyRefusals.Unread,
                    $"{tip.Problem ?? $"`{accept.Repository}`'s line could not be read here."} Nothing was accepted, so nothing was written."));
            }

            if (tip.Gates != accept.Blob)
            {
                return (rows, tips, Refuse(digest, PlaceApplyRefusals.Moved,
                    $"`{accept.Repository}`'s line holds another declaration than the one you read, so nothing was written: read it again, then accept it."));
            }
        }

        return (rows, tips, null);
    }

    /// <summary>Operations done stay done, failed stay failed; a pending one that the apply's earlier step ended is failed with the reason.</summary>
    private static async Task<PlaceResult> ContinueAsync(
        PlaceApplyDoors doors, PlaceReceipts receipts, PlaceReceipt receipt, PlacePlanned? planned, CancellationToken ct)
    {
        string? refused = null;
        if (receipt.Of(PlaceOperations.Configuration) is { Outcome: PlaceOutcomes.Pending })
        {
            (receipt, refused) = await ConfigureAsync(doors, receipts, receipt, planned, ct).ConfigureAwait(false);
        }

        var configuration = receipt.Of(PlaceOperations.Configuration)?.Outcome ?? PlaceOutcomes.Done;
        foreach (var operation in receipt.Operations.Where(each => each.Kind == PlaceOperations.Adoption && each.Outcome == PlaceOutcomes.Pending).ToList())
        {
            if (configuration == PlaceOutcomes.Failed)
            {
                receipt = Write(receipts, receipt.With(operation.Id, each => each with { Outcome = PlaceOutcomes.Failed, Note = "the configuration was not applied, so it was not asked." }));
            }
            else if (configuration == PlaceOutcomes.Done)
            {
                receipt = await AdoptAsync(doors, receipts, receipt, operation, ct).ConfigureAwait(false);
            }
        }

        var done = receipt.Operations.All(each => each.Outcome == PlaceOutcomes.Done);
        return new PlaceResult(done, refused, Said(receipt), receipt.Digest, receipt.Operations);
    }

    private static PlaceReceipt Write(PlaceReceipts receipts, PlaceReceipt receipt)
    {
        receipts.Write(receipt);
        return receipt;
    }

    /// <summary>
    /// Steps 2, 3 and 5 of the configuration's operation: planned again unless the plan is at hand, the tips read, then every change and acceptance
    /// in one edit, judged first. The operation is <c>uncertain</c> in the receipt from before the edit until its answer.
    /// </summary>
    private static async Task<(PlaceReceipt Receipt, string? Refused)> ConfigureAsync(
        PlaceApplyDoors doors, PlaceReceipts receipts, PlaceReceipt receipt, PlacePlanned? planned, CancellationToken ct)
    {
        PlaceReceipt Failed(string note) => Write(receipts, receipt.With(PlaceOperations.Configuration, each => each with { Outcome = PlaceOutcomes.Failed, Note = note }));

        planned ??= await PlacePlans.PlanAsync(doors.Place, doors.Config(), doors.Home, doors.Plugins, doors.Door, receipt.Ask, ct).ConfigureAwait(false);
        if (planned.Digest != receipt.Digest) return (Failed(Moved(planned)), PlaceApplyRefusals.Moved);
        var (rows, tips, refusal) = await ReadAsync(doors, planned, receipt.Accepted, receipt.Digest, ct).ConfigureAwait(false);
        if (refusal is not null) return (Failed(refusal.Message), refusal.Refused);

        IReadOnlyList<QuestView> quests = receipt.Ask.Drive || receipt.Ask.Adoption ? await doors.Place.QuestsAsync(ct).ConfigureAwait(false) : [];
        receipt = Write(receipts, receipt.With(PlaceOperations.Configuration, each => each with { Outcome = PlaceOutcomes.Uncertain, Note = null }));
        try
        {
            doors.Change(config => Judged(doors, config, planned, receipt, rows, tips, quests));
        }
        catch (DriverException refused)
        {
            // Judged first, written never: nothing of it landed.
            var moved = refused is PlaceMovedException;
            return (Failed(refused.Message), moved ? PlaceApplyRefusals.Moved : PlaceApplyRefusals.Refused);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The edit's answer is lost: it may have landed, and the postcondition says.
            return (Write(receipts, receipt.With(PlaceOperations.Configuration, each => each with { Note = error.Message })), null);
        }

        return (Write(receipts, receipt.With(PlaceOperations.Configuration, each => each with { Outcome = PlaceOutcomes.Done, Note = null })), null);
    }

    /// <summary>
    /// The configuration after the plan, over <paramref name="config"/> as the edit read it: each repository's changes made through the doors' own
    /// edits and held to the plan's, then the chosen acceptances, each by its door's edit. Throws <see cref="PlaceMovedException"/> where a
    /// repository's changes are no longer the plan's.
    /// </summary>
    private static DriverConfig Judged(
        PlaceApplyDoors doors, DriverConfig config, PlacePlanned plan, PlaceReceipt receipt, IReadOnlyDictionary<string, RepoView> rows,
        IReadOnlyDictionary<string, PlaceTip> tips, IReadOnlyList<QuestView> quests)
    {
        foreach (var row in plan.Repositories)
        {
            var applied = PlacePlans.Apply(config, rows[row.Repository], receipt.Ask, tips[row.Repository], doors.Plugins, doors.Door, quests);
            if (applied.Refusals.Any(each => each.Part != PlacePlans.Adoption) || !applied.Changes.SequenceEqual(row.Changes))
            {
                throw new PlaceMovedException($"`{row.Repository}`'s configuration is no longer what the plan read, so nothing was written: plan it again.");
            }

            config = applied.After;
        }

        foreach (var accept in receipt.Accepted)
        {
            config = Accepted(doors.Home, config, accept, plan.Repositories.First(each => each.Repository == accept.Repository).Workspace);
        }

        return config;
    }

    /// <summary>The acceptance made through its own door's edit: the landed route's, with the gates it verifies by, or the integration's blob.</summary>
    private static DriverConfig Accepted(string home, DriverConfig config, PlaceAccept accept, string workspace)
    {
        if (accept.Purpose == PlacePlans.Integration) return config.WithIntegrationGates(accept.Repository, accept.Blob);
        var landed = WorkflowSelection.ResolveLanded(config, accept.Repository, workspace);
        var found = landed.Workflow == WorkflowSelection.Current ? null : WorkflowPorts.Store(home).Load(landed.Workflow);
        return WorkflowSelection.Apply(config, new WorkflowUse(landed.Workflow, accept.Repository, null, null)
        {
            Landed = true,
            Gates = found?.Read.Verifies == true ? accept.Blob : null,
            Pipeline = found?.Read.Pipeline,
        });
    }

    /// <summary>
    /// Step 6, one repository's adoption quest: found by its key where it was asked before, else asked now, as the set-up press asks it, with the
    /// key as the last line of its body. The operation is <c>uncertain</c> in the receipt from before the ask until its answer.
    /// </summary>
    private static async Task<PlaceReceipt> AdoptAsync(
        PlaceApplyDoors doors, PlaceReceipts receipts, PlaceReceipt receipt, PlaceOperation operation, CancellationToken ct)
    {
        var repository = operation.Repository!;
        if (await KeyedAsync(doors, receipt.Digest, repository, ct).ConfigureAwait(false) is { } found)
        {
            return Write(receipts, receipt.With(operation.Id, each => each with { Outcome = PlaceOutcomes.Done, Quest = found, Note = null }));
        }

        var plan = await SetupPress.PlanAsync(doors.Setup, doors.Config(), doors.Door, repository, doors.Day, receipt.Ask.Builds, ct).ConfigureAwait(false);
        if (!plan.Pressable)
        {
            var why = plan.Refusals.Count > 0 ? plan.Refusals[0].Sentence : "there is nothing to ask.";
            return Write(receipts, receipt.With(operation.Id, each => each with { Outcome = PlaceOutcomes.Failed, Note = why }));
        }

        receipt = Write(receipts, receipt.With(operation.Id, each => each with { Outcome = PlaceOutcomes.Uncertain, Note = null }));
        SetupOutcome outcome;
        try
        {
            outcome = await SetupPress.ApplyAsync(plan with { Body = $"{plan.Body}\n\n{KeyOf(receipt.Digest)}\n" }, doors.Setup, doors.Home, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or HttpRequestException or TaskCanceledException)
        {
            // Asked, or not: its answer is lost, and the quest found by its key says.
            return Write(receipts, receipt.With(operation.Id, each => each with { Note = error.Message }));
        }

        return Write(receipts, receipt.With(operation.Id, each => outcome.Published
            ? each with { Outcome = PlaceOutcomes.Done, Quest = outcome.QuestId, Note = outcome.Message }
            : each with { Outcome = PlaceOutcomes.Failed, Note = outcome.Message }));
    }

    /// <summary>The quest to <paramref name="repository"/> whose body carries the plan's key, or null where none does.</summary>
    private static async Task<string?> KeyedAsync(PlaceApplyDoors doors, string digest, string repository, CancellationToken ct)
    {
        var key = KeyOf(digest);
        var quests = await doors.Setup.QuestsAsync(ct).ConfigureAwait(false);
        return quests.FirstOrDefault(quest => string.Equals(quest.To, repository, StringComparison.OrdinalIgnoreCase) && quest.Body.Contains(key, StringComparison.Ordinal))?.Id;
    }

    /// <summary>
    /// Each uncertain operation read by its postcondition: the configuration's values and acceptances, the quest found by its key. What is found is
    /// <c>done</c>; what is not is <c>pending</c> again. Reads only.
    /// </summary>
    private static async Task<PlaceReceipt> SettleAsync(PlaceApplyDoors doors, PlaceReceipts receipts, PlaceReceipt receipt, CancellationToken ct)
    {
        foreach (var operation in receipt.Operations.Where(each => each.Outcome == PlaceOutcomes.Uncertain).ToList())
        {
            PlaceOperation settled;
            if (operation.Kind == PlaceOperations.Configuration)
            {
                var holds = await ConfigurationHoldsAsync(doors, receipt, ct).ConfigureAwait(false);
                settled = holds
                    ? operation with { Outcome = PlaceOutcomes.Done, Note = null }
                    : operation with { Outcome = PlaceOutcomes.Pending, Note = "its edit did not land." };
            }
            else
            {
                var found = await KeyedAsync(doors, receipt.Digest, operation.Repository!, ct).ConfigureAwait(false);
                settled = found is not null
                    ? operation with { Outcome = PlaceOutcomes.Done, Quest = found, Note = null }
                    : operation with { Outcome = PlaceOutcomes.Pending, Note = "no quest was found for it." };
            }

            receipt = receipt.With(operation.Id, _ => settled);
            receipts.Write(receipt);
        }

        return receipt;
    }

    /// <summary>The configuration's postcondition: no repository has a change of the plan's left to make, and each chosen acceptance is as chosen.</summary>
    private static async Task<bool> ConfigurationHoldsAsync(PlaceApplyDoors doors, PlaceReceipt receipt, CancellationToken ct)
    {
        var registry = await doors.Place.RegistryAsync(ct).ConfigureAwait(false);
        IReadOnlyList<QuestView> quests = receipt.Ask.Drive || receipt.Ask.Adoption ? await doors.Place.QuestsAsync(ct).ConfigureAwait(false) : [];
        var config = doors.Config();
        foreach (var name in receipt.Ask.Repositories)
        {
            if (registry.FirstOrDefault(row => string.Equals(row.Repository, name, StringComparison.OrdinalIgnoreCase)) is not { } row) return false;
            var tip = await doors.Place.TipAsync(row, config, ct).ConfigureAwait(false);
            var applied = PlacePlans.Apply(config, row, receipt.Ask, tip, doors.Plugins, doors.Door, quests);
            if (applied.Changes.Any(each => each.Key != PlacePlans.SetupPressKey)) return false;
        }

        foreach (var accept in receipt.Accepted)
        {
            var workspace = RemoteTarget.Workspace(registry.FirstOrDefault(row => string.Equals(row.Repository, accept.Repository, StringComparison.OrdinalIgnoreCase))?.Workspace);
            var held = accept.Purpose == PlacePlans.Integration
                ? IntegrationAcceptance.Of(config, accept.Repository)
                : WorkflowSelection.ResolveLanded(config, accept.Repository, workspace).Gates;
            if (held != accept.Blob && !(accept.Purpose == PlacePlans.Landed && held is null)) return false;
        }

        return true;
    }

    /// <summary>The outcome in a sentence, naming each operation's standing: <i>configuration applied; adoption pending</i>.</summary>
    public static string Said(PlaceReceipt receipt)
    {
        string Word(PlaceOperation operation, string done, string failed) => operation.Outcome switch
        {
            PlaceOutcomes.Done => done,
            PlaceOutcomes.Failed => failed,
            _ => operation.Outcome,
        };

        var adoptions = receipt.Operations.Count(each => each.Kind == PlaceOperations.Adoption);
        var parts = receipt.Operations.Select(each => each.Kind == PlaceOperations.Configuration
            ? $"configuration {Word(each, "applied", "not applied")}"
            : $"adoption{(adoptions > 1 ? $" of `{each.Repository}`" : "")} {Word(each, "asked", "refused")}").ToList();
        var notes = receipt.Operations.Where(each => (each.Outcome is PlaceOutcomes.Failed or PlaceOutcomes.Uncertain) && each.Note is { Length: > 0 }).Select(each => each.Note!);
        return string.Join("; ", parts) + (notes.Any() ? $" — {string.Join(" ", notes)}" : parts.Count == 0 ? "nothing to apply." : ".");
    }
}

/// <summary>The configuration is no longer what the plan read: raised inside the edit, so nothing is written.</summary>
public sealed class PlaceMovedException(string message) : DriverException(message);
