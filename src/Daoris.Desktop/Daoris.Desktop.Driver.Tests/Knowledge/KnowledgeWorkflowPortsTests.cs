using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Collection("knowledge review port")]
public sealed class KnowledgeWorkflowPortsTests
{
    [Fact]
    public void Workflows_installs_its_choice_reader_and_saved_files_answer_only_the_rooms_facts()
    {
        Assert.IsType<KnowledgeWorkflowReads>(KnowledgeWorkflows.Current);
        Assert.Equal(("current", "current"), KnowledgeWorkflows.Current.Select(DriverConfig.Empty, "app", "default"));
        IKnowledgeWorkflowFile file = new WorkflowFileFound("docs", null, new NamedWorkflowRead("docs", "Documents", [], "unread"));
        Assert.Equal("docs", file.Id);
        Assert.Equal("Documents", file.Name);
        Assert.Equal("unread", file.Problem);
    }

    [Fact]
    public void An_uninstalled_choice_reader_throws_and_the_test_restores_it()
    {
        var reader = KnowledgeWorkflows.Current;
        try
        {
            KnowledgeWorkflows.Current = null!;
            Assert.Throws<InvalidOperationException>(() => KnowledgeWorkflows.Current);
        }
        finally
        {
            KnowledgeWorkflows.Current = reader;
        }
        Assert.Same(reader, KnowledgeWorkflows.Current);
    }
}
