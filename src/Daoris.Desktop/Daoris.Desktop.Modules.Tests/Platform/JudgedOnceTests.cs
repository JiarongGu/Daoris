using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A slice over a kept list does no work while nothing moved: the list read again is the same object, judged once, and the judgement
/// handed back the same is compared no further by the slice.
/// </summary>
public sealed class JudgedOnceTests
{
    /// <summary>A partition's data that counts how often it is read, which a comparison of it does by writing it.</summary>
    private sealed class Counted
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public int Value
        {
            get
            {
                Interlocked.Increment(ref _reads);
                return 1;
            }
        }
    }

    [Fact]
    public void A_list_is_judged_once_while_it_is_the_same_object_and_again_once_it_is_another()
    {
        var judged = 0;
        var once = new JudgedOnce<string>(list =>
        {
            judged++;
            return [new SliceJudged("all", list)];
        });
        var first = new string('a', 3);

        var made = once.Of(first);
        Assert.Same(made, once.Of(first));
        Assert.Equal(1, judged);

        Assert.NotSame(made, once.Of(new string('a', 3)));
        Assert.Equal(2, judged);
    }

    [Fact]
    public async Task A_judgement_handed_back_as_the_one_last_judged_is_compared_no_further_and_pushes_nothing()
    {
        var bus = new EventBus(NullLogger<EventBus>.Instance);
        var pushes = 0;
        bus.SubscribeToModule(StateSlice.Module, message =>
        {
            if (message.Type == StateSlice.PushType) Interlocked.Increment(ref pushes);
            return Task.CompletedTask;
        });
        var data = new Counted();
        IReadOnlyList<SliceJudged> same = [new SliceJudged("all", data)];
        var slice = new StateSlice("test", bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(same), epoch: "e1");

        await slice.JudgeAsync();
        var compared = data.Reads;
        Assert.True(compared > 0);
        Assert.Equal(1, pushes);

        await slice.JudgeAsync();
        await slice.JudgeAsync();

        Assert.Equal(compared, data.Reads);
        Assert.Equal(1, pushes);
        Assert.Equal(3, slice.Judgements);
        Assert.Equal(StateSlice.Ready, slice.Snapshot().Status);
    }
}
