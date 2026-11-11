using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A default's delivery carries an id, a name and a purpose in English, which are the terminal's
/// words and the page's fallback (the page words them from its catalogues by the ids), the quick set-up's table reads them, a workflow's name
/// ends in <i>workflow</i>, and no workflow's, delivery's or renamed step's name contains the whole name of another kind's. The shipped data
/// holds no per-language field: the Chinese names are the web's catalogue, held by its own census and by the glossary's <c>distinct</c> lists.
/// </summary>
public sealed class ShippedDeliveryWordsTests
{
    private static readonly string[] Offered = ["merge-after-accept", "branch-to-push", "pull-request", "integrated", "in-place"];

    /// <summary>The steps §2.5 renames, whose words the page's rows n and w write.</summary>
    private static readonly string[] RenamedSteps = ["Completed on the host", "The integration request", "Handed on"];

    [Fact]
    public void The_deliveries_the_set_up_offers_keep_their_names_and_carry_their_ids()
    {
        Assert.Equal(
            [
                ("merge-after-accept", "Merge after you accept"),
                ("branch-to-push", "A branch to push"),
                ("pull-request", "A pull request"),
                ("integrated", "Integrated into the line"),
                ("in-place", "Work in place"),
            ],
            Offered.Select(id => (WorkflowDefaults.Of(id)!.DeliveryWords.Id, WorkflowDefaults.Of(id)!.DeliveryWords.Name)));
        Assert.StartsWith("For a repository whose work you accept", WorkflowDefaults.Of("merge-after-accept")!.DeliveryWords.For);
        Assert.All(WorkflowDefaults.All, each => Assert.False(
            string.IsNullOrWhiteSpace(each.DeliveryWords.Id) || string.IsNullOrWhiteSpace(each.DeliveryWords.Name)
            || string.IsNullOrWhiteSpace(each.DeliveryWords.For) || string.IsNullOrWhiteSpace(each.Name) || string.IsNullOrWhiteSpace(each.For), each.Id));
    }

    [Fact]
    public void A_delivery_has_one_id_and_one_name_wherever_the_data_says_it()
    {
        var named = WorkflowDefaults.All.Select(each => each.DeliveryWords).GroupBy(each => each.Id).ToList();

        Assert.Equal(
            ["merge-after-accept", "merge-automatic", "branch-to-push", "pull-request", "integrated", "integrated-no-press", "in-place"],
            named.Select(group => group.Key));
        Assert.All(named, group => Assert.Single(group.Select(each => each.Name).Distinct()));
        Assert.Equal(named.Count, named.Select(group => group.First().Name).Distinct().Count());
    }

    [Fact]
    public void The_quick_set_ups_table_reads_the_delivery_names_and_no_workflow_s()
    {
        Assert.Equal(
            Offered.Select(id => WorkflowDefaults.Of(id)!.DeliveryWords.Name),
            SetupPresets.Table.Skip(1).Select(preset => preset.Name));
        Assert.All(Offered, id => Assert.NotEqual(WorkflowDefaults.Of(id)!.Name, SetupPresets.Of(id)!.Name));
    }

    [Fact]
    public void A_workflow_s_name_ends_in_workflow()
    {
        Assert.All(WorkflowDefaults.All, each => Assert.EndsWith(" workflow", each.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void No_name_of_a_workflow_a_delivery_or_a_renamed_step_contains_the_whole_name_of_another_kind_s()
    {
        // A name inside its own kind may hold another's (the glossary's `distinct` says so); across kinds it may not.
        var kinds = new (string Kind, string[] Names)[]
        {
            ("workflow", [.. WorkflowDefaults.All.Select(each => each.Name)]),
            ("delivery", [.. WorkflowDefaults.All.Select(each => each.DeliveryWords.Name).Append("Not driven").Distinct()]),
            ("step", RenamedSteps),
        };

        var contained = kinds.SelectMany(outer => outer.Names.SelectMany(name => kinds.Where(other => other.Kind != outer.Kind)
            .SelectMany(other => other.Names.Where(inner => name.Contains(inner, StringComparison.OrdinalIgnoreCase))
                .Select(inner => $"{outer.Kind} `{name}` contains {other.Kind} `{inner}`")))).ToList();

        Assert.Empty(contained);
    }
}
