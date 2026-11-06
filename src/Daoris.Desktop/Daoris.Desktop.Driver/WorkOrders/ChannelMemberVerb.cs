using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>
/// What an agent outside Daoris asked of <c>ask channel</c>: <see cref="Join"/> an ask's channel or a line's (<paramref name="Line"/>, a
/// session of it), claiming an <paramref name="Agent"/> and a <paramref name="Repository"/>; or, as the <paramref name="Member"/> it was
/// admitted as, <see cref="Post"/> words, <see cref="Read"/> the channel (after <paramref name="Since"/> where given) or <see cref="Leave"/>.
/// </summary>
public sealed record ChannelMemberAsk(
    string Act, string? Ask = null, string? Line = null, string? Member = null, string? Agent = null, string? Repository = null,
    string? Text = null, string? To = null, string? Re = null, bool Question = false, bool Urgent = false, DateTimeOffset? Since = null)
{
    public const string Join = "join";
    public const string Post = "post";
    public const string Read = "read";
    public const string Leave = "leave";
}

/// <summary>
/// The member's forms of the one channel verb (CHANNELJOIN1b; the event-channel design §5.3 to §5.5): <c>ask channel join &lt;ask&gt;</c> or
/// <c>--line &lt;session&gt;</c>, sent to the running Daoris as the person's press <c>CHANNEL_JOIN</c>, which waits for their Confirm and is
/// refused before any card from a process in an agents' job; then <c>ask channel --member &lt;id&gt; ["…"]</c> and <c>ask channel leave
/// --member &lt;id&gt;</c>, the member's own doors of the service, which carry no key: its posts are its claims, never the person's.
/// </summary>
/// <remarks>
/// <para><b>No lease</b> (<see cref="VerbLeases"/>): each form's one write is the service's.</para>
/// <para><b>Exit codes</b>: 0 joined, posted, read or left; 1 refused (a code the service answered); 2 the usage, the service, or a join
/// while Daoris is closed.</para>
/// </remarks>
public static class ChannelMemberVerb
{
    /// <summary>The member's forms, as the host prints them beside a problem.</summary>
    public const string Usage =
        "ask channel join <ask> | --line <session> [--agent <name>] [--repository <name>]\n"
        + "       daoris ask channel --member <id> [\"…\"] [--to <repository>] [--re <message>] [--question] [--urgent] [--since <moment>]\n"
        + "       daoris ask channel leave --member <id>";

    /// <summary>Whether the words after <c>ask</c> are a member's form: <c>channel join</c>, <c>channel leave</c>, or any naming <c>--member</c>.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["channel", "join" or "leave", ..] || (args is ["channel", ..] && args.Contains("--member"));

    /// <summary>The words after <c>ask</c> read, or null with why not.</summary>
    public static ChannelMemberAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        var act = args.Count > 1 && args[1] is "join" or "leave" ? args[1] : null;
        string? ask = null, line = null, member = null, agent = null, repository = null, to = null, re = null, since = null;
        bool question = false, urgent = false;
        var words = new List<string>();
        for (var i = act is null ? 1 : 2; i < args.Count; i++)
        {
            var flag = args[i];
            if (flag is "--question") { question = true; continue; }
            if (flag is "--urgent") { urgent = true; continue; }
            if (flag is "--line" or "--member" or "--agent" or "--repository" or "--to" or "--re" or "--since")
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    problem = $"{flag} needs a value.";
                    return null;
                }

                var value = args[++i];
                switch (flag)
                {
                    case "--line": line = value; break;
                    case "--member": member = value; break;
                    case "--agent": agent = value; break;
                    case "--repository": repository = value; break;
                    case "--to": to = value; break;
                    case "--re": re = value; break;
                    default: since = value; break;
                }

