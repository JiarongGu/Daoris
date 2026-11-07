using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The steward's verb hearing that its act asks the owner: an act outside the acts the owner gave the steward waits for
/// the owner's Confirm in Daoris's window as the owner's own press, and confirmed it is the owner's, never the steward's.
/// </summary>
public sealed class StewardAsked(string id, TextWriter output)
{
    /// <summary>Whether the instance said the act waits for the owner.</summary>
    public bool Asked { get; private set; }

    /// <summary>The verb's notification reader: the line it says once the instance tells it the act waits for the owner's Confirm.</summary>
    public void Told(JsonElement told)
    {
        if (Asked || !told.TryGetProperty("payload", out var batch) || batch.ValueKind != JsonValueKind.Array) return;
        foreach (var each in batch.EnumerateArray())
        {
            if (InstanceClient.Text(each, "module") != InstanceClient.PipeModule || InstanceClient.Text(each, "type") != InstanceDoor.Confirming) continue;
            if (!each.TryGetProperty("payload", out var status) || InstanceClient.Text(status, "id") != id) continue;
            Asked = true;
            output.WriteLine(
                "steward: this act is not among the acts the owner gave the steward, so it waits for the owner's Confirm in Daoris's window "
                + $"(request {id}); confirmed, it is the owner's own press.");
            output.Flush();
            return;
        }
    }

    /// <summary>What the verb says of an act the owner is asked: confirmed and done as theirs, or not done.</summary>
    public string Ended(string act, bool done) => done
        ? $"steward: {act}: the owner confirmed it in Daoris's window; it is recorded as the owner's."
        : $"steward: {act}: the owner did not confirm it; nothing was done.";
}
