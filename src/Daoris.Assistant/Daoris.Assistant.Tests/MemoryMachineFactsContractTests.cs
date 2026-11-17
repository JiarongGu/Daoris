namespace Daoris.Assistant.Tests;

/// <summary>The machine facts' contract on the memory fake.</summary>
public sealed class MemoryMachineFactsContractTests : MachineFactsContract
{
    private readonly MemoryMachineFacts _facts = new();

    protected override IMachineFacts Backend => _facts;

    protected override Task AnswerAsync(string topic, IReadOnlyList<MachineFact> facts)
    {
        _facts.Answer(topic, facts);
        return Task.CompletedTask;
    }
}
