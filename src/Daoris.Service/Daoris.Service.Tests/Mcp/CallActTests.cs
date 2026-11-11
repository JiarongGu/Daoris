using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// Each call's line in the session's lineage carries the act its door words it as, from the one classification the door's place reads,
/// so the second check of Ask Daoris's acts selects a press of the connector and passes over a search.
/// </summary>
public sealed class CallActTests
{
    [Fact]
    public void A_call_s_act_is_its_tool_s_class_as_the_door_words_it_whatever_it_names()
    {
        Assert.Equal("search", CallPlace.ActOf("knowledge_search"));
        Assert.Equal("read", CallPlace.ActOf("checkout_read"));
        Assert.Equal("propose", CallPlace.ActOf("handover_propose"));
        Assert.Equal("press", CallPlace.ActOf("quest_publish"));
        Assert.Null(CallPlace.ActOf("no_such_tool"));
        Assert.Null(CallPlace.ActOf(null));
    }

    [Fact]
    public void The_lineage_line_keeps_the_act_and_names_none_where_it_has_none()
    {
        var at = DateTimeOffset.UnixEpoch;

        Assert.Contains("\"act\":\"press\"", LineageFile.Line("quest_publish", new CallLineage(CallLineage.Claude) { ToolUse = "toolu_1" }, at, act: "press"), StringComparison.Ordinal);
        Assert.DoesNotContain("\"act\"", LineageFile.Line("quest_publish", null, at), StringComparison.Ordinal);
    }
}
