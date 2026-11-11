using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A department's recoverable creation (<c>REGISTRY_CREATE</c>'s body): the plan's steps made in order, each operation's outcome kept in a
/// receipt written before its first effect. It plans again and refuses <c>moved</c> where the digest differs, and a digest already holding an
/// unfinished receipt goes on from it: the receipt is what is authorized.
/// </summary>
/// <remarks>
/// <para><b>Each step is harmless to repeat or undone by the next press.</b> The folder is made where it is not, the history is read before it is
/// made (a folder holding exactly the receipt's empty commit is the history done), the registration is an upsert found again by the registry, the
/// drive is a setting, and the set-up quest is found by its key before it is asked.</para>
/// <para><b>A failure leaves everything.</b> The folder, the commit and the receipt stay; nothing is deleted. The person removes what they do not
/// want: <c>repository retire</c> takes the row off, and the folder is theirs.</para>
/// <para><b>No content.</b> The press writes no <c>daoris.json</c>, no brief and no code; what the department says of itself is its own session's,
/// asked by the set-up quest. Nothing is pushed and no remote is made.</para>
/// </remarks>
public static class RepositoryCreates
{
    /// <summary>The line of a set-up quest's body that names the creation it is asked by, so a retry finds it.</summary>
    public static string KeyOf(string digest) => $"<!-- daoris repository-create {digest} -->";

    /// <summary>
    /// Apply the plan <paramref name="digest"/> names for <paramref name="ask"/>. A digest already holding an unfinished receipt goes on from it,
    /// whatever <paramref name="ask"/> says.
    /// </summary>
    public static async Task<CreateResult> ApplyAsync(CreateDoors doors, CreateAsk ask, string digest, CancellationToken ct = default)
    {
        if (!PlaceDigests.Is(digest)) return Refuse(digest, PlaceApplyRefusals.Digest, "name the plan's digest, as its plan says it: 64 lower-case hex.");

        var receipts = new CreateReceipts(doors.Home);
        CreateReceipt? receipt;
        try
        {
            receipt = receipts.Read(digest);
        }
        catch (DriverException unread)
        {
            return Refuse(digest, PlaceApplyRefusals.Receipt, unread.Message);
        }

        if (receipt is { Finished: true }) return Result(receipt, null);
        var plan = await RepositoryCreatePlans.PlanAsync(doors, receipt?.Ask ?? ask, ct).ConfigureAwait(false);
        if (plan.Digest != digest)
        {
            return Refuse(digest, PlaceApplyRefusals.Moved,
                $"the plan you read no longer holds: a value, the registry or the folder moved since, so nothing was written. Plan it again (now `{plan.Digest[..12]}`).");
        }

        if (!plan.Applies)
        {
            return Refuse(digest, PlaceApplyRefusals.Refused, $"the plan refuses, so nothing was written: {string.Join(" ", plan.Refusals.Select(each => each.Sentence))}");
        }

        if (receipt is null)
        {
            receipt = Begin(plan);
            receipts.Write(receipt);
        }

        return await ContinueAsync(doors, receipts, receipt, ct).ConfigureAwait(false);
    }

    private static CreateResult Refuse(string digest, string code, string message) => new(false, code, message, digest, []);

    /// <summary>The receipt of a plan about to be applied: every operation pending, authorized before the first effect.</summary>
    private static CreateReceipt Begin(CreatePlanned plan)
    {
        var name = plan.Asked.Name;
        PlaceOperation Pending(string id, string said) => new(id, id, name, said, PlaceOutcomes.Pending);
        var operations = plan.Steps.Select(step => Pending(step.Id, step.Said)).ToList();
        return new CreateReceipt(plan.Digest, DateTimeOffset.UtcNow, plan.Asked, plan.Workspace, plan.Folder, plan.Line, plan.Author, plan.RemoteStep, operations);
    }

