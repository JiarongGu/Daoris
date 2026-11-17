namespace Daoris.Assistant.Tests;

/// <summary>The checkouts' contract on the memory tree.</summary>
public sealed class MemoryCheckoutsContractTests : CheckoutsContract
{
    private readonly MemoryCheckouts _checkouts = new();

    public MemoryCheckoutsContractTests()
    {
        _checkouts.Add("game", "checkouts/game");
        _checkouts.Add("engine", "checkouts/engine");
    }

    protected override ICheckouts Backend => _checkouts;

    protected override string Checkout => "game";

    protected override Task PlantAsync(string path, string text)
    {
        _checkouts.Put(Checkout, path, text);
        return Task.CompletedTask;
    }
}
