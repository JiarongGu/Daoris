namespace Daoris.Driver;

/// <summary>
/// A room's part of a place's recoverable apply (<c>PLACE_APPLY</c>): one operation, the quest to the repository's own session, which is the whole of
/// the room's receipt. It writes no configuration, saves no workflow and chains nothing; the quest's body is <see cref="RoomBrief"/>'s, the line the
/// declaration is read on and the key (<see cref="PlaceApplies.KeyOf"/>) as its last line, so a retry finds it and asks no second.
/// </summary>
/// <remarks>
/// <para><b>Judged again where it is asked.</b> The plan the person read pinned the ask, not the repository's line: a room that appeared since, a
/// hold, a set-up opened or a line that moved leaves the operation <c>failed</c> with the plan's own sentence, and nothing is published.</para>
/// <para><b>The rule.</b> The press adds <see cref="SetupPressRules.RoomRules"/> to the repository's scope beside what its set-up added, as every
/// press does through <see cref="SetupPress.ApplyAsync"/>, and takes back only what it added where the ask is refused. The repository is adopted, so
/// its sessions run the doctrine tool already; the rule is what lets a session on the protocol door run <c>sync</c> and <c>check</c> unasked.</para>
/// </remarks>
public static class PlaceRoomApplies
{
    /// <summary>The operations a room asked of in <paramref name="plan"/> authorizes: the quest to its one repository.</summary>
    public static IEnumerable<PlaceOperation> OperationsOf(PlacePlanned plan) =>
        plan.Asked.Room is not { } path
            ? []
            : plan.Repositories.Select(row => new PlaceOperation(
                PlaceOperations.RoomOf(row.Repository), PlaceOperations.Room, row.Repository, $"a room at `{path}` asked of its own session", PlaceOutcomes.Pending));

    /// <summary>
    /// The room's quest: found by its key where it is asked already, else judged again and asked now with the key as the last line of its body. The
    /// operation is <c>uncertain</c> in the receipt from before the ask until its answer, and settles by the key a retry reads.
    /// </summary>
    public static async Task<PlaceReceipt> AskAsync(
        PlaceApplyDoors doors, PlaceReceipts receipts, PlaceReceipt receipt, PlaceOperation operation, CancellationToken ct)
    {
        PlaceReceipt Keep(Func<PlaceOperation, PlaceOperation> change)
        {
            receipt = receipt.With(operation.Id, change);
            receipts.Write(receipt);
            return receipt;
        }

        var repository = operation.Repository!;
        if (await PlaceApplies.KeyedAsync(doors, receipt.Digest, repository, ct).ConfigureAwait(false) is { } found)
        {
            return Keep(each => each with { Outcome = PlaceOutcomes.Done, Quest = found, Note = null });
        }

        var ask = receipt.Ask;
        var config = doors.Config();
        var registry = await doors.Place.RegistryAsync(ct).ConfigureAwait(false);
        if (registry.FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase)) is not { } row)
        {
            return Keep(each => each with { Outcome = PlaceOutcomes.Failed, Note = SetupPress.NotOnRegistry(repository).Sentence });
        }

        var quests = await doors.Place.QuestsAsync(ct).ConfigureAwait(false);
        var (refusals, read) = await PlaceRooms.JudgeAsync(doors.Place, config, doors.Unadopted, row, ask.Room!, quests, ct).ConfigureAwait(false);
        if (refusals.Count > 0 || read is not { Line: { } line, Commit: { } commit })
        {
            return Keep(each => each with
            {
                Outcome = PlaceOutcomes.Failed, Note = refusals.Count > 0 ? string.Join(" ", refusals.Select(refusal => refusal.Sentence)) : "its line could not be read.",
            });
        }

        var (title, body) = RoomBrief.Compose(new RoomBriefInput(row.Repository, ask.Room!, ask.Purpose!, line, commit, read.Declared, read.Lanes, read.Folders));
        var plan = new SetupPlan(row.Repository)
        {
            Workspace = row.Workspace,
            Adapter = ActorChoices.BuildAgent(config),
            Case = SetupCase.Room,
            Alone = !config.Drivable.Contains(row.Repository, StringComparer.OrdinalIgnoreCase),
            Title = title,
            Body = $"{body}\n{PlaceApplies.KeyOf(receipt.Digest)}\n",
        };
        Keep(each => each with { Outcome = PlaceOutcomes.Uncertain, Note = null });
        SetupOutcome outcome;
        try
        {
            outcome = await SetupPress.ApplyAsync(plan, doors.Setup, doors.Home, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or HttpRequestException or TaskCanceledException)
        {
            // Asked, or not: its answer is lost, and the quest found by its key says.
            return Keep(each => each with { Note = error.Message });
        }

        return Keep(each => outcome.Published
            ? each with { Outcome = PlaceOutcomes.Done, Quest = outcome.QuestId, Note = outcome.Message }
            : each with { Outcome = PlaceOutcomes.Failed, Note = outcome.Message });
    }
}
