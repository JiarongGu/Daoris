using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What <c>daoris-driver ask channel</c> was asked: the ask, and with words a post, to one repository or everyone (<paramref name="To"/>
/// null), answering a message or not; with no words, a read.
/// </summary>
public sealed record ChannelAsk(string Ask, string? To, string? Re, string? Text);

/// <summary>
/// The person's side of an ask's channel at a terminal (SESSONE2c, D167 points 6 to 8; the co-working design §2.4, D50), the twin
/// of the ask page's <i>Channel</i>: <c>daoris-driver ask channel &lt;id&gt; [--to &lt;repository&gt;] [--re &lt;message&gt;] ["…"]</c>.
/// </summary>
/// <remarks>
/// <para><b>The doors the page presses</b>: <c>GET /api/asks/{id}/channel</c>, <c>POST …/channel</c> and <c>POST …/channel/read</c>
/// (SESSONE2a), through <see cref="ServiceClient"/>, as the shell's <c>CHANNEL_*</c> routes use them. With no words it prints the
/// channel and moves the person's own mark through the newest item, since the person read it here; with words it posts them as the
/// person's, which every session on the ask reads (D167 point 7).</para>
///
/// <para><b>Daoris's lines are coded by the service</b> and worded here in English, as every terminal sentence is; the page words
/// the same codes in the reader's language (D142).</para>
///
/// <para><b>Exit codes</b>: 0 read or posted, 1 refused (a code the service answered), 2 the usage or the service.</para>
/// </remarks>
public static class ChannelCommand
{
    /// <summary>The verb's usage, as the host prints it beside a problem.</summary>
    public const string Usage = "ask channel <id> [--to <repository>] [--re <message>] [\"…\"]";

    /// <summary>Whether the words after <c>ask</c> are this verb's: <c>ask channel …</c>, before the ask's other words.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["channel", ..];

    /// <summary>The words after <c>ask channel</c> read, or null with why not: the ask's id, the flags, then the person's words.</summary>
    public static ChannelAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        string? ask = null, to = null, re = null;
        var words = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--to" or "--re":
                    if (i + 1 >= args.Count)
                    {
                        problem = $"{args[i]} needs a value.";
                        return null;
                    }

                    if (args[i] == "--to") to = args[++i];
                    else re = args[++i];
                    break;
                default:
                    if (ask is null) ask = args[i].TrimStart('#');
                    else words.Add(args[i]);
                    break;
            }
        }

        if (string.IsNullOrEmpty(ask) || ask.StartsWith('-'))
        {
            problem = "ask channel takes the ask's id first.";
            return null;
        }

        var text = string.Join(' ', words).Trim();
        if (text.Length == 0 && (to ?? re) is not null)
        {
            problem = "--to and --re go with the words to post, not with a read.";
            return null;
        }

        return new ChannelAsk(ask, to, re, text.Length == 0 ? null : text);
    }

    /// <summary>Read the channel, or post to it, and print what the service answered.</summary>
    public static Task<int> RunAsync(ChannelAsk ask, ServiceClient service, TextWriter output, CancellationToken ct = default) =>
        ask.Text is { } text ? PostAsync(ask, text, service, output, ct) : ReadAsync(ask, service, output, ct);

    private static async Task<int> ReadAsync(ChannelAsk ask, ServiceClient service, TextWriter output, CancellationToken ct)
    {
        if (await service.ChannelAsync(ask.Ask, null, ct).ConfigureAwait(false) is not { } channel)
        {
            output.WriteLine(Refusal("not-found", ask));
            return 1;
        }

        if (channel.Items.Count == 0)
        {
            output.WriteLine($"There is nothing on ask #{ask.Ask}'s channel yet.");
            return 0;
        }

        output.WriteLine($"Ask #{ask.Ask}'s channel, {channel.Unread} unread:");
        foreach (var item in channel.Items) output.WriteLine($"  {Line(item)}");

        // The person read it here, so their mark moves as the page's does; a late arrival after it stays unread.
        if (channel.Newest is { } newest) await service.ChannelMarkAsync(ask.Ask, newest, ct).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> PostAsync(ChannelAsk ask, string text, ServiceClient service, TextWriter output, CancellationToken ct)
    {
        var posted = await service.ChannelPostAsync(ask.Ask, text, ask.To, ask.Re, ct).ConfigureAwait(false);
        if (!posted.Ok)
        {
            output.WriteLine(Refusal(posted.Code!, ask));
            return 1;
        }

        output.WriteLine($"Posted to ask #{ask.Ask}'s channel, for {ask.To ?? "everyone"}.");
        return 0;
    }

    /// <summary>A refusal's code in a sentence; a code this build does not know is said as the service gave it.</summary>
    public static string Refusal(string code, ChannelAsk ask) => code switch
    {
        "empty" => "Nothing was posted: the words are empty.",
        "too-long" => "Nothing was posted: a message is at most 4,000 characters.",
        "bad-to" => $"Nothing was posted: `{ask.To}` is not one of the ask's repositories.",
        "bad-re" => $"Nothing was posted: the channel holds no message to answer `{ask.Re}`.",
        "not-found" => $"No ask #{ask.Ask} here.",
        _ => $"The service refused it: {code}.",
    };

    /// <summary>One item as a line: when, who, to whom, what it answers, then the words or Daoris's coded line.</summary>
    public static string Line(ChannelItemView item)
    {
        var at = item.At.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (item.Kind == ChannelItemView.Line) return $"{at}  Daoris: {Coded(item)}";

        var who = item.Person ? "You" : string.Join(" · ", new[] { item.Repository, item.Agent }.Where(part => !string.IsNullOrEmpty(part)));
        var marks = new List<string>();
        if (item.Question && item.Open) marks.Add("open question");
        else if (item.Question) marks.Add("question");
        if (item.Re is not null) marks.Add($"re {item.Re}");
        var tail = marks.Count == 0 ? "" : $" ({string.Join(", ", marks)})";
        return $"{at}  {(who.Length == 0 ? "A session" : who)} → {item.To ?? "everyone"}{tail}: {item.Text}";
    }

    private static string Coded(ChannelItemView line)
    {
        var quest = $"`{line.Name}` (quest {line.Quest})";
        return line.Code switch
        {
            "published" => $"published {quest} for {line.To}",
            "taken" => $"{line.To} took {quest}",
            "done" => $"{line.To} finished {quest}",
            "declined" => $"{line.To} declined {quest}",
            "awaiting" => $"{quest} waits on your yes",
            _ => $"{line.Code} {quest}",
        };
    }
}
