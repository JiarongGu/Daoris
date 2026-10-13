using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TRACE1b (D143, D50): the chain a trace reads, as data, which both doors word. What may reach a page is held here, beside
/// the route's own test (<c>DriverModuleTraceTests</c>): a tree by its folder's name, and neither its path nor what a failed
/// read said, which may name a file under the home (D47 §4).
/// </summary>
public sealed class TraceChainTests
{
    [Theory]
    [InlineData("C:/trees/dashboards-q1", "dashboards-q1")]
    [InlineData(@"C:\trees\dashboards-q1\", "dashboards-q1")]
    [InlineData("/srv/data/trees/reports-q5", "reports-q5")]
    [InlineData("reports-q5", "reports-q5")]
    [InlineData("/", null)]
    [InlineData(null, null)]
    public void A_tree_is_named_by_its_folder_and_never_its_path(string? tree, string? named) =>
        Assert.Equal(named, TraceChains.FolderOf(tree));

    [Fact]
    public void What_a_read_said_and_a_trees_path_stay_off_the_wire()
    {
        var chain = new TraceChain(TraceEntry.Session, "s1")
        {
            Unread = [new TraceUnread(TraceStores.Landings) { Problem = @"C:\home\landings.json is not JSON" }],
            Links =
            [
                new TraceLink(TraceLink.SessionKind)
                {
                    Session = new TraceSessionLink("s1")
                    {
                        Tree = "dashboards-q1",
                        TreePath = "C:/trees/dashboards-q1",
                        Events = new TraceEvents { Missing = TraceEventGaps.Unread, Problem = @"C:\home\sessions\s1.events.jsonl is locked" },
                        Rules = new TraceRules { Missing = TraceRuleGaps.Unread, Problem = @"C:\home\spawn\s1.settings.json" },
                    },
                },
                new TraceLink(TraceLink.AskKind) { Ask = new TraceAskLink("a1") { Missing = TraceLinkGaps.Unread, Problem = "refused at C:/socket" } },
            ],
        };

        var wire = JsonSerializer.Serialize(chain, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"tree\":\"dashboards-q1\"", wire);
        Assert.DoesNotContain("C:", wire);
        Assert.DoesNotContain("problem", wire);
        Assert.DoesNotContain("treePath", wire);
        Assert.Contains("\"source\":\"events\"", wire);
    }
}
