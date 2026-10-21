using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NOTIFY1a: a conversation that answered the person is told as the tick's attention is, on <c>SESSION_ATTENTION</c>, in the
/// same shape (the shell's notifier and the page's toast read it by its fields), while the machine's notifications are on.
/// </summary>
public sealed class DriverLoopAttentionTests : DriverModuleBridge
{
    private JsonElement[] Attention() => [.. Raised
        .Where(message => message.Type == "SESSION_ATTENTION")
        .Select(message => JsonSerializer.SerializeToElement(message.Payload))];

    /// <summary>The runner's answer, told with the driver's own sentence (D24) and the conversation a notification opens.</summary>
    [Fact]
    public async Task A_conversation_s_answer_is_told_as_attention_in_the_driver_s_sentence()
    {
        var loop = Loop();

        await loop.AnsweredAsync(new AttentionEvent(AttentionKind.Answered, "c1", "engine"));

        await UntilAsync(() => Attention().Length > 0);
        var told = Assert.Single(Attention());
        Assert.Equal(
            ("Answered", "c1", "engine", "engine — a conversation answered you"),
            (told.GetProperty("Kind").GetString(), told.GetProperty("Session").GetString(),
                told.GetProperty("Repository").GetString(), told.GetProperty("Headline").GetString()));
        Assert.Equal(JsonValueKind.Null, told.GetProperty("Detail").ValueKind);
    }

    /// <summary>Notifications off is not being told, as for the tick's attention: nothing is said.</summary>
    [Fact]
    public async Task A_conversation_s_answer_is_not_told_while_notifications_are_off()
    {
        DriverConfig.Empty.WithNotify(false).Save(DriverConfigPath);
        var loop = Loop();

        await loop.AnsweredAsync(new AttentionEvent(AttentionKind.Answered, "c1", "engine"));
        // Turned back on, the next is told: so the first was not merely late.
        DriverConfig.Empty.Save(DriverConfigPath);
        await loop.AnsweredAsync(new AttentionEvent(AttentionKind.Answered, "c2", "engine"));

        await UntilAsync(() => Attention().Length > 0);
        Assert.Equal("c2", Assert.Single(Attention()).GetProperty("Session").GetString());
    }

    /// <summary>
    /// NOTIFY2g (the notification design §2.4, §2.5): an answer is kept as an <c>answered</c> notice before the switch's gate, so one heard
    /// with the switch off is listed; one notice per conversation, unread again at its next answer, and never said again as a notice's
    /// arrival, its own attention being its voice.
    /// </summary>
    [Fact]
    public async Task A_conversation_s_answer_is_kept_as_a_notice_whatever_the_switch_says()
    {
        DriverConfig.Empty.WithNotify(false).Save(DriverConfigPath);
        var loop = Loop();
        await loop.Notices.Slice.JudgeAsync();

        await loop.AnsweredAsync(new AttentionEvent(AttentionKind.Answered, "c1", "engine"));
        var kept = Assert.Single(Daoris.Driver.Notices.Read(Home, DateTimeOffset.UtcNow)!);
        Assert.Equal(("answered-c1", "answered", "c1", "engine", false, (string?)null), (kept.Id, kept.Kind, kept.Session, kept.Repository, kept.Read, kept.Workspace));
        Assert.Empty(Attention());

        DriverConfig.Empty.Save(DriverConfigPath);
        Assert.Null(loop.Notices.MarkRead(["answered-c1"]));
        await Task.Delay(20);
        await loop.AnsweredAsync(new AttentionEvent(AttentionKind.Answered, "c1", "engine"));
        await loop.Notices.Slice.JudgeAsync();

        var renewed = Assert.Single(Daoris.Driver.Notices.Read(Home, DateTimeOffset.UtcNow)!);
        Assert.False(renewed.Read);
        Assert.DoesNotContain(Raised, each => each.Type == NoticesStateSlice.ArrivedType);
    }
}
