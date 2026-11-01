using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SETUPUX1a (D221; the setup UX design §2.5): a default's delivery carries its own names and purpose, the quick set-up's table reads them, a
/// workflow's name ends in <i>workflow</i>, and no workflow's, delivery's or renamed step's name contains another's whole name.
/// </summary>
public sealed class ShippedDeliveryWordsTests
{
    private static readonly string[] Offered = ["merge-after-accept", "branch-to-push", "pull-request", "integrated"];

    private static IEnumerable<string> Names(Func<ShippedWords, string> language) =>
        WorkflowDefaults.All.Select(each => language(each.Names))
            .Concat(WorkflowDefaults.All.Select(each => language(each.DeliveryWords.Names)))
            .Append(language(new ShippedWords("Not driven", "未驱动")))
            .Concat(RenamedSteps.Select(language))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>The steps §2.5 renames, whose words the page's rows n and w write.</summary>
    private static readonly ShippedWords[] RenamedSteps =
    [
        new("Completed on the host", "在托管平台完成"), new("The integration request", "集成请求"), new("Handed on", "已移交"),
    ];

    [Fact]
    public void The_deliveries_the_set_up_offers_keep_their_names_and_purposes_in_the_delivery()
    {
        Assert.Equal(
            [
                ("merge-after-accept", "Merge after you accept", "你采纳后合并"),
                ("branch-to-push", "A branch to push", "留给你推送的分支"),
                ("pull-request", "A pull request", "拉取请求"),
                ("integrated", "Integrated into the line", "集成到主线"),
            ],
            Offered.Select(id => (id, WorkflowDefaults.Of(id)!.DeliveryWords.Names.En, WorkflowDefaults.Of(id)!.DeliveryWords.Names.Zh)));
        Assert.StartsWith("For a repository whose work you accept", WorkflowDefaults.Of("merge-after-accept")!.DeliveryWords.For.En);
        Assert.All(WorkflowDefaults.All, each => Assert.False(
            string.IsNullOrWhiteSpace(each.DeliveryWords.Names.En) || string.IsNullOrWhiteSpace(each.DeliveryWords.Names.Zh)
            || string.IsNullOrWhiteSpace(each.DeliveryWords.For.En) || string.IsNullOrWhiteSpace(each.DeliveryWords.For.Zh), each.Id));
    }

    [Fact]
    public void The_quick_set_ups_table_reads_the_delivery_names_and_no_workflow_s()
    {
        Assert.Equal(
            Offered.Select(id => WorkflowDefaults.Of(id)!.DeliveryWords.Names.En),
            SetupPresets.Table.Skip(1).Select(preset => preset.Name));
        Assert.All(Offered, id => Assert.NotEqual(WorkflowDefaults.Of(id)!.Names.En, SetupPresets.Of(id)!.Name));
    }

    [Fact]
    public void A_workflow_s_name_ends_in_workflow_in_both_languages()
    {
        Assert.All(WorkflowDefaults.All, each =>
        {
            Assert.EndsWith(" workflow", each.Names.En, StringComparison.Ordinal);
            Assert.EndsWith("工作流", each.Names.Zh, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void No_name_of_a_workflow_a_delivery_or_a_renamed_step_contains_another_s(string language)
    {
        var names = Names(language == "en" ? words => words.En : words => words.Zh).ToList();

        var contained = names.SelectMany(outer => names.Where(inner => outer != inner && outer.Contains(inner, StringComparison.OrdinalIgnoreCase))
            .Select(inner => $"`{outer}` contains `{inner}`")).ToList();

        Assert.Empty(contained);
    }
}
