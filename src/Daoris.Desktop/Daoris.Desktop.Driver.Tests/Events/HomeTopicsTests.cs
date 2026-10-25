using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ports the events declare and the subdomains above them answer (FOUNDATION1 mod-vii): where the files of a home topic are, for a topic
/// whose files belong to another subdomain. Each owner installs its answer when the driver's assembly loads, so every watch the look, the
/// shell's loop or a test builds hears exactly the files it always did; a topic nobody answers throws where a watch asks for it.
/// </summary>
public sealed class HomeTopicsTests
{
    // Never created: an answer only says where a topic's files are, and a place's folder is read when its stamps are.
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-home-topics-" + Guid.NewGuid().ToString("N")[..8]);

    [Fact]
    public void The_topics_whose_files_a_higher_subdomain_owns_are_answered_when_the_assembly_loads()
    {
        Assert.Equal([ChangeTopics.Gates, ChangeTopics.Help, ChangeTopics.Plugins], HomeTopics.Answered.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Ask_Daoris_answers_its_proposals_folder_as_the_one_place_of_the_help_topic()
    {
        var places = HomeTopics.Of(ChangeTopics.Help, _home);

        var place = Assert.Single(places);
        Assert.Equal(Path.GetFullPath(HelpProposals.FolderOf(_home)), place.Folder);
        Assert.False(place.Subfolders);
    }

    [Fact]
    public void The_plugins_answer_is_their_folder_heard_for_a_plugin_arriving_or_leaving_and_its_manifest_never_the_data_folder_nor_a_dotted_one()
    {
        var place = Assert.Single(HomeTopics.Of(ChangeTopics.Plugins, _home));

        Assert.Equal(Path.GetFullPath(DaorisHome.Entry(_home, HomeEntry.Plugins)), place.Folder);
        Assert.True(place.Subfolders);
        Assert.True(place.Hears("one", ChangeKind.Created));
        Assert.True(place.Hears("one/plugin.json", ChangeKind.Changed));
        Assert.False(place.Hears("one/other.json", ChangeKind.Changed));
        Assert.False(place.Hears(PluginCatalog.DataFolder + "/one/state.json", ChangeKind.Changed));
        Assert.False(place.Hears(".hidden", ChangeKind.Created));
        Assert.Empty(place.Stamps());
    }

    [Fact]
    public void The_workflows_answer_is_the_opinion_gates_records_and_the_workflows_with_the_runs_a_gate_read_listed_by_name()
    {
        var places = HomeTopics.Of(ChangeTopics.Gates, _home);

        Assert.Equal(2, places.Length);
        Assert.Equal(2, HomeWatch.GateParts(_home).Length);
        Assert.Equal(Path.GetFullPath(DaorisHome.Entry(_home, HomeEntry.OpinionGates)), places[0].Folder);
        Assert.False(places[0].Subfolders);
        Assert.Null(places[0].ByName);
        Assert.Equal(Path.GetFullPath(WorkflowStore.FolderOf(_home)), places[1].Folder);
        Assert.True(places[1].Subfolders);
        Assert.NotNull(places[1].ByName);
    }

    [Fact]
    public void A_topic_nobody_answers_throws_where_a_watch_asks_for_it()
    {
        var error = Assert.Throws<InvalidOperationException>(() => HomeTopics.Of("nobody-answers-this", _home));

        Assert.Contains("`nobody-answers-this`", error.Message, StringComparison.Ordinal);
    }
}
