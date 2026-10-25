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
        Assert.Equal([ChangeTopics.Help], HomeTopics.Answered.Keys.Order(StringComparer.Ordinal));
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
    public void A_topic_nobody_answers_throws_where_a_watch_asks_for_it()
    {
        var error = Assert.Throws<InvalidOperationException>(() => HomeTopics.Of("nobody-answers-this", _home));

        Assert.Contains("`nobody-answers-this`", error.Message, StringComparison.Ordinal);
    }
}
