namespace Daoris.Driver;

/// <summary><c>ask through &lt;id&gt;</c>: the ask, and the person's words, or none to take theirs back.</summary>
public sealed record AskThroughAsk(string Ask, string? Words);

/// <summary>
/// <c>daoris ask through &lt;id&gt; "&lt;the person's words&gt;"|--clear</c>: the person's through words on an ask already made,
/// which each run of its quests reads live, or their take-back. The person's press of the running Daoris (<c>ASK_THROUGH</c>), confirmed in
/// its window, or Ask Daoris's, whose words are judged against the person's own messages in its conversation; alone where none runs.
/// </summary>
/// <remarks>Exit codes: 0 kept, 1 refused (no ask, a closed ask, words past their bound or never said), 2 the usage.</remarks>
public static class AskThroughCommand
{
    /// <summary>The words read, or null with the problem: words, or <c>--clear</c>, never both and never neither.</summary>
    public static AskThroughAsk? Read(string ask, IReadOnlyList<string> words, bool clear, out string? problem)
    {
        problem = null;
        var said = string.Join(' ', words).Trim();
        if (clear == said.Length > 0)
        {
            problem = clear
                ? "`ask through <id> --clear` takes the words back, so it takes no words beside it."
                : "`ask through <id>` takes the person's words asking for the work to be delivered, or --clear to take them back.";
            return null;
        }

        return new AskThroughAsk(ask.Trim().TrimStart('#'), clear ? null : said);
    }

    public static Task<int> PressAsync(AskThroughAsk ask, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.AskThrough, InstanceDoor.Said(("ask", ask.Ask), ("words", ask.Words)),
            () => RunAsync(ask, service, output, ct), output, ct);

    /// <summary>Keep the words on the ask, or take them back, and print the service's sentence.</summary>
    public static async Task<int> RunAsync(AskThroughAsk ask, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        var answer = await service.ChooseAskThroughAsync(ask.Ask, ask.Words, ct).ConfigureAwait(false);
        output.WriteLine(answer.Message);
        return answer.Ok ? 0 : 1;
    }
}
