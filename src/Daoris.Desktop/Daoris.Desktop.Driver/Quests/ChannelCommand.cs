using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What <c>daoris-driver ask channel</c> was asked: the ask, and with words a post, to one repository or everyone (<paramref name="To"/>
/// null), answering a message or not; with no words, a read. A post is urgent, the person's default (D188 point 2), unless
/// <paramref name="Ordinary"/> asked it to wait for each turn's end (COWORK1e).
/// </summary>
public sealed record ChannelAsk(string Ask, string? To, string? Re, string? Text, bool Ordinary = false);

/// <summary>
/// The person's side of an ask's channel at a terminal (SESSONE2c, D167 points 6 to 8; the co-working design §2.4, D50), the twin
/// of the ask page's <i>Channel</i>: <c>daoris-driver ask channel &lt;id&gt; [--to &lt;repository&gt;] [--re &lt;message&gt;]
/// [--ordinary] ["…"]</c>.
/// </summary>
/// <remarks>
/// <para><b>The level and the receipts</b> (COWORK1e; D188 points 2 and 6; the message-urgency design §8.4, §8.6): a post is the
/// person's, urgent by default, and <c>--ordinary</c> posts it for each turn's end; <c>--urgent</c>, the design's spelling, says
/// the default aloud. A read names an urgent message's level, and under the person's read each message says what it reached in
/// each repository it addresses, as the service judged it and the page words it.</para>
///
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
    public const string Usage = "ask channel <id> [--to <repository>] [--re <message>] [--ordinary] [\"…\"]";

    /// <summary>Whether the words after <c>ask</c> are this verb's: <c>ask channel …</c>, before the ask's other words.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["channel", ..];

    /// <summary>The words after <c>ask channel</c> read, or null with why not: the ask's id, the flags, then the person's words.</summary>
    public static ChannelAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        string? ask = null, to = null, re = null, level = null;
        var words = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--ordinary" or "--urgent":
                    if (level is not null && level != args[i])
                    {
                        problem = "--ordinary and --urgent are two levels: a post takes one.";
                        return null;
                    }

                    level = args[i];
                    break;
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
        if (text.Length == 0 && (to ?? re ?? level) is not null)
        {
            problem = "--to, --re, --ordinary and --urgent go with the words to post, not with a read.";
            return null;
        }

        return new ChannelAsk(ask, to, re, text.Length == 0 ? null : text, level == "--ordinary");
    }

    /// <summary>Read the channel, or post to it, and print what the service answered.</summary>
    public static Task<int> RunAsync(ChannelAsk ask, ServiceClient service, TextWriter output, CancellationToken ct = default) =>
        RunAsync(ask, service, output, instance: null, ct);

    /// <summary>
    /// The verb, as the running Daoris's client where <paramref name="instance"/> names one to look for (RPC1e, the terminal RPC design
    /// §7.2): a post is the person's words, which the window confirms, its route asked for this verb's own lines. A read stays the
    /// service's: it moves the person's own mark, a door the service writes once whoever asks.
    /// </summary>
    public static Task<int> RunAsync(ChannelAsk ask, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        ask.Text is { } text
            ? InstanceDoor.ThroughAsync(
                instance, PersonPresses.ChannelPost,
                InstanceDoor.Said(("ask", ask.Ask), ("text", text), ("to", ask.To), ("re", ask.Re), ("ordinary", ask.Ordinary ? true : null)),
                () => PostAsync(ask, text, service, output, ct), output, ct)
            : ReadAsync(ask, service, output, ct);

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
        foreach (var item in channel.Items)
        {
            output.WriteLine($"  {Line(item)}");
            if (Reached(item) is { } reached) output.WriteLine($"      {reached}");
        }

        // The person read it here, so their mark moves as the page's does; a late arrival after it stays unread.
        if (channel.Newest is { } newest) await service.ChannelMarkAsync(ask.Ask, newest, ct).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> PostAsync(ChannelAsk ask, string text, ServiceClient service, TextWriter output, CancellationToken ct)
    {
        var posted = await service.ChannelPostAsync(ask.Ask, text, ask.To, ask.Re, ct, urgent: !ask.Ordinary).ConfigureAwait(false);
        if (!posted.Ok)
        {
            output.WriteLine(Refusal(posted.Code!, ask));
            return 1;
        }

        // The level the service kept, never the one asked: a host from before keeps none, and the line says nothing of it.
        var level = posted.Message?.Urgent switch
        {
            true => ", urgent: each session it reaches reads it at its next step",
            false => ", ordinary: each session it reaches reads it when its turn ends",
            null => "",
        };
        output.WriteLine($"Posted to ask #{ask.Ask}'s channel, for {ask.To ?? "everyone"}{level}.");
        return 0;
    }

    /// <summary>
    /// What a message reached in each repository it addresses (COWORK1d's receipts, the design §8.4), worded as the page's
    /// <c>asks.channel.receipt.*</c>; null for a line, an entry's read and a host from before, which answer none.
    /// </summary>
    public static string? Reached(ChannelItemView item) =>
        item.Receipts is { Count: > 0 } receipts
            ? $"What it reached: {string.Join("; ", receipts.Select(Receipt))}."
            : null;

    private static string Receipt(ChannelReceiptView receipt) => receipt.State switch
    {
        "waiting" => $"waiting for {receipt.Repository}",
        "held" => $"held for {receipt.Repository}'s session",
        "sent" => $"sent to {receipt.Repository}",
        "taken" => $"taken by {receipt.Repository}",
        "read" => $"read by {receipt.Repository}",
        "partly" => $"partly handed to {receipt.Repository}",
        "answered" => $"answered by {receipt.Repository}",
        "not-reached" => $"it never reached {receipt.Repository}'s session: the ask's work ended first",
        _ => $"{receipt.Repository}: {receipt.State}",
    };

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

    /// <summary>
    /// One item as a line: when, who, to whom, its level where it is urgent (an ordinary or older one is never marked, as on the page),
    /// what it answers, then the words or Daoris's coded line.
    /// </summary>
    public static string Line(ChannelItemView item)
    {
        var at = item.At.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (item.Kind == ChannelItemView.Line) return $"{at}  Daoris: {Coded(item)}";

        var who = item.Person ? "You" : string.Join(" · ", new[] { item.Repository, item.Agent }.Where(part => !string.IsNullOrEmpty(part)));
        var marks = new List<string>();
        if (item.Urgent == true) marks.Add("urgent");
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
