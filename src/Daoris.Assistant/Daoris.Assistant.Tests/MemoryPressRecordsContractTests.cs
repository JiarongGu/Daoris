namespace Daoris.Assistant.Tests;

/// <summary>The presses' contract on the memory fake.</summary>
public sealed class MemoryPressRecordsContractTests : PressRecordsContract
{
    private readonly MemoryPressRecords _presses = new();

    protected override IPressRecords Backend => _presses;

    protected override void Press(PressMade press) => _presses.Press(press);
}
