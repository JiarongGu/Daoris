using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="IConversations"/> passes: <see cref="ScriptedConversations"/> in the assistant's suite, the desktop
/// adapter over the one conversation runtime in its own.
/// </summary>
public abstract class ConversationsContract
{
    private static readonly CancellationToken None = CancellationToken.None;

    /// <summary>The conversations under test, none started.</summary>
    protected abstract IConversations Backend { get; }

    /// <summary>Ends <paramref name="conversation"/>'s turn as its agent would: the fake is told, the real side's agent answers.</summary>
    protected abstract Task EndTurnAsync(string conversation);

    private static ConversationStart Asked(string words = "What is running?") =>
        new("rooms/ask", "restricted", "read", words) { Rules = ["drafts"], Reads = ["docs/"], Focus = "workspace:game" };

    [Fact]
    public async Task A_start_answers_turning_under_the_mode_and_access_asked_and_reads_back_the_same()
    {
        var seen = await Backend.StartAsync(Asked(), None);

        Assert.False(string.IsNullOrWhiteSpace(seen.Id));
        Assert.False(string.IsNullOrWhiteSpace(seen.Agent));
        Assert.Equal(("restricted", "read", ConversationState.Turning, "workspace:game"), (seen.Mode, seen.Access, seen.State, seen.Focus));
        Assert.Null(seen.AccessAsked);
        Assert.Equal(seen, await Backend.ReadAsync(seen.Id, None));
    }

    [Fact]
    public async Task Two_starts_are_two_conversations()
    {
        var one = await Backend.StartAsync(Asked("one"), None);
        var two = await Backend.StartAsync(Asked("two"), None);

        Assert.NotEqual(one.Id, two.Id);
    }

    [Fact]
    public async Task A_turns_end_leaves_it_waiting_and_words_turn_it_again()
    {
        var seen = await Backend.StartAsync(Asked(), None);

        await EndTurnAsync(seen.Id);
        Assert.Equal(ConversationState.Waiting, (await Backend.ReadAsync(seen.Id, None))!.State);

        await Backend.GoOnAsync(seen.Id, "And the trees?", None);
        Assert.Equal(ConversationState.Turning, (await Backend.ReadAsync(seen.Id, None))!.State);
    }

    [Fact]
    public async Task An_access_swap_waits_for_the_turns_end()
    {
        var seen = await Backend.StartAsync(Asked(), None);

        await Backend.SwapAccessAsync(seen.Id, "full", None);
        var asked = (await Backend.ReadAsync(seen.Id, None))!;
        Assert.Equal(("read", "full"), (asked.Access, asked.AccessAsked));

        await EndTurnAsync(seen.Id);
        var swapped = (await Backend.ReadAsync(seen.Id, None))!;
        Assert.Equal(("full", (string?)null), (swapped.Access, swapped.AccessAsked));
    }

    [Fact]
    public async Task A_stopped_conversation_stays_stopped_and_takes_no_words()
    {
        var seen = await Backend.StartAsync(Asked(), None);

        await Backend.StopAsync(seen.Id, None);
        await Backend.StopAsync(seen.Id, None);

        Assert.Equal(ConversationState.Stopped, (await Backend.ReadAsync(seen.Id, None))!.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Backend.GoOnAsync(seen.Id, "Still there?", None));
    }

    [Fact]
    public async Task An_unknown_conversation_reads_as_none_and_is_refused()
    {
        Assert.Null(await Backend.ReadAsync("nobody", None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Backend.GoOnAsync("nobody", "Hello?", None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Backend.StopAsync("nobody", None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Backend.SwapAccessAsync("nobody", "full", None));
    }
}
