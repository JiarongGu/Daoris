using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// LANDSYNC1 (D87's 2026-10-15 amendment point 4): the <c>LANDING</c> plan and the <c>LAND_SESSION_TREE</c> press carry what the
/// landing leaves out because the line holds it, beside the answer's fields as they were, and a conflict with the words the review
/// offers to say to the session. Each is absent where the line holds none of the tree's commits, so the answer reads as before.
/// What the landing leaves out over real git is held by the driver's <c>LineHoldsLandingTests</c>.
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

    /// <summary>
    /// FABRIC-CLOSE1h9 (D173's FABRIC-CLOSE1h3 note, decision 4): a press whose push was held back lands with no plugin told, and says
    /// why beside its sentence, so the page tells it from an ordinary landing.
    /// </summary>
    [Fact]
    public void A_press_whose_push_was_held_back_answers_why_beside_a_landing_with_no_plugin()
    {
        const string why = "Its quest was taken back before its plugin was told, so its push was not tried";
        var landed = new TreeLanding(true, $"put its work on `mine/x`. {why}.", "mine/x") { Withheld = why };

        var read = Read(TreesRoutes.Pressed("s1a2b3c4", landed, LandingGate.Open("engine"), TreesWords.Land(landed, []), []));

        Assert.True(read.GetProperty("done").GetBoolean());
        Assert.Equal(JsonValueKind.Null, read.GetProperty("plugin").ValueKind);
        Assert.Equal(why, read.GetProperty("withheld").GetString());
        Assert.Equal(1, read.GetProperty("said").GetProperty("code").GetInt32());
    }

    [Fact]
    public void A_press_whose_push_went_on_answers_no_withheld_and_its_fields_as_before()
    {
        var landed = new TreeLanding(true, "merged into `main`.", null);

        var read = Read(TreesRoutes.Pressed("s1a2b3c4", landed, LandingGate.Open("engine"), TreesWords.Land(landed, []), []));

        Assert.Equal(JsonValueKind.Null, read.GetProperty("withheld").ValueKind);
        // CHATFLOW1d: `chat` is a chat run's press's own answer, added, and null on every other press.
        Assert.Equal(JsonValueKind.Null, read.GetProperty("chat").ValueKind);
        Assert.Equal(
            ["session", "done", "message", "branch", "plugin", "withheld", "review", "opinion", "workflow", "leftOut", "conflict", "said", "followed", "chat"],
            read.EnumerateObject().Select(field => field.Name));
    }

    private static JsonElement Read(object answer) =>
        JsonDocument.Parse(JsonSerializer.Serialize(answer, JsonSerializerOptions.Web)).RootElement;

    [Fact]
    public void Where_the_line_holds_none_nothing_is_added()
    {
        Assert.Null(TreesRoutes.LeftOut(null));
        Assert.Null(TreesRoutes.Conflict(null));
    }
}
