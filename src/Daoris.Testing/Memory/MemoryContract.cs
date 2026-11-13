using Daoris.Ports.Memory;
using Xunit;

namespace Daoris.Testing;

/// <summary>
/// The cases every <see cref="IMemoryEngines"/> must pass, written once against the port and read only through it: the stand-in runs
/// them in the unit tier, Lyntai's adapter over a real file in its dependency suite, so the stand-in the application's tests are proved
/// over cannot drift from the engine it stands in for.
/// </summary>
public abstract class MemoryContract
{
    /// <summary>The repository every case writes in.</summary>
    protected const string Owner = "Owner";

    /// <summary>A fresh memory under test, keeping <paramref name="repository"/>'s.</summary>
    protected abstract IMemoryEngines Memory(string repository);

    private IMemoryEngine Engine() => Memory(Owner).Open(Owner, MemoryReinforcement.None)!;

    [Fact]
    public void A_blank_repository_opens_no_memory()
    {
        var memory = Memory(Owner);

        Assert.Null(memory.Open("", MemoryReinforcement.None));
        Assert.Null(memory.Open("  ", MemoryReinforcement.None));
        Assert.NotNull(memory.Open(Owner, MemoryReinforcement.None));
    }

    [Fact]
    public async Task A_same_content_write_answers_the_entry_it_repeats_its_revision_moved()
    {
        var engine = Engine();

        var first = await engine.RememberAsync("The map is drawn in tiles.");
        var again = await engine.RememberAsync("The map is drawn in tiles.");
        var other = await engine.RememberAsync("Tiles are sixteen pixels.");

        Assert.Equal(first.Id, again.Id);
        Assert.True(again.Revision > first.Revision, $"revision {again.Revision} after {first.Revision}");
        Assert.NotEqual(first.Id, other.Id);
        Assert.Equal("The map is drawn in tiles.", (await engine.GetAsync(first.Id))!.Text);
    }

    [Fact]
    public async Task A_repeated_relation_answers_the_same_link()
    {
        var engine = Engine();
        var map = await engine.RememberAsync("The map is drawn in tiles.");
        var pixels = await engine.RememberAsync("Tiles are sixteen pixels.");

        var first = await engine.RelateAsync(pixels.Id, map.Id, "disputes");
        var again = await engine.RelateAsync(pixels.Id, map.Id, "disputes");

        Assert.Equal((pixels.Id, map.Id, "disputes"), (first.From, first.To, first.Kind));
        Assert.Equal((first.From, first.To, first.Kind), (again.From, again.To, again.Kind));
        Assert.Contains(await engine.ExpandAsync(pixels.Id), related => related.Entry.Id == map.Id);
    }

    [Fact]
    public async Task A_confirmation_and_an_end_hold_only_at_the_revision_read()
    {
        var engine = Engine();
        var entry = await engine.RememberAsync("The map is drawn in tiles.");

        Assert.Null(await engine.ConfirmAsync(entry.Id, entry.Revision + 7));
        var confirmed = await engine.ConfirmAsync(entry.Id, entry.Revision);
        Assert.NotNull(confirmed);
        Assert.True(confirmed.Revision > entry.Revision, $"revision {confirmed.Revision} after {entry.Revision}");

        Assert.Null(await engine.EndAsync(entry.Id, entry.Revision, "Read before the confirmation."));
        var ended = await engine.EndAsync(entry.Id, confirmed.Revision, "The map is drawn in vectors now.");
        Assert.Contains("The map is drawn in vectors now.", ended!.Ended);
        Assert.Null(await engine.EndAsync(entry.Id, ended.Revision, "Ended twice."));
        Assert.Null(await engine.ConfirmAsync(entry.Id, ended.Revision));
        Assert.Null(await engine.ConfirmAsync("404", 1));
    }

    [Fact]
    public async Task A_read_with_no_reinforcement_moves_no_revision()
    {
        var engine = Engine();
        var map = await engine.RememberAsync("The map is drawn in tiles.");
        var legend = await engine.RememberAsync("The legend sits under the map.");
        await engine.RelateAsync(legend.Id, map.Id, "amends");

        await engine.SearchAsync("map", 10);
        await engine.GetAsync(map.Id);
        await engine.ExpandAsync(legend.Id);

        Assert.Equal(map.Revision, (await engine.GetAsync(map.Id))!.Revision);
        Assert.Equal(legend.Revision, (await engine.GetAsync(legend.Id))!.Revision);
    }

    [Fact]
    public async Task A_view_asks_its_check_before_it_reads_and_a_refused_entry_reads_as_missing()
    {
        var memory = Memory(Owner);
        var engine = memory.Open(Owner, MemoryReinforcement.None)!;
        var map = await engine.RememberAsync("The map is drawn in tiles.");
        Assert.True(engine.HoldsView);

        var refusing = new Checking(permits: false);
        Assert.Null(await engine.ViewOf(refusing).GetAsync(map.Id));
        Assert.Contains(new MemoryReference(Owner, map.Id), refusing.Asked);

        var permitting = new Checking(permits: true);
        var view = engine.ViewOf(permitting);
        Assert.Equal("The map is drawn in tiles.", (await view.GetAsync(map.Id))!.Text);
        Assert.Equal(map.Revision, await view.RevisionOfAsync(map.Id));
        Assert.Contains(new MemoryReference(Owner, map.Id), permitting.Asked);
    }

    private sealed class Checking(bool permits) : IMemoryViewCheck
    {
        public List<MemoryReference> Asked { get; } = [];

        public Task<bool> PermitsAsync(MemoryReference reference, CancellationToken ct = default)
        {
            Asked.Add(reference);
            return Task.FromResult(permits);
        }
    }
}
