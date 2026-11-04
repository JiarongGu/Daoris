using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// What is worth interrupting the person for, in the one shape the shell's notifier and the page's toast read: a look's news, a
/// journal's news and a conversation's answer are each told through it.
/// </summary>
internal static class AttentionTold
{
    public static Task TellAsync(IEventBus bus, AttentionEvent item) => bus.EmitAsync("DAORIS", "SESSION_ATTENTION", new
    {
        Kind = item.Kind.ToString(),
        item.Session,
        item.Repository,
        item.State,
        item.Headline,
        item.Detail,
        // The quest a park names; null for a session's own news.
        item.Quest,
        // How many sessions failed before a quest parked, which its notification says in the window's words.
        item.Strikes,
    });
}
