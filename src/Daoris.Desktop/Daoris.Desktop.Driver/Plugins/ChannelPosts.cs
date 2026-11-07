using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A channel's message kept, told as <see cref="WorkEventKinds.ChannelPosted"/>: the
/// journal's <c>channel</c> row of change <c>message</c>, its poster's class read from the poster's id as the doors read it, never from a
/// name. A message of another machine's (a mirror) is told without its id, which is not this machine's to name.
/// </summary>
public static class ChannelPosts
{
    /// <summary>The class codes of <c>by</c>.</summary>
    public const string Person = "person";

    /// <summary>A part of the line: a session.</summary>
    public const string Part = "part";

    /// <summary>A member outside Daoris, read from the id's shape.</summary>
    public const string Outside = "outside";

    /// <summary>A teammate's, mirrored from another machine.</summary>
    public const string Teammate = "teammate";

    /// <summary>The four classes of <c>by</c>.</summary>
    public static readonly IReadOnlyList<string> Classes = [Person, Part, Outside, Teammate];

    private static readonly Regex MemberShape = new(@"\Ao[0-9a-f]{7}\z", RegexOptions.CultureInvariant);

    /// <summary>Whether <paramref name="id"/> is a member's, minted as <c>o</c> and seven hex characters.</summary>
    public static bool IsMember(string? id) => id is not null && MemberShape.IsMatch(id);

    /// <summary>The event a journal row is, or null where it is no message kept.</summary>
    public static WorkEvent? Of(JournalEntry row)
    {
        if (row is not { Family: "channel", Change: "message" }) return null;
        var mirrored = row.Marks.Contains("mirrored", StringComparer.Ordinal);
        var poster = row.FromSession is { Length: > 0 } session ? session : null;
        var by = IsMember(poster) ? Outside : mirrored ? Teammate : poster is null ? Person : Part;
        return new WorkEvent(WorkEventKinds.ChannelPosted, DateTimeOffset.FromUnixTimeMilliseconds(row.At))
        {
            Message = mirrored ? null : row.Message,
            Ask = row.Ask,
            Poster = poster,
            By = by,
            Urgent = row.Marks.Contains("urgent", StringComparer.Ordinal) ? true : null,
        };
    }
}
