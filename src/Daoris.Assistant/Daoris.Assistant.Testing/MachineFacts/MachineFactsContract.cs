using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="IMachineFacts"/> passes, written once against the port: <see cref="MemoryMachineFacts"/> runs them in the
/// assistant's suite, the desktop adapter's facts over the driver's hub in its own, so the fake the room is proved on cannot drift
/// from the judges it stands in for.
/// </summary>
public abstract class MachineFactsContract
{
    /// <summary>The facts under test, no judge answered yet.</summary>
    protected abstract IMachineFacts Backend { get; }

    /// <summary>Makes <paramref name="topic"/>'s judge answer <paramref name="facts"/>: the fake is told, the real side's judge answers.</summary>
    protected abstract Task AnswerAsync(string topic, IReadOnlyList<MachineFact> facts);

    [Fact]
    public void Every_topic_reads_not_read_yet_until_its_judge_answers()
    {
        foreach (var topic in FactTopics.All)
        {
            var reading = Backend.Read(topic);

            Assert.Equal((topic, FactsState.NotReadYet), (reading.Topic, reading.State));
            Assert.Empty(reading.Facts);
        }
    }

    [Fact]
    public async Task A_topic_answered_reads_its_facts_in_order_and_the_others_stay_unread()
    {
        MachineFact[] facts = [new("main", "clean"), new("feature", "ahead") { Parts = [new("commits", "2")] }];

        await AnswerAsync(FactTopics.Trees, facts);

        var reading = Backend.Read(FactTopics.Trees);
        Assert.Equal(FactsState.Read, reading.State);
        Assert.Equal(["main", "feature"], reading.Facts.Select(fact => fact.Name));
        Assert.Equal(["clean", "ahead"], reading.Facts.Select(fact => fact.State));
        Assert.Equal(("commits", "2"), (reading.Facts[1].Parts[0].Name, reading.Facts[1].Parts[0].State));
        Assert.Equal(FactsState.NotReadYet, Backend.Read(FactTopics.Sessions).State);
    }

    [Fact]
    public async Task An_answer_is_told_by_its_topic()
    {
        var told = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Backend.Changed += topic => told.TrySetResult(topic);

        await AnswerAsync(FactTopics.Roles, [new("ask-daoris", "staffed")]);

        Assert.Equal(FactTopics.Roles, await told.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void A_topic_the_room_does_not_read_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Backend.Read("weather"));
    }
}
