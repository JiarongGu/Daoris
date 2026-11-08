using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where a record's tree is opened: a line in a slot is opened at the slot, a legacy tree at its own path, a line whose slot was taken
/// can be brought back, and a path the pool never heard of is gone with its folder.
/// </summary>
public sealed class LinePlacesTests : IDisposable
{
    private readonly PooledLines _lines = new();

    public void Dispose() => _lines.Dispose();

    [Fact]
    public void A_line_in_a_slot_is_opened_at_the_slot_whose_folder_no_record_names()
    {
        _lines.Slot(1, "s-aaaa");

        var where = new LinePlaces(_lines.Home).Of(_lines.Tree("s-aaaa"));

        Assert.Equal((LineStand.Stands, _lines.SlotFolder(1)), (where.Stand, where.Folder));
        Assert.Equal(1, where.Slot!.Number);
    }

    [Fact]
    public void A_line_whose_slot_was_taken_can_be_brought_back()
    {
        _lines.Slot(1, "s-aaaa");
        _lines.LetGo(1, "s-aaaa");
        _lines.Take(1, "s-bbbb");

        var where = new LinePlaces(_lines.Home).Of(_lines.Tree("s-aaaa"));

        Assert.Equal((LineStand.CanBeBroughtBack, _lines.Tree("s-aaaa")), (where.Stand, where.Folder));
        Assert.True(where.StandsOrCanBeHad);
        Assert.False(where.Stands);
    }

    [Fact]
    public void A_legacy_tree_answers_its_own_path_and_a_gone_one_answers_gone()
    {
        Directory.CreateDirectory(_lines.Tree("s-legacy"));
        _lines.Slot(1, "s-aaaa");

        var places = new LinePlaces(_lines.Home);

        Assert.Equal((LineStand.Stands, _lines.Tree("s-legacy")), (places.Of(_lines.Tree("s-legacy")).Stand, places.Of(_lines.Tree("s-legacy")).Folder));
        Assert.Equal(LineStand.Gone, places.Of(_lines.Tree("s-never")).Stand);
        Assert.False(places.Of(_lines.Tree("s-never")).StandsOrCanBeHad);
    }

    [Fact]
    public void A_home_with_no_pool_answers_the_disk_for_a_tree_and_for_the_checkout()
    {
        var checkout = Path.Combine(_lines.Home, "checkout");
        Directory.CreateDirectory(checkout);

        var places = new LinePlaces(_lines.Home);

        Assert.Equal(LineStand.Stands, places.Of(checkout).Stand);
        Assert.Equal(LineStand.Gone, places.Of(Path.Combine(_lines.Home, "nothing")).Stand);
        Assert.Equal(LineStand.Gone, places.Of(_lines.Tree("s-aaaa")).Stand);
    }

    [Fact]
    public void The_slots_a_line_worked_in_are_listed_the_present_one_first()
    {
        _lines.Slot(1, "s-aaaa");
        _lines.LetGo(1, "s-aaaa");
        _lines.Slot(2, "s-aaaa");

        var slots = new LinePlaces(_lines.Home).SlotsOf(PooledLines.Workspace, PooledLines.Repository, _lines.Tree("s-aaaa"));

        Assert.Equal([2, 1], slots.Select(each => each.Number));
    }
}
