using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Who said a word, read once. This is the driver's twin of the service's
/// <c>Senders</c>, held to <c>fixtures/senders.json</c> among the service's tests, which the page's <c>sender.test.ts</c> reads as well,
/// row for row. It reads a kept word (<see cref="SaidWordView"/>) and an event (<see cref="SessionEvent"/>), which carries no key, so it
/// tells a partner from a helper by the work of the record the event names.
/// </summary>
public sealed class SendersTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-02T10:00:00Z");

    public static TheoryData<string> WordRows => [.. Table.Read().Rows.Where(row => row.Word is not null).Select(row => row.Name)];

    public static TheoryData<string> EventRows => [.. Table.Read().Rows.Where(row => row.Event is not null).Select(row => row.Name)];

    /// <summary>🔴 Every row with a kept word: its marks answer the sender the table says, and <c>Persons</c> is the person being it.</summary>
    [Theory]
    [MemberData(nameof(WordRows))]
    public void A_kept_words_marks_answer_the_sender_the_table_says(string name)
    {
        var row = Table.Read().Rows.Single(each => each.Name == name);
        var word = new SaidWordView("w1", "said", Now, [], false) { By = row.Word!.By, Helper = row.Word.Helper, From = row.Word.From, Key = row.Word.Key };

        Assert.Equal(row.Sender, Senders.Of(word));
        Assert.Equal(row.Sender == Senders.Person, word.Persons);
    }

    /// <summary>🔴 Every row with an event: its marks and the work of the helper it names answer the sender the table says.</summary>
    [Theory]
    [MemberData(nameof(EventRows))]
    public void An_events_marks_answer_the_sender_the_table_says(string name)
    {
        var row = Table.Read().Rows.Single(each => each.Name == name);
        var marks = row.Event!;
        Assert.Equal(row.Sender, Senders.Of(marks.Origin, marks.From, marks.Opinion, marks.Helper, marks.HelperWork, marks.Channel));
    }

    /// <summary>The message a conversation says is read as the event it is recorded as: the same sender, and <c>Persons</c> its person being it.</summary>
    [Fact]
    public void A_chat_message_is_read_as_the_event_it_is_recorded_as()
    {
        var daoris = new ChatMessage("Go on.", []) { Daoris = new DaorisWord("transient/s1") };
        var supervised = new ChatMessage("Yes.", []) { Supervised = "d1" };
        var person = new ChatMessage("Hello.", []);

        Assert.Equal(Senders.Daoris, daoris.Sender);
        Assert.Equal(Senders.Supervisor, supervised.Sender);
        Assert.False(supervised.Persons);
        Assert.True(person.Persons);
        Assert.Equal(Senders.Person, person.Sender);
        Assert.Equal("target", ChatMessage.Words(daoris).Origin);
        Assert.Equal("person", ChatMessage.Words(person).Origin);
    }

    private sealed record WordMarks(string? From = null, string? By = null, string? Helper = null, string? Key = null, string? Channel = null);

    private sealed record EventMarks(string? Origin = null, string? From = null, string? Opinion = null, string? Helper = null, string? HelperWork = null, string? Channel = null);

    private sealed record Row(string Name, WordMarks? Word, EventMarks? Event, string Sender);

    private sealed record Table(IReadOnlyList<Row> Rows)
    {
        public static Table Read()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "senders.json")));
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            return new Table(document.RootElement.GetProperty("rows").Deserialize<List<Row>>(options)!);
        }
    }
}