                continue;
            }

            if (act == ChannelMemberAsk.Join && ask is null) ask = flag.TrimStart('#');
            else words.Add(flag);
        }

        var text = string.Join(' ', words).Trim();
        if (act == ChannelMemberAsk.Join)
        {
            if ((ask is null) == (line is null))
            {
                problem = "ask channel join takes an ask's id or --line <session>, one of them.";
                return null;
            }

            if (member is not null || text.Length > 0)
            {
                problem = "a join names its channel, its agent and its repository: the member's id is the person's answer, and words come after.";
                return null;
            }

            return new ChannelMemberAsk(ChannelMemberAsk.Join, Ask: ask, Line: line, Agent: agent, Repository: repository);
        }

        if (member is null)
        {
            problem = "the member's forms name it: --member <id>, the id the join printed.";
            return null;
        }

        if (act == ChannelMemberAsk.Leave) return new ChannelMemberAsk(ChannelMemberAsk.Leave, Member: member);
        if (text.Length == 0)
        {
            if (to is not null || re is not null || question || urgent)
            {
                problem = "--to, --re, --question and --urgent go with the words to post, not with a read.";
                return null;
            }

            DateTimeOffset? after = null;
            if (since is not null)
            {
                if (!DateTimeOffset.TryParse(since, System.Globalization.CultureInfo.InvariantCulture, out var moment))
                {
                    problem = "--since takes a moment, ISO 8601.";
                    return null;
                }

                after = moment;
            }

            return new ChannelMemberAsk(ChannelMemberAsk.Read, Member: member, Since: after);
        }

        return new ChannelMemberAsk(ChannelMemberAsk.Post, Member: member, Text: text, To: to, Re: re, Question: question, Urgent: urgent);
    }

    /// <summary>
    /// The form asked: a join through the running Daoris as the person's press (<paramref name="instance"/>), and alone, with no Daoris to
    /// confirm it, refused; a member's post, read or leave at its own doors of the service.
    /// </summary>
    public static Task<int> RunAsync(ChannelMemberAsk ask, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        ask.Act switch
        {
            ChannelMemberAsk.Join => InstanceDoor.ThroughAsync(
                instance, PersonPresses.ChannelJoin,
                InstanceDoor.Said(("ask", ask.Ask), ("line", ask.Line), ("agent", ask.Agent), ("repository", ask.Repository)),
                () => ClosedAsync(output), output, ct),
            ChannelMemberAsk.Post => PostAsync(ask, service, output, ct),
            ChannelMemberAsk.Leave => LeaveAsync(ask, service, output, ct),
            _ => ReadAsync(ask, service, output, ct),
        };

    /// <summary>
    /// The admission the shell presses once the person confirmed the join: the member's id, which the terminal prints with the two verbs
    /// it may run, and what its posts are.
    /// </summary>
    public static async Task<int> JoinAsync(ChannelMemberAsk ask, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        var joined = await service.JoinChannelAsync(new JoinChannel(ask.Ask, ask.Line, ask.Agent, ask.Repository), ct).ConfigureAwait(false);
        if (joined.Member is not { } member) return Refused(output, joined.Code!, ask);

        var channel = ask.Ask is { } id ? $"ask #{id}'s channel" : $"the channel of session {ask.Line}'s line";
        output.WriteLine($"You joined {channel} as member {member.Id} ({member.Agent}{(member.Repository is { } repository ? $", {repository}" : "")}).");
        output.WriteLine($"  Post:  daoris ask channel --member {member.Id} \"…\" [--to <repository>] [--re <message>] [--question] [--urgent]");
        output.WriteLine($"  Read:  daoris ask channel --member {member.Id} [--since <moment>]");
        output.WriteLine($"  Leave: daoris ask channel leave --member {member.Id}");
        output.WriteLine("Your posts are your own claims on the channel, never the person's: they start, stop and steer no session. Work for a repository is still a quest.");
        return 0;
    }

    private static async Task<int> ClosedAsync(TextWriter output)
    {
        await output.WriteLineAsync(
            "Daoris is not running, so no Confirm can be asked: a join waits for the person in Daoris's window. Nothing was asked.").ConfigureAwait(false);
        return 2;
    }

    private static async Task<int> PostAsync(ChannelMemberAsk ask, ServiceClient service, TextWriter output, CancellationToken ct)
    {
        var posted = await service.MemberPostAsync(ask.Member!, ask.Text!, ask.To, ask.Re, ask.Question, ask.Urgent, ct).ConfigureAwait(false);
        if (!posted.Ok) return Refused(output, posted.Code!, ask);
        var level = posted.Message?.Urgent switch
        {
            true => ", urgent: a running part takes it at its next step where its door takes words",
            false when ask.Urgent => ", ordinary: a member posts at most 3 urgent, so this one waits for a natural point",
            _ => ", ordinary: a running part takes it at a natural point",
        };
        output.WriteLine($"Posted as member {ask.Member}, for {ask.To ?? "everyone"}{level}.");
        return 0;
    }

    private static async Task<int> ReadAsync(ChannelMemberAsk ask, ServiceClient service, TextWriter output, CancellationToken ct)
    {
        var read = await service.MemberReadAsync(ask.Member!, ask.Since, ct).ConfigureAwait(false);
        if (read.Code is { } code) return Refused(output, code, ask);
        if (read.Items.Count == 0)
        {
            output.WriteLine("Nothing new on your channel.");
            return 0;
        }

        output.WriteLine($"Your channel, {read.Unread} unread:");
        foreach (var item in read.Items) output.WriteLine($"  {ChannelCommand.Line(item, person: "The person")}");
        return 0;
    }

    private static async Task<int> LeaveAsync(ChannelMemberAsk ask, ServiceClient service, TextWriter output, CancellationToken ct)
    {
        var left = await service.MemberLeaveAsync(ask.Member!, ct).ConfigureAwait(false);
        if (left.Member is null) return Refused(output, left.Code!, ask);
        output.WriteLine($"Member {ask.Member} left the channel: its messages keep their poster, and joining again is a new Confirm.");
        return 0;
    }

    /// <summary>A refusal's code in a sentence, exit 1; a code this build does not know is said as the service gave it.</summary>
    private static int Refused(TextWriter output, string code, ChannelMemberAsk ask)
    {
        output.WriteLine(code switch
        {
            "not-member" => $"Nothing was done: `{ask.Member}` is no member of a channel here, never admitted or gone. Joining again is a new Confirm and a new id.",
            "too-many" when ask.Act == ChannelMemberAsk.Join => "Nothing was admitted: that channel holds 3 outside agents already, or this machine 8.",
            "too-many" => "Nothing was posted: a member posts at most 10 messages.",
            "too-long" => "Nothing was posted: a message is at most 4,000 characters.",
            "empty" => "Nothing was posted: the words are empty.",
            "bad-to" => $"Nothing was posted: `{ask.To}` is not one of the channel's repositories.",
            "bad-re" => $"Nothing was posted: the channel holds no message to answer `{ask.Re}`.",
            "bad-part" => "Nothing was posted: a member is no part of the line, so it posts to everyone or one repository, never one session.",
            "helper" => "Nothing was asked: a helper's or a reviewer's line has no channel.",
            "no-session" => $"Nothing was asked: no session `{ask.Line}` is held here.",
            "not-found" => "Nothing was asked: no such ask or line here.",
            _ => $"The service refused it: {code}.",
        });
        return 1;
    }
}
