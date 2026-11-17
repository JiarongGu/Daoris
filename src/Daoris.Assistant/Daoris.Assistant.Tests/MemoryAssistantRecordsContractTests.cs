namespace Daoris.Assistant.Tests;

/// <summary>The assistant's records' contract on the memory fake.</summary>
public sealed class MemoryAssistantRecordsContractTests : AssistantRecordsContract
{
    protected override IAssistantRecords Backend { get; } = new MemoryAssistantRecords();
}
