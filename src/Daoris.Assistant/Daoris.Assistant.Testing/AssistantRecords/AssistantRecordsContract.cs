using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="IAssistantRecords"/> passes: <see cref="MemoryAssistantRecords"/> in the assistant's suite, the desktop
/// adapter over the home's files and the desktop store in its own.
/// </summary>
public abstract class AssistantRecordsContract
{
    private static readonly CancellationToken None = CancellationToken.None;

    /// <summary>The records under test, none kept.</summary>
    protected abstract IAssistantRecords Backend { get; }

    [Fact]
    public async Task A_record_kept_reads_back_whole()
    {
        var record = new AssistantRecord(AssistantRecordKind.Report, "conversation-1", """{"range":"1-4","findings":[]}""");

        await Backend.KeepAsync(record, None);

        Assert.Equal(record, await Backend.ReadAsync(AssistantRecordKind.Report, "conversation-1", None));
    }

    [Fact]
    public async Task Keeping_under_the_same_key_replaces_it()
    {
        await Backend.KeepAsync(new(AssistantRecordKind.Focus, "conversation-1", "workspace:game"), None);
        await Backend.KeepAsync(new(AssistantRecordKind.Focus, "conversation-1", "workspace:engine"), None);

        Assert.Equal("workspace:engine", (await Backend.ReadAsync(AssistantRecordKind.Focus, "conversation-1", None))!.Body);
        Assert.Single(await Backend.ListAsync(AssistantRecordKind.Focus, None));
    }

    [Fact]
    public async Task A_kind_lists_its_records_by_key_and_no_other_kinds()
    {
        await Backend.KeepAsync(new(AssistantRecordKind.Receipt, "conversation-2", "two"), None);
        await Backend.KeepAsync(new(AssistantRecordKind.Receipt, "conversation-1", "one"), None);
        await Backend.KeepAsync(new(AssistantRecordKind.Mark, "conversation-1", "marked"), None);

        var receipts = await Backend.ListAsync(AssistantRecordKind.Receipt, None);

        Assert.Equal(["conversation-1", "conversation-2"], receipts.Select(each => each.Key));
        Assert.Equal(["one", "two"], receipts.Select(each => each.Body));
        Assert.Empty(await Backend.ListAsync(AssistantRecordKind.Press, None));
    }

    [Fact]
    public async Task A_key_not_kept_reads_as_none()
    {
        await Backend.KeepAsync(new(AssistantRecordKind.Mark, "conversation-1", "marked"), None);

        Assert.Null(await Backend.ReadAsync(AssistantRecordKind.Mark, "conversation-2", None));
        Assert.Null(await Backend.ReadAsync(AssistantRecordKind.Focus, "conversation-1", None));
    }
}