    /// <summary>Each operation not done, in order: settled where a stop left it in flight, then made; the first that does not end done stops the rest.</summary>
    private static async Task<CreateResult> ContinueAsync(CreateDoors doors, CreateReceipts receipts, CreateReceipt receipt, CancellationToken ct)
    {
        string? stoppedAt = null;
        foreach (var id in receipt.Operations.Select(each => each.Id).ToList())
        {
            if (receipt.Of(id)!.Outcome == PlaceOutcomes.Done) continue;
            receipt = await SettleAsync(doors, receipts, receipt, id, ct).ConfigureAwait(false);
            if (receipt.Of(id)!.Outcome != PlaceOutcomes.Done)
            {
                receipt = id switch
                {
                    CreateOperations.Folder => MakeFolder(receipts, receipt),
                    CreateOperations.History => await MakeHistoryAsync(receipts, receipt, ct).ConfigureAwait(false),
                    CreateOperations.Registration => await RegisterAsync(doors, receipts, receipt, ct).ConfigureAwait(false),
                    CreateOperations.Driving => Drive(doors, receipts, receipt),
                    _ => await AskSetUpAsync(doors, receipts, receipt, ct).ConfigureAwait(false),
                };
            }
            if (receipt.Of(id)!.Outcome != PlaceOutcomes.Done)
            {
                stoppedAt = id;
                break;
            }

            if (id == CreateOperations.SetUp) receipt = await RepositoryCreateWork.ChainAsync(doors, receipts, receipt, ct).ConfigureAwait(false);
        }

        return Result(receipt, stoppedAt);
    }

    private static CreateReceipt Write(CreateReceipts receipts, CreateReceipt receipt)
    {
        receipts.Write(receipt);
        return receipt;
    }

    private static CreateReceipt Outcome(CreateReceipts receipts, CreateReceipt receipt, string id, string outcome, string? note = null, string? quest = null) =>
        Write(receipts, receipt.With(id, each => each with { Outcome = outcome, Note = note, Quest = quest ?? each.Quest }));

    /// <summary>
    /// An operation a stop left in flight, read by its postcondition: what is found is done, what is not is pending again. A failed operation
    /// is pending again too, since what failed it may have been put right.
    /// </summary>
    private static async Task<CreateReceipt> SettleAsync(CreateDoors doors, CreateReceipts receipts, CreateReceipt receipt, string id, CancellationToken ct)
    {
        var operation = receipt.Of(id)!;
        if (operation.Outcome == PlaceOutcomes.Failed) return Outcome(receipts, receipt, id, PlaceOutcomes.Pending);
        if (operation.Outcome != PlaceOutcomes.Uncertain) return receipt;

        var found = id switch
        {
            CreateOperations.Registration => (await doors.World.RegistryAsync(ct).ConfigureAwait(false))
                .Any(row => string.Equals(row.Repository, receipt.Ask.Name, StringComparison.OrdinalIgnoreCase) && row.Root is { } root && SameFolder(root, receipt.Folder)),
            CreateOperations.SetUp => await KeyedAsync(doors, receipt, ct).ConfigureAwait(false) is not null,
            _ => false,
        };
        if (!found) return Outcome(receipts, receipt, id, PlaceOutcomes.Pending);
        return id == CreateOperations.SetUp
            ? Outcome(receipts, receipt, id, PlaceOutcomes.Done, quest: await KeyedAsync(doors, receipt, ct).ConfigureAwait(false))
            : Outcome(receipts, receipt, id, PlaceOutcomes.Done);
    }

    private static CreateReceipt MakeFolder(CreateReceipts receipts, CreateReceipt receipt)
    {
        try
        {
            Directory.CreateDirectory(receipt.Folder);
            return Outcome(receipts, receipt, CreateOperations.Folder, PlaceOutcomes.Done);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Outcome(receipts, receipt, CreateOperations.Folder, PlaceOutcomes.Failed, $"the folder could not be made: {error.Message.TrimEnd('.', ' ')}.");
        }
    }

