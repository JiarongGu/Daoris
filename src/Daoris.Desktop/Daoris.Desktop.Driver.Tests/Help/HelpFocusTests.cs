using System.Text.Json;
using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// An Ask Daoris conversation's focus (ASKSCOPE2b, `docs/2026-11-11-ask-scope-ux-design.md` §6): the place, one shape on every
/// side (its twin is the page's `help/focus.ts`, whose `focus.test.ts` holds the same spellings), kept on the conversation's
/// file beside its name and pin, and chosen through <see cref="HelpFocusing"/>, which <c>HELP_FOCUS</c> and
/// <c>ask-daoris focus</c> both call. Files only.
/// </summary>
public sealed class HelpFocusTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-focus-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Monday = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly RepoView[] Registry =
    [
        new("engine", true, "C:/checkouts/engine", "Games"),
        new("tools", true, null, "Games"),
        new("api", true, "C:/checkouts/api", "Work"),
    ];

    private static readonly HelpFocus Engine = HelpFocus.OfRepository("Games", "engine");

    public HelpFocusTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private static SessionRecord Help(string id, string state = "working", string workspace = "Games") =>
        new(id, HelpRoom.Repository, state) { Kind = "chat", Created = Monday, Updated = Monday, Workspace = workspace };

    private void Said(string id, string question)
    {
        Events.Append(id, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = question, At = Monday });
        Events.Append(id, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", At = Monday });
    }

    private HelpFocusAnswer Choose(SessionRecord record, HelpFocus place) =>
        new HelpFocusing(_home).Choose(record.Id, [record], Registry, Events, _ => false, place);

    /// <summary>The spelling table, line for line the page's `focus.test.ts`.</summary>
    [Theory]
    [InlineData("""{"kind":"machine"}""", "machine")]
    [InlineData("""{"kind":"workspace","workspace":"Games"}""", "Games")]
    [InlineData("""{"kind":"repository","workspace":"Games","repository":"engine"}""", "Games/engine")]
    public void A_place_is_spelled_one_way_on_the_wire_and_in_the_terminal(string wire, string said)
    {
        using var document = JsonDocument.Parse(wire);
        var place = HelpFocus.Read(document.RootElement);
        Assert.NotNull(place);
        Assert.Equal(said, place.Said);
        Assert.Equal(place, HelpFocus.FromSaid(said));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) place.Write(writer);
        Assert.Equal(wire, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Theory]
    [InlineData("""{"kind":"room"}""")]
    [InlineData("""{"kind":"workspace"}""")]
    [InlineData("""{"kind":"repository","workspace":"Games"}""")]
    [InlineData("""{"kind":"workspace","workspace":" "}""")]
    [InlineData("\"Games\"")]
    public void A_shape_that_is_no_place_reads_as_none(string wire)
    {
        using var document = JsonDocument.Parse(wire);
        Assert.Null(HelpFocus.Read(document.RootElement));
    }

    [Fact]
    public void A_place_holds_itself_and_what_is_inside_it()
    {
        Assert.True(HelpFocus.Machine.Holds(Engine));
        Assert.True(HelpFocus.OfWorkspace("games").Holds(Engine));
        Assert.False(HelpFocus.OfWorkspace("Work").Holds(Engine));
        Assert.True(Engine.Holds(HelpFocus.OfRepository("Games", "engine")));
        Assert.False(Engine.Holds(HelpFocus.OfWorkspace("Games")));
        Assert.False(Engine.Holds(HelpFocus.OfRepository("Work", "engine")));
    }

    [Fact]
    public void A_record_from_before_reads_its_home_and_default_reads_this_machine()
    {
        var games = HelpFoci.Of(HelpKept.None, "Games");
        Assert.Equal(HelpFocus.OfWorkspace("Games"), games.Focus);
        Assert.Null(games.FocusNext);
        Assert.Equal([HelpFocus.OfWorkspace("Games")], games.Foci);
        Assert.Equal(HelpFocus.Machine, HelpFoci.Of(HelpKept.None, RemoteTarget.DefaultWorkspace).Focus);
    }

    [Fact]
    public void A_focus_chosen_before_any_words_is_the_first_and_no_change()
    {
        var answer = Choose(Help("h1"), Engine);
        Assert.Null(answer.Refused);
        Assert.Equal(Engine, answer.Focus);
        Assert.Null(answer.FocusNext);
        var kept = new HelpConversations(_home).Read("h1");
        Assert.Equal(Engine, kept.Focus);
        Assert.Null(kept.FocusNext);
        Assert.Equal([Engine], kept.Foci);
    }

    [Fact]
    public void A_change_after_words_waits_for_the_next_message_and_choosing_the_focus_again_clears_it()
    {
        Said("h1", "What blocks the release?");
        var record = Help("h1");
        var changed = Choose(record, Engine);
        Assert.Equal(HelpFocus.OfWorkspace("Games"), changed.Focus);
        Assert.Equal(Engine, changed.FocusNext);
        var kept = new HelpConversations(_home).Read("h1");
        Assert.Null(kept.Focus);
        Assert.Equal(Engine, kept.FocusNext);
        Assert.Null(kept.Foci);

        var kept2 = Choose(record, HelpFocus.OfWorkspace("games"));
        Assert.Equal(HelpFocus.OfWorkspace("Games"), kept2.Focus);
        Assert.Null(kept2.FocusNext);
        Assert.Null(new HelpConversations(_home).Read("h1").FocusNext);
    }

    [Fact]
    public void A_place_is_spelled_as_the_registry_spells_it_and_a_repository_without_a_checkout_is_one()
    {
        var answer = Choose(Help("h1"), HelpFocus.OfRepository("games", "tools"));
        Assert.Equal(HelpFocus.OfRepository("Games", "tools"), answer.Focus);
        Assert.Equal("Games", answer.Focus!.Workspace);
    }

    [Fact]
    public void An_unknown_place_another_kind_of_record_and_one_that_cannot_go_on_are_refused()
    {
        Assert.Equal(HelpFocusing.UnknownPlace, Choose(Help("h1"), HelpFocus.OfRepository("Work", "engine")).Refused);
        Assert.Equal(HelpFocusing.UnknownPlace, Choose(Help("h1"), HelpFocus.OfWorkspace("Elsewhere")).Refused);
        var other = new SessionRecord("s1", "engine", "working") { Kind = "chat", Created = Monday, Updated = Monday };
        Assert.Equal(HelpFocusing.NotHelp, Choose(other, Engine).Refused);
        Said("h2", "Why?");
        Assert.Equal(HelpFocusing.Ended, Choose(Help("h2", "completed"), Engine).Refused);
        Assert.Null(new HelpConversations(_home).Read("h2").FocusNext);
    }

    [Fact]
    public void About_lists_the_conversations_homed_there_or_that_held_a_focus_there_or_inside_it()
    {
        Said("g1", "In Games");
        Said("w1", "In Work, about engine");
        Said("w2", "In Work only");
        var records = new[] { Help("g1", workspace: "Games"), Help("w1", workspace: "Work"), Help("w2", workspace: "Work") };
        new HelpConversations(_home).Change("w1", kept => kept with { Focus = HelpFocus.OfWorkspace("Work"), Foci = [HelpFocus.OfWorkspace("Work"), Engine] });

        string[] About(HelpFocus place) => new HelpConversations(_home).List(records, Events, _ => false, about: place)
            .Conversations.Select(row => row.Session).Order().ToArray();
        Assert.Equal(["g1", "w1"], About(HelpFocus.OfWorkspace("Games")));
        Assert.Equal(["w1"], About(Engine));
        Assert.Equal(["g1", "w1", "w2"], About(HelpFocus.Machine));
        Assert.Equal(["w1", "w2"], About(HelpFocus.OfWorkspace("Work")));
    }

    [Fact]
    public void The_terminal_reads_a_focus_and_a_listing_about_a_place_never_beside_a_workspace()
    {
        Assert.Equal(new HelpVerbAsk("focus") { Id = "h1", Text = "Games/engine" }, HelpCommand.Read(["focus", "h1", " Games/engine "], out _));
        Assert.Equal(new HelpVerbAsk("focus") { Id = "h1", Text = "machine" }, HelpCommand.Read(["focus", "h1", "machine"], out _));
        Assert.Null(HelpCommand.Read(["focus", "h1", "Games/"], out var bad));
        Assert.NotNull(bad);
        Assert.Equal(new HelpVerbAsk("list") { About = "Games" }, HelpCommand.Read(["list", "--about", "Games"], out _));
        Assert.Null(HelpCommand.Read(["list", "--about", "Games", "--workspace", "Work"], out var both));
        Assert.Contains("--about", both);
    }

    [Fact]
    public void A_row_answers_its_applied_focus_and_the_one_waiting()
    {
        Said("h1", "What blocks the release?");
        Choose(Help("h1"), Engine);
        var row = Assert.Single(new HelpConversations(_home).List([Help("h1")], Events, _ => false).Conversations);
        Assert.Equal(HelpFocus.OfWorkspace("Games"), row.Focus);
        Assert.Equal(Engine, row.FocusNext);
    }
}
