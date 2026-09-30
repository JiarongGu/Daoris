namespace Daoris.Service.Tests;

/// <summary>The <c>delete</c> kind's writer (HELP6): a quest or an ask made by mistake, by its id — the drawer's and the record's Delete.</summary>
public sealed class HelpDeleteProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_delete_is_written_with_what_it_would_delete()
    {
        var (quest, _) = Box().ProposeDelete("#q1a2b3c4", null, "a duplicate of #q9", "h1", Now);
        var (ask, _) = Box().ProposeDelete(null, "a5b6c7d8", "made by mistake", "h1", Now);

        var one = Written(quest!);
        Assert.Equal(("delete", "quest", "q1a2b3c4"), (one.GetProperty("kind").GetString(), one.GetProperty("door").GetString(), one.GetProperty("target").GetString()));
        var other = Written(ask!);
        Assert.Equal(("delete", "ask", "a5b6c7d8"), (other.GetProperty("kind").GetString(), other.GetProperty("door").GetString(), other.GetProperty("target").GetString()));
    }

    /// <summary>The shape, checked here and nothing more; whether the record may go is the driver's to ask.</summary>
    [Theory]
    [InlineData("||", "a quest or an ask — name exactly one")]
    [InlineData("q1|a1|", "a quest or an ask — name exactly one")]
    public void A_malformed_delete_proposal_is_refused_with_nothing_written(string fields, string says)
    {
        var part = fields.Split('|').Select(field => field.Length == 0 ? null : field).ToArray();

        var (id, message) = Box().ProposeDelete(part[0], part[1], "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
