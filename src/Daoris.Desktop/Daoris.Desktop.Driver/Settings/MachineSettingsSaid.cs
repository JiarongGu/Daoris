namespace Daoris.Driver;

/// <summary>
/// Each machine setting (<see cref="MachineSettings"/>) said with its value in <c>driver.json</c> and the verb that changes it: every one
/// for <c>daoris settings</c>, one for its verb named with no value, and each in <c>daoris place list</c>. A setting a verb above the
/// settings owns says its own lines (<see cref="ChoiceVerb.Listed"/>). A read; nothing is written.
/// </summary>
internal static class MachineSettingsSaid
{
    /// <summary>The word <c>daoris settings</c> with no verb reaches the choices' command by.</summary>
    public const string Every = "settings";

    public static int All(DriverConfig choices, DriverChoicesWorld world)
    {
        world.Instance.Out.WriteLine($"daoris: {world.ConfigPath}");
        foreach (var setting in MachineSettings.All) Say(setting.Key, choices, world);
        return 0;
    }

    /// <summary>The setting <paramref name="args"/> names with no value, said, and its exit; null where they name none so.</summary>
    public static int? One(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world)
    {
        if (args.Count != 1 || MachineSettings.Of(args[0]) is null) return null;
        world.Instance.Out.WriteLine($"daoris: {world.ConfigPath}");
        Say(args[0], choices, world);
        return 0;
    }

    public static void Say(string key, DriverConfig choices, DriverChoicesWorld world)
    {
        string Set(bool unset, string verb, string what = "it") => $"  ({(unset ? "the default" : "set")} — `daoris settings {verb}` changes {what})";
        var write = world.Instance.Out;
        switch (key)
        {
            case "notify":
                write.WriteLine($"  notify     {(choices.Notify ? "on" : "off")} — a session parking, or ending without you asking, is {(choices.Notify ? "" : "not ")}said"
                    + Set(choices.Notify, "notify on|off"));
                break;
            case "cooloff":
                write.WriteLine($"  cooloff    {choices.CoolOffMinutes ?? (int)CoolOffDefault.Value.TotalMinutes} minutes an account cools when its agent names no time for its limit"
                    + Set(choices.CoolOffMinutes is null, "cooloff <minutes>"));
                break;
            case "quiet":
                write.WriteLine($"  quiet      {choices.QuietMinutes ?? QuietSetting.Default} minutes before Daoris looks in on a session"
                    + Set(choices.QuietMinutes is null, "quiet <minutes>"));
                break;
            case "cap":
                write.WriteLine($"  cap        {choices.Cap} concurrent session(s)" + Set(choices.Cap == DriverConfig.Empty.Cap, "cap <n>"));
                break;
            case "manifest":
                var bounds = choices.Manifest.Bounds;
                write.WriteLine($"  manifest   {bounds.Files} files and {(int)bounds.Time.TotalMinutes} minutes before a folder is too large to follow"
                    + Set(choices.Manifest == ManifestSetting.Unset, "manifest files|minutes <n>", "either"));
                break;
            default:
                world.Verbs.FirstOrDefault(each => each.Word == key)?.Listed(choices, world);
                break;
        }
    }
}
