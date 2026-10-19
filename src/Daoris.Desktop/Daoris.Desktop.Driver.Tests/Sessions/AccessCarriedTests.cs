using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTONOMY1c (D184 points 3 and 5): the setup a session started under is read back off its record, and a session going on in its
/// own conversation, which is not handed its instruction again, is told where its go-aheads have changed since. A carry-on is
/// composed afresh from the setup as it stands, so it needs nothing more; a resumed conversation would otherwise keep the go-aheads
/// it was first told, and a person who set them back to <c>named</c> would find a session that still asks none.
/// </summary>
public sealed class AccessCarriedTests
{
    private const string Record =
        """
        [{"id":"s1","tree":"C:/trees/s1","state":"awaiting-person","repository":"dashboards",
          "access":{"permissions":"auto","routing":"person","landing":"person","goAheads":"never","questions":"claude-code"}},
         {"id":"s2","tree":"C:/trees/s2","state":"awaiting-person","repository":"dashboards"}]
        """;

    private static ResolvedAccess GoAheads(string value) => AccessRules.ResolveLevels(
        new AccessSetting { Pauses = new Dictionary<string, string>(StringComparer.Ordinal) { [AccessRules.GoAheads] = value } }, null, null);

    [Fact]
    public void A_record_keeps_the_setup_it_started_under_and_one_from_before_reads_none()
    {
        var kept = ServiceClient.ReadRecord(Record, "s1")!;

        Assert.Equal(AccessRules.Never, kept.Access![AccessRules.GoAheads]);
        Assert.Equal(AccessRules.Auto, kept.Access[AccessRules.Permissions]);
        Assert.Null(ServiceClient.ReadRecord(Record, "s2")!.Access);
    }

    [Fact]
    public void A_resumed_conversation_is_told_where_its_go_aheads_changed_and_nothing_where_they_did_not()
    {
        var wasNever = ServiceClient.ReadRecord(Record, "s1")!;
        var fromBefore = ServiceClient.ReadRecord(Record, "s2")!;

        var askAgain = Continuations.GoAheadsChanged(wasNever, GoAheads(AccessRules.Named));
        Assert.NotNull(askAgain);
        Assert.Contains("ask the person a go-ahead again before an act outside this repository or on a production system", askAgain);

        var askNone = Continuations.GoAheadsChanged(fromBefore, AccessRules.ResolveLevels(AccessSetting.FullAccess, null, null));
        Assert.NotNull(askNone);
        Assert.Contains("now asks no go-ahead but what the quest or the person's words demand", askNone);

        Assert.Null(Continuations.GoAheadsChanged(wasNever, GoAheads(AccessRules.Never)));
        // A record from before the setup was told today's go-aheads, so today's says nothing.
        Assert.Null(Continuations.GoAheadsChanged(fromBefore, AccessRules.ResolveLevels(null, null, null)));
    }
}
