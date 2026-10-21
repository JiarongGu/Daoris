using System.Globalization;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>One ask's channel as the person reads it, for <i>Messages</i>: a live ask of this machine's, or a teammate's.</summary>
/// <param name="Ask">The ask's id.</param>
/// <param name="Workspace">Its circle: the ask's own, or a teammate's ask's from its messages.</param>
/// <param name="Teammate">Whether it is held on a teammate's machine, its channel read from the messages the sync mirrored.</param>
/// <param name="View">The channel as the person reads it, which moves no mark: its <c>unread</c> the service's own.</param>
public sealed record AskChannel(string Ask, string Workspace, bool Teammate, ChannelView View);

/// <summary>
/// <i>Messages</i> judged (NOTIFY2g, D196 points 6 and 7; the notification design §2.4, §2.5, §5): the <c>messages</c> partition of the
/// <c>notices</c> slice, one entry per ask, and each urgent message to everyone said once as it arrives.
/// </summary>
/// <remarks>
/// <para><b>One unread model, the service's.</b> An entry's <c>unread</c> is its channel's, the number the ask's row and the rail say,
/// which passes over the person's own posts and Daoris's lines. <c>newest</c> is the newest message an agent or a teammate wrote, read
/// or not, never the person's own nor a line: its first line, who wrote it, its level and whether it is to everyone.</para>
///
/// <para><b>A watermark per ask</b> (§5): the messages of an ask its last judgement saw, by id, so one mirrored late with an older moment
/// is still new. An ask's first judgement records what it holds and says nothing, whether it was there at the start or came after, so a
/// restart repeats nothing and an ask is never a backlog. Each later judgement says each message past it that is urgent, to everyone,
/// and an agent's or a teammate's, then moves it; an urgent one followed by an ordinary one before the next judgement is still said, and
/// a judgement that sees nothing new says nothing. A message from before D188's levels reads as ordinary. An ask no longer listed is
/// forgotten.</para>
///
/// <para><b>Never the desktop.</b> An arrival is the page's toast alone (§5's table); nothing here reaches a system notification.</para>
/// </remarks>
public sealed class AskMessages
{
    /// <summary>The event an urgent message to everyone is said by.</summary>
    public const string ArrivedType = "MESSAGE";

    /// <summary>The fields an ask's entry is pushed with: the page's <c>AskMessages</c> type, which its test reads.</summary>
    public static readonly IReadOnlyList<string> Fields = ["ask", "workspace", "teammate", "unread", "newest"];

    /// <summary>The fields of an entry's <c>newest</c>, and of an arrival beside the ask's: the page's <c>NewestMessage</c> type.</summary>
    public static readonly IReadOnlyList<string> NewestFields = ["id", "at", "who", "repository", "line", "level", "toEveryone"];

    /// <summary>How long a message's line may be, in characters, before it is cut.</summary>
    public const int LineBound = 200;

    /// <summary>An agent wrote it, on this machine or a teammate's.</summary>
    public const string Agent = "agent";

    /// <summary>A teammate wrote it, as a person, on their machine.</summary>
    public const string Teammate = "teammate";

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, HashSet<string>> _seen = new(StringComparer.Ordinal);

    /// <summary>
    /// One judgement over every ask's channel: the partition, keyed by ask, and the messages that arrived since the last, each as its
    /// arrival says it.
    /// </summary>
    public (JsonObject Partition, IReadOnlyList<JsonObject> Arrived) Judge(IReadOnlyList<AskChannel> channels)
    {
        var partition = new JsonObject();
        var arrived = new List<JsonObject>();
        lock (_gate)
        {
            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var channel in channels)
            {
                if (!listed.Add(channel.Ask)) continue;
                var messages = channel.View.Items.Where(Theirs).ToList();
                var entry = new JsonObject
                {
                    ["ask"] = channel.Ask,
                    ["workspace"] = channel.Workspace,
                    ["teammate"] = channel.Teammate,
                    ["unread"] = channel.View.Unread,
                };
                if (messages.OrderBy(item => item.At).ThenBy(item => item.Id, StringComparer.Ordinal).LastOrDefault() is { } newest)
                {
                    entry["newest"] = Newest(newest);
                }

                partition[channel.Ask] = entry;

                var ids = messages.Select(item => item.Id!).ToHashSet(StringComparer.Ordinal);
                if (_seen.TryGetValue(channel.Ask, out var seen))
                {
                    foreach (var message in messages.Where(item => !seen.Contains(item.Id!) && Said(item)).OrderBy(item => item.At))
                    {
                        var said = Newest(message);
                        said["ask"] = channel.Ask;
                        said["workspace"] = channel.Workspace;
                        said["teammate"] = channel.Teammate;
                        arrived.Add(said);
                    }

                    seen.UnionWith(ids);
                }
                else
                {
                    _seen[channel.Ask] = ids;
                }
            }

            foreach (var gone in _seen.Keys.Where(ask => !listed.Contains(ask)).ToList()) _seen.Remove(gone);
        }

        return (partition, arrived);
    }

    /// <summary>A message an agent or a teammate wrote: never the person's own on this machine, nor one of Daoris's lines.</summary>
    public static bool Theirs(ChannelItemView item) => item.IsMessage && (!item.Person || item.Origin is not null);

    /// <summary>What is said as it arrives (§5): urgent, and to everyone. One from before the levels is ordinary.</summary>
    private static bool Said(ChannelItemView item) => item.Urgent == true && item.To is null;

    private static JsonObject Newest(ChannelItemView item)
    {
        var newest = new JsonObject
        {
            ["id"] = item.Id,
            ["at"] = item.At.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture),
            ["who"] = item.Person ? Teammate : Agent,
        };
        if (item.Repository is { Length: > 0 } repository) newest["repository"] = repository;
        newest["line"] = Line(item.Text!);
        newest["level"] = item.Urgent == true ? "urgent" : "ordinary";
        newest["toEveryone"] = item.To is null;
        return newest;
    }

    /// <summary>A message's first line with words, trimmed, cut at <see cref="LineBound"/> with an ellipsis.</summary>
    public static string Line(string text)
    {
        var line = text.Split('\n').Select(each => each.Trim()).FirstOrDefault(each => each.Length > 0) ?? "";
        return line.Length <= LineBound ? line : line[..(LineBound - 1)].TrimEnd() + "…";
    }
}
