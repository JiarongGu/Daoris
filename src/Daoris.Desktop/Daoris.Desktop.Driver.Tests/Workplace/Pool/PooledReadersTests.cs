using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The readers that meet a gone tree, each asked of a record whose line's slot another line took: it answers <i>can be brought back</i>, and the
/// legacy tree, or a home with no pool, answers as before.
/// </summary>
public sealed class PooledReadersTests : IDisposable
{
    private static readonly HarnessConversation Kept = new("claude-code-acp", "0b5e7c1a");

    private readonly PooledLines _lines = new();

    public void Dispose() => _lines.Dispose();

    private PriorSession Park(string tree) =>
        new("s1", tree, "awaiting-person", "It stopped to ask you:\n\nWhich port?", "engine")
        {
            Said = PersonWords.Of("Port 8080."),
            Profile = "account-1",
            Adapter = "claude-code-acp",
        };

    private string Taken()
    {
        _lines.Slot(1, "s-aaaa");
        _lines.LetGo(1, "s-aaaa");
        _lines.Take(1, "s-bbbb");
        return _lines.Tree("s-aaaa");
    }

    private ContinueReason? Judged(string tree, bool withHome = true) =>
        Continuations.Judge(Park(tree), "claude-code-acp", doorResumes: true, profile: "account-1", Kept, home: withHome ? _lines.Home : null);

    // ——— Words going on, and the conversation's go-on, briefed and limit moves: stands, or can be had.

    [Fact]
    public void Words_going_on_in_a_line_whose_slot_was_taken_resume_where_a_gone_line_falls_back()
    {
        var taken = Taken();
        Directory.CreateDirectory(_lines.Tree("s-legacy"));

        Assert.Null(Judged(taken));
        Assert.Equal(ContinueWhy.Tree, Judged(_lines.Tree("s-never"))?.Code);
        Assert.Null(Judged(_lines.Tree("s-legacy")));
        Assert.Null(Judged(_lines.Tree("s-legacy"), withHome: false));
        Assert.Equal(ContinueWhy.Tree, Judged(taken, withHome: false)?.Code);
    }

    [Fact]
    public void A_conversation_s_tree_stands_when_a_slot_can_be_brought_back_to_its_line()
    {
        var taken = Taken();

        Assert.True(ConversationTrees.Stands(_lines.Home, taken));
        Assert.False(ConversationTrees.Stands(_lines.Home, _lines.Tree("s-never")));
        Assert.False(ConversationTrees.Stands(null, taken));
    }

    // ——— The review served: a slot that took its present line after the showing is not the set-up's build.

    [Fact]
    public void Show_it_again_is_refused_where_the_slot_took_its_line_after_the_showing()
    {
        _lines.Slot(1, "s-aaaa");
        var tree = _lines.Tree("s-aaaa");
        var now = DateTimeOffset.UtcNow;

        Assert.False(ReviewServed.TreeStands(_lines.Home, tree, now.AddHours(-1)));
        Assert.True(ReviewServed.TreeStands(_lines.Home, tree, now.AddHours(1)));
        Assert.True(ReviewServed.TreeStands(_lines.Home, tree, null));
        _lines.TookAt(1, now.AddDays(-2));
        Assert.True(ReviewServed.TreeStands(_lines.Home, tree, now.AddHours(-1)));
    }

    [Fact]
    public void A_legacy_tree_and_a_taken_line_answer_the_disk_s_in_show_it_again()
    {
        Directory.CreateDirectory(_lines.Tree("s-legacy"));
        var taken = Taken();

        Assert.True(ReviewServed.TreeStands(_lines.Home, _lines.Tree("s-legacy"), DateTimeOffset.UtcNow.AddDays(1)));
        Assert.True(ReviewServed.TreeStands(null, _lines.Tree("s-legacy"), null));
        Assert.False(ReviewServed.TreeStands(_lines.Home, taken, null));
    }

    // ——— A removal's judgement: when a slot opened is the take.

    [Fact]
    public void A_slot_opened_when_its_present_line_took_it_and_a_fresh_tree_when_its_folder_was_made()
    {
        var slot = _lines.Slot(1, "s-aaaa");
        var folder = Directory.CreateDirectory(_lines.Tree("s-legacy")).FullName;
        Directory.SetCreationTimeUtc(folder, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));

        Assert.Equal(slot.Since.UtcDateTime, ContentHolds.OpenedAt(slot.Path, slot));
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), ContentHolds.OpenedAt(folder, null));
    }

    // ——— The agent's home: a slot's project folder is the slot's.

    private static string Records(string tree) => JsonSerializer.Serialize(new object[] { new { id = "a", state = "completed", tree } });

    [Fact]
    public void A_pooled_line_s_record_claims_the_project_folder_of_each_slot_it_worked_in()
    {
        _lines.Slot(1, "s-aaaa");
        _lines.LetGo(1, "s-aaaa");
        _lines.Slot(2, "s-aaaa");
        var tree = _lines.Tree("s-aaaa");

        var origins = ToolHomeLeftovers.Origins(_lines.Home, Records(tree))!;

        Assert.Equal([tree, _lines.SlotFolder(2), _lines.SlotFolder(1)], origins.Select(each => each.Folder));
        Assert.All(origins, each => Assert.Equal("tree", each.Source));
    }

    [Fact]
    public void A_legacy_record_claims_its_own_tree_alone()
    {
        _lines.Slot(1, "s-aaaa");
        var tree = _lines.Tree("s-legacy");

        var origins = ToolHomeLeftovers.Origins(_lines.Home, Records(tree))!;

        Assert.Equal([tree], origins.Select(each => each.Folder));
    }
}
