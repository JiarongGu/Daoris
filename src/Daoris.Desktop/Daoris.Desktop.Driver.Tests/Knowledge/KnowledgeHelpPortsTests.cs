using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Collection("knowledge review port")]
public sealed class KnowledgeHelpPortsTests
{
    [Fact]
    public void Help_installs_the_same_room_reads_and_place_tables()
    {
        var reader = KnowledgeHelp.Current;
        Assert.IsType<KnowledgeHelpReads>(reader);
        Assert.Null(reader.ScopeOf("default"));
        Assert.Equal(HelpRoom.ScopeOf(" work "), reader.ScopeOf(" work "));
        Assert.Same(HelpPlaces.Views, reader.Views);
        Assert.Same(HelpPlaces.Domains, reader.Domains);
        Assert.Same(HelpPlaces.Parts, reader.Parts);
        Assert.Equal(HelpPlaces.AskItem, reader.AskItem);
        Assert.Equal(HelpPlaces.OfferItem, reader.OfferItem);
        Assert.Equal(HelpRoom.Repository, KnowledgeRoomIdentity.Repository);
    }

    [Fact]
    public void A_section_and_a_workspace_read_a_source_port_that_is_no_Help_record()
    {
        var sources = new Sources();
        var machine = new HelpMachine { Whole = sources };
        var described = new HelpRoomIntro().Describe(machine, sources);
        Assert.Equal(HelpModeNames.Full, described.Mode);
        Assert.IsType<HelpRoomIntro>(sources.Section);
        Assert.Equal("work", machine.Within("work").Workspace);
        Assert.Null(machine.Workspace);
    }

    [Fact]
    public void An_uninstalled_Help_reader_throws_and_the_test_restores_it()
    {
        var reader = KnowledgeHelp.Current;
        try
        {
            KnowledgeHelp.Current = null!;
            Assert.Throws<InvalidOperationException>(() => KnowledgeHelp.Current);
        }
        finally
        {
            KnowledgeHelp.Current = reader;
        }
        Assert.Same(reader, KnowledgeHelp.Current);
    }

    private sealed class Sources : IHelpMachineSources
    {
        internal IHelpRoomSection? Section { get; private set; }

        public HelpMachine Describe(IHelpRoomSection section, HelpMachine machine)
        {
            Section = section;
            return machine with { Mode = HelpModeNames.Full };
        }

        public HelpMachine Describe(string? workspace) => new() { Workspace = workspace };
    }
}
