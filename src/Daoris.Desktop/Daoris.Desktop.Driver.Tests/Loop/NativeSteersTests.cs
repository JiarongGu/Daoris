using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's *Send now* on the native wire: one line with no priority, under a uuid, whose echo says where it landed. Echoed
/// before the running turn's result, it joined that turn; after it, the turn had ended first and the word is the next turn, which the
/// door waits out. Against a wire of lambdas: no process, so the fast half. The real binary's measured turns are the proof tool's.
/// </summary>
public sealed class NativeSteersTests
{
    private sealed class Wire
    {
        public List<SessionEvent> Recorded { get; } = [];

        public List<string> Lines { get; } = [];

        public bool Writes { get; init; } = true;

        public NativeSteers Steers(bool echoes = true) => new(
            (text, uuid) => echoes ? $"{uuid}|{text}" : null,
            (_, line) =>
            {
                Lines.Add(line);
                return Task.FromResult<bool?>(Writes);
            },
            Recorded.Add);

        public string Uuid => Lines[^1].Split('|')[0];

        public IEnumerable<string?> Codes => Recorded.Where(e => e.Kind == SessionEventKind.Note).Select(e => e.Parts?[0].Code);
    }

    [Fact]
    public async Task A_word_echoed_before_the_turns_result_joined_that_turn_and_is_said_so()
    {
        var wire = new Wire();
        var steers = wire.Steers();

        var landed = steers.Now(new ChatMessage("put MANGO in your reply", []))!;

        var held = Assert.Single(wire.Recorded);
        Assert.Equal((SessionEventKind.User, "now", "now"), (held.Kind, held.Reaches, held.Level));
        Assert.NotNull(held.Id);
        Assert.EndsWith("|put MANGO in your reply", Assert.Single(wire.Lines));
        Assert.False(landed.IsCompleted);

        steers.Echoed(wire.Uuid);

        await landed.WaitAsync(Poll.Patience);
        var taken = wire.Recorded[1];
        Assert.Equal((SessionEventKind.User, null, held.Id), (taken.Kind, taken.Reaches, taken.Id));
        Assert.Equal([ChatNoteCodes.WordsSentNowInTurn.Code], wire.Codes);
        steers.TurnEnded();
        Assert.Equal(3, wire.Recorded.Count);
    }

    [Fact]
    public async Task A_word_echoed_after_the_turns_result_began_the_next_turn_which_the_door_waits_out()
    {
        var wire = new Wire();
        var steers = wire.Steers();
        var landed = steers.Now(new ChatMessage("put MANGO in your reply", []))!;

        steers.TurnEnded();
        Assert.False(landed.IsCompleted);
        steers.Echoed(wire.Uuid);
        Assert.False(landed.IsCompleted);
        Assert.Equal([ChatNoteCodes.WordsSentNowNextTurn.Code], wire.Codes);
        Assert.Contains(wire.Recorded, e => e.Kind == SessionEventKind.User && e.Reaches is null && e.Id == wire.Recorded[0].Id);

        steers.TurnEnded();

        await landed.WaitAsync(Poll.Patience);
    }

    [Fact]
    public async Task A_word_the_wire_would_not_take_is_said_not_sent_by_its_id_and_ends_its_wait()
    {
        var wire = new Wire { Writes = false };
        var steers = wire.Steers();

        var landed = steers.Now(new ChatMessage("put MANGO in your reply", []))!;

        await landed.WaitAsync(Poll.Patience);
        var note = wire.Recorded[^1];
        Assert.Equal(ChatNoteCodes.WordsNotSent.Code, note.Parts?[0].Code);
        Assert.Equal([wire.Recorded[0].Id!], note.Words);
    }

    [Fact]
    public async Task The_harness_gone_ends_every_wait_and_a_late_echo_is_nothing()
    {
        var wire = new Wire();
        var steers = wire.Steers();
        var landed = steers.Now(new ChatMessage("put MANGO in your reply", []))!;
        var uuid = wire.Uuid;

        steers.Gone();
        steers.Echoed(uuid);

        await landed.WaitAsync(Poll.Patience);
        Assert.Single(wire.Recorded);
    }

    [Fact]
    public void A_dialect_that_never_echoes_steers_nothing()
    {
        var wire = new Wire();
        var steers = wire.Steers(echoes: false);

        Assert.False(steers.Steers);
        Assert.Null(steers.Now(new ChatMessage("put MANGO in your reply", [])));
        Assert.Empty(wire.Recorded);
        Assert.Empty(wire.Lines);
    }
}