    /// <summary>The history: the folder read first, so a folder holding exactly this receipt's empty commit is done and one holding anything else is not touched.</summary>
    private static async Task<CreateReceipt> MakeHistoryAsync(CreateReceipts receipts, CreateReceipt receipt, CancellationToken ct)
    {
        var read = await CreateHistory.ReadAsync(receipt.Folder, ct).ConfigureAwait(false);
        if (read.State == CreateFolderState.Fresh && (receipt.Commit is null || receipt.Commit == read.Commit))
        {
            return Outcome(receipts, receipt with { Commit = read.Commit }, CreateOperations.History, PlaceOutcomes.Done);
        }

        if (read.State is CreateFolderState.Other or CreateFolderState.Fresh)
        {
            return Outcome(receipts, receipt, CreateOperations.History, PlaceOutcomes.Failed,
                $"{receipt.Folder} holds more than the empty commit this receipt names, so nothing was made there.");
        }

        receipt = Outcome(receipts, receipt, CreateOperations.History, PlaceOutcomes.Uncertain);
        var message = $"Open {receipt.Ask.Name}: {receipt.Ask.Purpose}";
        var (commit, problem) = await CreateHistory.InitAsync(receipt.Folder, receipt.Line ?? RepositoryCreatePlans.DefaultLine, receipt.Author!, message, ct).ConfigureAwait(false);
        return commit is null
            ? Outcome(receipts, receipt, CreateOperations.History, PlaceOutcomes.Failed, problem)
            : Outcome(receipts, receipt with { Commit = commit }, CreateOperations.History, PlaceOutcomes.Done);
    }

    /// <summary>The registration: an unadopted row at the folder, in the workspace, the body a checkout with no manifest gives and a folder department's the same.</summary>
    private static async Task<CreateReceipt> RegisterAsync(CreateDoors doors, CreateReceipts receipts, CreateReceipt receipt, CancellationToken ct)
    {
        var ask = receipt.Ask;
        var body = new JsonObject
        {
            ["repository"] = ask.Name,
            ["adopted"] = false,
            ["root"] = receipt.Folder,
            ["workspace"] = receipt.Workspace,
            ["kind"] = ask.Kind,
        };
        receipt = Outcome(receipts, receipt, CreateOperations.Registration, PlaceOutcomes.Uncertain);
        try
        {
            var (ok, message) = await doors.World.RegisterAsync(body, ct).ConfigureAwait(false);
            if (!ok) return Outcome(receipts, receipt, CreateOperations.Registration, PlaceOutcomes.Failed, message);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or TaskCanceledException)
        {
            // Registered, or not: its answer is lost, and the registry found by the folder says.
            return Outcome(receipts, receipt, CreateOperations.Registration, PlaceOutcomes.Uncertain, error.Message);
        }

        if (ask.Kind == PlaceKind.Folder) FolderFirstManifest.Keep(doors.Home, receipt.Workspace, ask.Name, receipt.Folder, []);
        return Outcome(receipts, receipt, CreateOperations.Registration, PlaceOutcomes.Done);
    }

    /// <summary>Drive on and hold off, by the one edit of the driver's file; left off, saying which step needs a remote and the next press, where the inherited workflow needs one.</summary>
    private static CreateReceipt Drive(CreateDoors doors, CreateReceipts receipts, CreateReceipt receipt)
    {
        if (receipt.RemoteStep is { } step)
        {
            return Outcome(receipts, receipt, CreateOperations.Driving, PlaceOutcomes.Done,
                $"driving stays off: the workflow `{receipt.Ask.Name}` inherits has a `{step}` step and it has no remote yet. "
                + $"Once it has one, `daoris place {receipt.Ask.Name} --preset <delivery> --apply` chooses how its work lands and drives it.");
        }

        try
        {
            var name = receipt.Ask.Name;
            doors.Change(config => config.WithDrivable(name, true).WithHold(name, false));
            return Outcome(receipts, receipt, CreateOperations.Driving, PlaceOutcomes.Done);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return Outcome(receipts, receipt, CreateOperations.Driving, PlaceOutcomes.Failed, $"the driver's choices could not be written: {error.Message.TrimEnd('.', ' ')}.");
        }
    }

