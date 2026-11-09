using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The views and kinds census (the design's §23, §33): every kind's parse and view's computation the driver declares reads its
/// contents alone. Shown red on a view whose helper reads the person's layer, a kind whose parse fills staffing through its port, a parse
/// that lowers and orders by the ambient culture, and a view asking its contents for a key its record does not hold.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DeclarationViewsCensusTests
{
    private static readonly System.Reflection.Assembly Driver = typeof(RepositoryDeclarations).Assembly;
    private static readonly System.Reflection.Assembly Tests = typeof(DeclarationViewsCensusTests).Assembly;

    [Fact]
    public void Every_kind_and_view_the_driver_declares_reads_its_contents_alone()
    {
        var problems = DeclarationViews.Problems(DeclarationParses.Declared(Driver), Driver);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void A_view_whose_helper_reads_the_persons_layer_turns_the_census_red()
    {
        Assert.Contains("PersonView.View reaches PersonFiles.Read: a home layer's reader, in PlantedReads.Person",
            DeclarationViews.Problems([typeof(PersonView)], Driver, Tests));
    }

    [Fact]
    public void A_kind_whose_parse_fills_staffing_through_its_port_turns_the_census_red()
    {
        Assert.Contains("StaffingKind.Parse reaches RoleStaffingPort.Fill: a port, in StaffingKind.Parse", DeclarationViews.Problems([typeof(StaffingKind)], Driver, Tests));
    }

    [Fact]
    public void A_parse_that_lowers_orders_and_interpolates_by_the_ambient_culture_turns_the_census_red()
    {
        var problems = DeclarationViews.Problems([typeof(AmbientKind)], Driver, Tests);

        Assert.Contains("AmbientKind.Parse reaches String.ToLower: leaves CultureInfo to the ambient culture, in AmbientKind.Parse", problems);
        Assert.Contains("AmbientKind.Parse reaches Enumerable.Order: leaves IComparer`1 to the ambient culture, in AmbientKind.Parse", problems);
        Assert.Contains("AmbientKind.Parse formats Int32 in an interpolation by the ambient culture, in AmbientKind.Parse", problems);
    }

    [Fact]
    public void A_view_asking_its_contents_for_a_key_its_record_does_not_hold_reads_nothing_and_throws()
    {
        var contents = new DeclarationContents([DeclarationBytes.Of([1], null)]);

        Assert.Throws<InvalidOperationException>(() => contents.Parse(ContentKey.Of([2]), RuleFileKind.One));
    }

    private sealed class PersonView : IDeclarationView<string, string>
    {
        public string View(string key, DeclarationContents contents) => PlantedReads.Person(key);

        public long Weigh(string value) => 0;
    }

    private sealed class StaffingKind : IDeclarationKind<string>
    {
        public string Parse(DeclarationBytes content) => RoleStaffingPort.Fill(null!, content.Text()).ToString();

        public bool Problem(string parsed) => false;

        public long Weigh(string parsed) => 0;
    }

    private sealed class AmbientKind : IDeclarationKind<string>
    {
        public string Parse(DeclarationBytes content) => $"{content.Size}:" + string.Join(',', content.Text().ToLower().Split(',').Order());

        public bool Problem(string parsed) => false;

        public long Weigh(string parsed) => 0;
    }

    private static class PlantedReads
    {
        internal static string Person(string home) => PersonFiles.Read(home).ToString()!;
    }
}
