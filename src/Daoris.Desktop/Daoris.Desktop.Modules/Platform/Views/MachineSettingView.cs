using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// One of the machine's dials as the card shows it: <c>daoris settings notify</c> (<c>SET_NOTIFY</c>), <c>cap</c> (<c>SET_CAP</c>),
/// <c>cooloff</c> (<c>SET_COOLOFF</c>), <c>quiet</c> (<c>SET_QUIET</c>) and <c>manifest</c> (<c>SET_MANIFEST</c>), one bound a press. A value the route would refuse is refused at the ask in the
/// driver's own words, before any card.
/// </summary>
public sealed class MachineSettingView : IPressView
{
    private readonly Func<JsonElement?, PressViewed> _judge;

    private MachineSettingView(string route, Func<JsonElement?, PressViewed> judge)
    {
        (Route, _judge) = (route, judge);
    }

    public static MachineSettingView Notify() => new("SET_NOTIFY", payload =>
    {
        var on = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("notify", out var flag) && flag.ValueKind == JsonValueKind.True;
        return Shown(on ? "notify-on" : "notify-off", [], $"daoris settings notify {(on ? "on" : "off")}");
    });

    public static MachineSettingView Cap() => new("SET_CAP", payload =>
    {
        if (Number(payload, "cap") is not { } cap || cap < 1) return PressViewed.Refused(DriverConfig.CapRefusal);
        var sessions = cap.ToString(CultureInfo.InvariantCulture);
        return Shown(cap == 1 ? "cap-one" : "cap", new() { ["sessions"] = sessions }, $"daoris settings cap {sessions}");
    });

    public static MachineSettingView CoolOff() => new("SET_COOLOFF", payload =>
    {
        if (Number(payload, "minutes") is not { } minutes || minutes < 1) return PressViewed.Refused(DriverConfig.CoolOffRefusal);
        var text = minutes.ToString(CultureInfo.InvariantCulture);
        return Shown("cooloff", new() { ["minutes"] = text }, $"daoris settings cooloff {text}");
    });

    public static MachineSettingView Quiet() => new("SET_QUIET", payload =>
    {
        if (Number(payload, "minutes") is not { } minutes || !QuietSetting.Takes(minutes)) return PressViewed.Refused(QuietSetting.Refusal);
        var text = minutes.ToString(CultureInfo.InvariantCulture);
        return Shown("quiet", new() { ["minutes"] = text }, $"daoris settings quiet {text}");
    });

    public static MachineSettingView Manifest() => new("SET_MANIFEST", payload =>
    {
        var (files, minutes, copy) = (Number(payload, "files"), Number(payload, "minutes"), Number(payload, "copy"));
        if (new[] { files, minutes, copy }.Count(value => value is not null) != 1) return PressViewed.Refused(ManifestSetting.OneRefusal);
        if (copy is { } megabytes)
        {
            if (!ManifestSetting.TakesCopy(megabytes)) return PressViewed.Refused(ManifestSetting.CopyRefusal);
            var bound = megabytes.ToString(CultureInfo.InvariantCulture);
            return Shown("manifest-copy", new() { ["copy"] = bound }, $"daoris settings manifest copy {bound}");
        }

        if (files is { } count)
        {
            if (!ManifestSetting.TakesFiles(count)) return PressViewed.Refused(ManifestSetting.FilesRefusal);
            var text = count.ToString(CultureInfo.InvariantCulture);
            return Shown("manifest-files", new() { ["files"] = text }, $"daoris settings manifest files {text}");
        }

        if (!ManifestSetting.TakesMinutes(minutes!.Value)) return PressViewed.Refused(ManifestSetting.MinutesRefusal);
        var time = minutes.Value.ToString(CultureInfo.InvariantCulture);
        return Shown("manifest-minutes", new() { ["minutes"] = time }, $"daoris settings manifest minutes {time}");
    });

    public string Module => DriverModule.Name;

    public string Route { get; }

    public Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken) =>
        Task.FromResult(_judge(payload));

    private static long? Number(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : null;

    private static PressViewed Shown(string kind, Dictionary<string, string> facts, string command) =>
        PressViewed.Shown(new PressView(kind, facts, command));
}