    /// <summary>
    /// The set-up quest, built once as the set-up press builds it (<see cref="SetupPress"/>, the same plan <c>place --adoption</c> asks), with the
    /// creation's purpose and key as the body's last lines. Found by its key where it is asked already.
    /// </summary>
    private static async Task<CreateReceipt> AskSetUpAsync(CreateDoors doors, CreateReceipts receipts, CreateReceipt receipt, CancellationToken ct)
    {
        if (await KeyedAsync(doors, receipt, ct).ConfigureAwait(false) is { } found)
        {
            return Outcome(receipts, receipt, CreateOperations.SetUp, PlaceOutcomes.Done, quest: found);
        }

        var name = receipt.Ask.Name;
        SetupPlan plan;
        try
        {
            plan = await SetupPress.PlanAsync(doors.Setup, doors.Config(), doors.Unadopted, name, doors.Day, builds: null, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or HttpRequestException or TaskCanceledException)
        {
            return Outcome(receipts, receipt, CreateOperations.SetUp, PlaceOutcomes.Failed, $"its set-up could not be planned: {error.Message.TrimEnd('.', ' ')}.");
        }

        if (!plan.Pressable)
        {
            var why = plan.Refusals.Count > 0 ? plan.Refusals[0].Sentence : "there is nothing to ask.";
            return Outcome(receipts, receipt, CreateOperations.SetUp, PlaceOutcomes.Failed,
                $"{why} Its set-up is asked from the repository's page, or `daoris place {name} --preset <delivery> --adoption --apply`.");
        }

        receipt = Outcome(receipts, receipt, CreateOperations.SetUp, PlaceOutcomes.Uncertain);
        SetupOutcome outcome;
        try
        {
            var body = $"{plan.Body}\n\nWhy `{name}` was opened: {receipt.Ask.Purpose}\n\n{KeyOf(receipt.Digest)}\n";
            outcome = await SetupPress.ApplyAsync(plan with { Body = body }, doors.Setup, doors.Home, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or HttpRequestException or TaskCanceledException)
        {
            // Asked, or not: its answer is lost, and the quest found by its key says.
            return Outcome(receipts, receipt, CreateOperations.SetUp, PlaceOutcomes.Uncertain, error.Message);
        }

        return outcome.Published
            ? Outcome(receipts, receipt, CreateOperations.SetUp, PlaceOutcomes.Done, outcome.Message, outcome.QuestId)
            : Outcome(receipts, receipt, CreateOperations.SetUp, PlaceOutcomes.Failed, outcome.Message);
    }

    /// <summary>The quest to the new department whose body carries the creation's key, or null where none does.</summary>
    private static async Task<string?> KeyedAsync(CreateDoors doors, CreateReceipt receipt, CancellationToken ct)
    {
        var key = KeyOf(receipt.Digest);
        var quests = await doors.Setup.QuestsAsync(ct).ConfigureAwait(false);
        return quests.FirstOrDefault(quest => string.Equals(quest.To, receipt.Ask.Name, StringComparison.OrdinalIgnoreCase) && quest.Body.Contains(key, StringComparison.Ordinal))?.Id;
    }

    private static bool SameFolder(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    private static CreateResult Result(CreateReceipt receipt, string? stoppedAt) =>
        new(receipt.Finished, null, Words(receipt, stoppedAt), receipt.Digest, receipt.Operations) { Folder = receipt.Folder, Commit = receipt.Commit };

    /// <summary>The creation in a sentence, naming each operation's standing: <i>folder made; history made; registered; driving on; set-up asked</i>.</summary>
    public static string Words(CreateReceipt receipt, string? stoppedAt = null)
    {
        string Word(PlaceOperation operation, string done, string failed) => operation.Outcome switch
        {
            PlaceOutcomes.Done => done,
            PlaceOutcomes.Failed => failed,
            _ => operation.Outcome,
        };

        var parts = receipt.Operations.Select(each => each.Id switch
        {
            CreateOperations.Folder => $"folder {Word(each, "made", "not made")}",
            CreateOperations.History => $"history {Word(each, receipt.Commit is { } commit ? $"made (commit `{TextBounds.ShortCommit(commit, 12)}`)" : "made", "not made")}",
            CreateOperations.Registration => Word(each, "registered", "not registered"),
            CreateOperations.Driving => Word(each, receipt.RemoteStep is null ? "driving on" : "driving left off", "driving not set"),
            _ => $"set-up {Word(each, each.Quest is { Length: > 0 } quest ? $"asked (#{quest})" : "asked", "not asked")}",
        }).ToList();
        var notes = receipt.Operations
            .Where(each => (each.Outcome is PlaceOutcomes.Failed or PlaceOutcomes.Uncertain || each.Id == CreateOperations.Driving) && each.Note is { Length: > 0 })
            .Select(each => each.Note!.TrimEnd());
        var stop = stoppedAt is null ? "" : $" Nothing was undone: press the same again to go on from `{stoppedAt}`.";
        return string.Join("; ", parts) + (notes.Any() ? $" — {string.Join(" ", notes)}" : ".") + stop;
    }
}
