namespace Daoris.Driver;

/// <summary>
/// Where the person's work follows the set-up. The set-up quest is asked through the set-up press's door, which publishes one quest and takes no
/// chain, so the request stays on the receipt (<see cref="CreateAsk.Work"/>, kept with the plan's digest) until the door takes a step behind it;
/// this is the one place that step is added.
/// </summary>
public static class RepositoryCreateWork
{
    /// <summary>The receipt after the work is chained behind the set-up quest it names; unchanged while the door takes no chain.</summary>
    public static Task<CreateReceipt> ChainAsync(CreateDoors doors, CreateReceipts receipts, CreateReceipt receipt, CancellationToken ct) =>
        Task.FromResult(receipt);
}
