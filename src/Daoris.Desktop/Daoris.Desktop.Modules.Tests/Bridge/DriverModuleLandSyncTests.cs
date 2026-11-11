using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The <c>LANDING</c> plan carries what the landing leaves out because the line holds it, and a conflict with the words the review
/// offers to say to the session; each is absent where the line holds none of the tree's commits. Over real git: the driver's <c>LineHoldsLandingTests</c>.
/// </summary>
public sealed class DriverModuleLandSyncTests
{
    [Fact]
    public void A_left_out_commit_goes_on_the_wire_by_its_short_id_subject_why_and_the_lines_commit()
    {
        var wire = TreesRoutes.LeftOut([new LeftOutCommit(new string('a', 40), "the fix", LeftOutWhy.Patch, new string('b', 40))]);

        Assert.Equal("""[{"commit":"aaaaaaaa","subject":"the fix","why":"patch","on":"bbbbbbbb"}]""", JsonSerializer.Serialize(wire, JsonSerializerOptions.Web));
    }

    [Fact]
    public void A_conflict_goes_on_the_wire_with_gits_lines_and_the_rebase_words()
    {
        var wire = TreesRoutes.Conflict(new LandingConflict("CONFLICT (content): Merge conflict in a.md.", "Rebase your work onto `main`."));

        var read = JsonDocument.Parse(JsonSerializer.Serialize(wire, JsonSerializerOptions.Web)).RootElement;
        Assert.Equal(["says", "words"], read.EnumerateObject().Select(field => field.Name));
        Assert.Equal("CONFLICT (content): Merge conflict in a.md.", read.GetProperty("says").GetString());
        Assert.Equal("Rebase your work onto `main`.", read.GetProperty("words").GetString());
    }

    [Fact]
    public void Where_the_line_holds_none_nothing_is_added()
    {
        Assert.Null(TreesRoutes.LeftOut(null));
        Assert.Null(TreesRoutes.Conflict(null));
    }
}
