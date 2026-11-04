namespace Daoris.Driver;

/// <summary>
/// The person's presses through the running Daoris: each is sent to the instance, which shows it in its window and runs it once on
/// the person's Confirm, the verb saying it waits and then the lines the route answers (<c>said</c>, the alone path's own words);
/// alone where no instance runs. A verb that holds a request-folder wait keeps that wait on its alone path, and the instance runs the
/// same path itself.
/// </summary>
internal static class SessionsPresses
{
    /// <summary>A press's payload: the route's own fields beside <c>said: true</c>, which asks for the verb's lines; an absent value is left out.</summary>
    internal static Dictionary<string, object?> Body(params (string Name, object? Value)[] fields)
    {
        var body = new Dictionary<string, object?>();
        foreach (var (name, value) in fields)
        {
            if (value is not null) body[name] = value;
        }

        body["said"] = true;
        return body;
    }

    /// <summary>A take-back's or a reword's payload: the record's word under its session, the new words only for a reword.</summary>
    internal static Dictionary<string, object?> QueueEditBody(SessionsAsk ask) =>
        Body(("session", ask.Ids[0]), ("id", ask.Word), ("text", ask.Verb == "reword" ? ask.Text : null));

    internal static async Task<int> ThroughAsync(
        SessionsWorld world, InstanceRoute route, object payload, Func<Task<int>> alone, TextWriter output, CancellationToken ct) =>
        world.Instance is { } instance
            ? await InstanceDoor.PressAsync(route, payload, alone, TreesSaid.Words(output, instance.Error, "sessions"), instance, ct).ConfigureAwait(false)
            : await alone().ConfigureAwait(false);
}
