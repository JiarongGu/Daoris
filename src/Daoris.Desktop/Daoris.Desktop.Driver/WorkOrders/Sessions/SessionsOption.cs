using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>session option &lt;id&gt; &lt;option&gt; &lt;value&gt;</c>: one of a running conversation's options set, as its page's picker sets it,
/// pressed on <c>SET_SESSION_OPTION</c> so the route judges it: the driver refuses an option never offered and a conversation it does
/// not hold, the agent a value it does not take. A conversation's options are held by the Daoris that runs it, so with none running
/// nothing is set.
/// </summary>
internal static class SessionsOption
{
    /// <summary>What a terminal says where no Daoris answers: nothing holds the conversation whose option it names.</summary>
    public const string Alone =
        "session: a conversation's options are held by the Daoris that runs it, and none answers here: start Daoris, then set it again; nothing was set.";

    /// <summary>The press's body, the page's: the conversation, the option and its value.</summary>
    public static Dictionary<string, object?> Body(SessionsAsk ask) =>
        SessionsPresses.Body(("id", ask.Ids[0]), ("option", ask.Word), ("value", ask.Text));

    public static async Task<int> RunAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        if (world.Instance is not { } instance)
        {
            await output.WriteLineAsync(Alone).ConfigureAwait(false);
            return 1;
        }

        return await InstanceDoor.PressAsync(PersonPresses.SessionOption, Body(ask), () =>
        {
            output.WriteLine(Alone);
            return Task.FromResult(1);
        }, answer => Said(answer, ask, output, instance.Error), instance, ct).ConfigureAwait(false);
    }

    /// <summary>The option as the route's answer holds it now, in one line; an answer naming it nowhere is a form this terminal does not read.</summary>
    public static int Said(JsonElement answer, SessionsAsk ask, TextWriter output, TextWriter error)
    {
        var option = ask.Word;
        var now = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array
            ? options.EnumerateArray().FirstOrDefault(each => each.ValueKind == JsonValueKind.Object
                && each.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() == option)
            : default;
        if (now.ValueKind != JsonValueKind.Object || !now.TryGetProperty("current", out var current))
        {
            error.WriteLine($"session: Daoris answered in a form this terminal does not read: it names no option `{option}`.");
            return 2;
        }

        output.WriteLine($"session: `{option}` of {ask.Ids[0]} is now `{(current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText())}`.");
        return 0;
    }
}
