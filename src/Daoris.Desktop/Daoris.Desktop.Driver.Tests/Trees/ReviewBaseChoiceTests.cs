using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWBASE1 (D164's note): which commit a session tree's review is measured from, chosen from git's answer. The candidates are
/// the newest commit the tree shares with the line here, with the line on origin, and the commit the record says it began at, in
/// that order; git names those no other candidate reaches (<c>merge-base --independent</c>), and the first it names is the base.
/// Pure: the git reads that feed it are <c>ReviewBaseTests</c>', in real repositories.
/// </summary>
public sealed class ReviewBaseChoiceTests
{
    private const string Line = "1111111111111111111111111111111111111111";
    private const string Origin = "2222222222222222222222222222222222222222";
    private const string Record = "3333333333333333333333333333333333333333";

    /// <summary>The owner's case: the tree took in the line after its record's base, so the line's commit descends from the record's.</summary>
    [Fact]
    public void The_candidate_that_descends_from_the_others_is_the_base()
    {
        Assert.Equal(Origin, ReviewBase.Newest([Line, Origin, Record], [Origin]));
        Assert.Equal(Line, ReviewBase.Newest([Line, Origin, Record], [Line]));
    }

    /// <summary>A chain's step grew from its parent's tip, past the line: the record's base is the newest, as before.</summary>
    [Fact]
    public void A_record_newer_than_the_line_stays_the_base()
    {
        Assert.Equal(Record, ReviewBase.Newest([Line, Record], [Record]));
    }

    /// <summary>
    /// None descends from the others (the record's base is a parent's tip, and the tree merged the line since): the line's is taken,
    /// so the line's own changes are never shown as the session's.
    /// </summary>
    [Fact]
    public void Where_none_descends_from_the_others_the_lines_comes_before_the_records()
    {
        Assert.Equal(Line, ReviewBase.Newest([Line, Origin, Record], [Record, Line]));
        Assert.Equal(Origin, ReviewBase.Newest([Line, Origin, Record], [Record, Origin]));
    }

    /// <summary>A record may hold a commit by a shorter id; git answers a full one, and the record's own word is what is returned.</summary>
    [Fact]
    public void A_shorter_id_is_matched_by_the_full_one_git_names()
    {
        Assert.Equal("3333333", ReviewBase.Newest([Line, "3333333"], [Record.ToUpperInvariant()]));
    }

    /// <summary>Git naming none of them (its read failed) chooses nothing: the caller keeps the record's word, as before.</summary>
    [Fact]
    public void Nothing_named_chooses_nothing()
    {
        Assert.Null(ReviewBase.Newest([Line, Record], []));
        Assert.Null(ReviewBase.Newest([], [Line]));
    }

    /// <summary>
    /// REVIEWBASE3: a chain's step grew from its parent's tip and merged the line since, so git names both the line's and the
    /// record's: neither reaches the other, and the review reads from a merge of the two, the line's first.
    /// </summary>
    [Fact]
    public void Where_the_lines_and_the_records_are_both_named_the_two_are_merged()
    {
        Assert.Equal((Line, Record), ReviewBase.Incomparable([Line, Origin, Record], [Record, Line], Record));
        Assert.Equal((Origin, Record), ReviewBase.Incomparable([Line, Origin, Record], [Origin, Record], Record));
    }

    /// <summary>Where one of them descends from the other, or there is no record's, nothing is merged: the base alone stands.</summary>
    [Fact]
    public void Where_one_reaches_the_other_or_there_is_no_record_nothing_is_merged()
    {
        Assert.Null(ReviewBase.Incomparable([Line, Origin, Record], [Line], Record));
        Assert.Null(ReviewBase.Incomparable([Line, Record], [Record], Record));
        Assert.Null(ReviewBase.Incomparable([Line, Origin], [Line, Origin], null));
        // The record's base is the line's own commit: one candidate, nothing to merge.
        Assert.Null(ReviewBase.Incomparable([Line], [Line], Line));
        // Git named none (its read failed): nothing is chosen, so nothing is merged.
        Assert.Null(ReviewBase.Incomparable([Line, Record], [], Record));
    }

    /// <summary>A record holding a shorter id is merged by its own word, as <c>Newest</c> returns it.</summary>
    [Fact]
    public void A_shorter_record_is_merged_by_its_own_word()
    {
        Assert.Equal((Line, "3333333"), ReviewBase.Incomparable([Line, "3333333"], [Line, Record], "3333333"));
    }

    /// <summary>The candidates in their order, each once, the unread left out.</summary>
    [Fact]
    public void The_candidates_are_the_lines_then_origins_then_the_records_each_once()
    {
        Assert.Equal([Line, Origin, Record], ReviewBase.Candidates(Line, Origin, Record));
        Assert.Equal([Line, Record], ReviewBase.Candidates(Line, Line, Record));
        Assert.Equal([Origin], ReviewBase.Candidates(null, Origin, null));
        Assert.Equal([Record], ReviewBase.Candidates(null, null, Record));
        Assert.Empty(ReviewBase.Candidates(null, "", null));
    }
}
