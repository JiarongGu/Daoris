using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place across</c> as the card shows it: whether a repository's or a workspace's checkout is read by agents outside it
/// (<c>SET_READ_ACROSS</c>, <c>read on|off|--clear</c>), and whether sessions in one repository may also write into another
/// (<c>SET_WRITE_ACROSS</c>, <c>write-to &lt;other&gt; [--clear]</c>). Refused before any card where a name is not registered here, or where the
/// driver's own edit refuses it (a repository naming itself).
/// </summary>
public sealed class PlaceAcrossView : IPressView
{
    private readonly bool _write;

    private PlaceAcrossView(bool write)
    {
        _write = write;
    }

    public static PlaceAcrossView Read() => new(false);

    public static PlaceAcrossView WriteTo() => new(true);

    public string Module => DriverModule.Name;

    public string Route => _write ? "SET_WRITE_ACROSS" : "SET_READ_ACROSS";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scoped = await PlaceScope.ReadAsync(world, payload, _write ? "a write-to relationship" : "reading across", !_write, cancellationToken)
            .ConfigureAwait(false);
        if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);
        return _write ? await WriteAsync(world, scoped, payload, cancellationToken).ConfigureAwait(false) : ReadOf(scoped, payload);
    }

    private static PressViewed ReadOf(PlaceScoped scoped, JsonElement? payload)
    {
        bool? read = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("read", out var sent) && sent.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? sent.ValueKind == JsonValueKind.True
            : null;
        var which = read switch { true => "on", false => "off", null => "clear" };
        var kind = (scoped.Circle ? "across-workspace-read-" : "across-read-") + which;
        return PressViewed.Shown(new PressView(kind, scoped.Facts(), $"daoris place across {scoped.Words} read {(read is null ? "--clear" : which)}"));
    }

    private static async Task<PressViewed> WriteAsync(IPipeGroundWorld world, PlaceScoped scoped, JsonElement? payload, CancellationToken cancellationToken)
    {
        var other = PressPayload.Text(payload, "to");
        if (other.Length == 0) return PressViewed.Refused("`across … write-to` names the repository sessions write into — `daoris place across <repository> write-to <other>`.");
        var allow = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("allow", out var flag) && flag.ValueKind == JsonValueKind.True;
        var registry = await world.RegistryAsync(cancellationToken).ConfigureAwait(false);
        var found = registry.FirstOrDefault(row => string.Equals(row.Repository, other, StringComparison.OrdinalIgnoreCase));
        if (found is null && allow)
        {
            return PressViewed.Refused($"`{other}` is not registered on this machine — use a repository's name as Repositories lists it.");
        }

        var target = found?.Repository ?? other;
        try
        {
            _ = DriverConfig.Empty.WithWriteAcross(scoped.Repository!, target, allow);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        var facts = scoped.Facts();
        facts["other"] = target;
        return PressViewed.Shown(new PressView(allow ? "across-write-to" : "across-write-clear", facts,
            $"daoris place across {scoped.Words} write-to {ShellWord.Of(target, ShellWord.Name)}{(allow ? "" : " --clear")}"));
    }
}
