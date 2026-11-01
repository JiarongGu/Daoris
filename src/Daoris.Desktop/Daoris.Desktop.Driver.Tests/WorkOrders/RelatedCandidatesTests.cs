using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>SESSRELATED1: only a session whose tree folder exists here is a candidate; a gone tree has no work to meet.</summary>
public sealed class RelatedCandidatesTests
{
    private static SessionView Session(string id, string tree, string repository = "Daoris") =>
        new(id, repository) { Tree = tree, BaseCommit = "abc" };

    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    [Fact]
    public void A_session_whose_tree_is_gone_is_left_out_and_the_others_stay()
    {
        var all = new[] { Session("me", "t/me"), Session("here", "t/here"), Session("gone", "t/gone"), Session("else", "t/else", "Other") };

        var (mine, others) = RelatedCandidates.Pick(all, None, _ => [], "me", tree => tree != "t/gone")!.Value;

        Assert.Equal("me", mine.Id);
        Assert.Equal(["me", "here"], others.Select(other => other.Id));
    }

    [Fact]
    public void The_asking_session_with_its_tree_gone_has_no_related_sessions_at_all()
    {
        var all = new[] { Session("me", "t/me"), Session("here", "t/here") };

        Assert.Null(RelatedCandidates.Pick(all, None, _ => [], "me", tree => tree != "t/me"));
    }

    [Fact]
    public void The_tree_is_not_asked_of_a_session_of_another_repository_or_without_a_base()
    {
        var asked = new List<string>();
        var all = new[] { Session("me", "t/me"), Session("else", "t/else", "Other"), new SessionView("bare", "Daoris") { Tree = "t/bare" } };

        RelatedCandidates.Pick(all, None, _ => [], "me", tree => { asked.Add(tree); return true; });

        Assert.Equal(["t/me"], asked.Distinct());
    }
}
