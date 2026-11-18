using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each place's risk and the owner's marks are one record in <c>driver.json</c>, read narrowest first with destination marks
/// covering every place under them; the home's migration writes today's implicit person-holds as line marks, moving no routing.
/// </summary>
public sealed class PlaceRiskTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "place-risk", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private static PlaceRiskBook Book(string json) => PlaceRiskBook.Read(JsonDocument.Parse(json).RootElement);

    [Fact]
    public void A_home_with_no_record_reads_unwritten_and_every_place_low()
    {
        var book = Book("{}");
        Assert.False(book.Written);
        Assert.Equal((PlaceRisks.Low, PlaceRiskBook.DefaultSource), (book.At(new RolePlace("work", "app")).Risk, book.At(new RolePlace("work", "app")).Source));
    }

    [Fact]
    public void The_narrowest_risk_stands_and_a_workspace_mark_covers_its_repositories()
    {
        var book = Book("""
            { "placeRisk": {
              "machine": { "risk": "low", "marks": ["line"] },
              "workspace/work": { "risk": "low", "marks": ["release.production"] },
              "repository/app": { "risk": "medium", "marks": [] } } }
            """);
        var app = book.At(new RolePlace("work", "app"));
        Assert.Equal((PlaceRisks.Medium, PlaceRiskBook.OwnSource), (app.Risk, app.Source));
        Assert.True(app.Marked(PlaceMarks.ProductionReleases));
        Assert.False(app.RoleMayHold(PlaceMarks.ProductionReleases));
        Assert.True(app.RoleMayHold());
        // A line's mark is the level's own, never inherited.
        Assert.DoesNotContain(PlaceMarks.Line, app.Marks);
        Assert.True(book.OwnerHoldsLine(RoleScope.Everywhere, SupervisorKinds.Vacancy));
        Assert.False(book.OwnerHoldsLine(new RoleScope(RoleScope.Workspace, "work"), SupervisorKinds.Vacancy));
        Assert.Equal(PlaceRiskBook.WorkspaceSource, book.At(new RolePlace("work", "other")).Source);
    }

    [Fact]
    public void A_risk_this_build_does_not_know_reads_high_so_no_role_acts_there()
    {
        var book = Book("""{ "placeRisk": { "workspace/work": { "risk": "critical" } } }""");
        Assert.False(book.At(new RolePlace("work")).RoleMayHold());
    }

    [Fact]
    public void An_edit_sets_risk_and_marks_and_a_low_unmarked_place_loses_its_entry()
    {
        var book = PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Mark = [PlaceMarks.ProductionReleases] });
        Assert.True(book.Written);
        Assert.Equal([PlaceMarks.ProductionReleases], book.Places["workspace/work"].Marks);
        var high = book.Apply(new PlaceRiskEdit { Workspace = "work", Repository = "app", Risk = PlaceRisks.High });
        Assert.Equal(PlaceRisks.High, high.Places["repository/app"].Risk);
        var cleared = high.Apply(new PlaceRiskEdit { Workspace = "work", Unmark = [PlaceMarks.ProductionReleases] });
        Assert.False(cleared.Places.ContainsKey("workspace/work"));
        Assert.Throws<DriverException>(() => book.Apply(new PlaceRiskEdit { Risk = "severe" }));
        Assert.Throws<DriverException>(() => book.Apply(new PlaceRiskEdit { Mark = ["lead"] }));
    }

    [Fact]
    public void The_record_round_trips_through_the_config_and_an_empty_one_stays_written()
    {
        var config = DriverConfig.Empty with { PlaceRisk = PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Mark = [PlaceMarks.Line] }) };
        var read = Book(DriverConfigWriter.Written(config));
        Assert.True(read.OwnerHoldsLine(RoleScope.Everywhere, SupervisorKinds.Question));
        var empty = Book(DriverConfigWriter.Written(DriverConfig.Empty with { PlaceRisk = new PlaceRiskBook(new Dictionary<string, PlaceRisk>()) }));
        Assert.True(empty.Written);
    }
}
