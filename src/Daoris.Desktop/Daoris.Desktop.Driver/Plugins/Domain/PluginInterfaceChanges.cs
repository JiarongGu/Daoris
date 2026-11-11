namespace Daoris.Driver;

/// <summary>
/// An update's <c>interfaces</c> rows (the plugin UI design's §8): one per entry added, removed or changed, compared whole by its name
/// or id, each marked where it asks more of the person: a command gaining <c>tool</c>, <c>writes</c>, <c>adopts</c> or an argument, a
/// view gaining <c>context</c>, storage newly declared.
/// </summary>
public static class PluginInterfaceChanges
{
    public const string What = "interfaces";

    public static IEnumerable<PluginChange> Between(PluginInterfacesDeclared was, PluginInterfacesDeclared now)
    {
        foreach (var name in Names(was.Commands.Select(each => each.Name), now.Commands.Select(each => each.Name)))
        {
            var before = was.Commands.FirstOrDefault(each => each.Name == name);
            var after = now.Commands.FirstOrDefault(each => each.Name == name);
            if (Row(PluginInterfaces.Commands, name, Said(before), Said(after), AsksMore(before, after)) is { } row) yield return row;
        }

        if (Row(PluginInterfaces.Storage, null, Said(was.Storage), Said(now.Storage), was.Storage is null && now.Storage is not null) is { } storage)
            yield return storage;

        foreach (var id in Names(was.Views.Select(each => each.Id), now.Views.Select(each => each.Id)))
        {
            var before = was.Views.FirstOrDefault(each => each.Id == id);
            var after = now.Views.FirstOrDefault(each => each.Id == id);
            var told = after is not null && after.Context.Except(before?.Context ?? []).Any();
            if (Row(PluginInterfaces.Views, id, Said(before), Said(after), told) is { } row) yield return row;
        }
    }

    /// <summary>The names on either side, the installed order first.</summary>
    private static IEnumerable<string> Names(IEnumerable<string> was, IEnumerable<string> now) => was.Concat(now).Distinct(StringComparer.Ordinal);

    private static PluginChange? Row(string @interface, string? name, string was, string now, bool asksMore) =>
        was == now ? null : new(What, was, now) { Interface = @interface, Name = name, AsksMore = asksMore };

    private static bool AsksMore(PluginCommandDeclared? was, PluginCommandDeclared? now) =>
        now is not null
        && ((now.Tool && was?.Tool != true) || (now.Writes && was?.Writes != true) || (now.Adopts is not null && was?.Adopts is null)
            || now.Args.Any(arg => was?.Args.All(before => before.Name != arg.Name) ?? true));

    private static string Said(PluginCommandDeclared? command) => command is null ? "" : string.Join("; ", Parts(command));

    private static IEnumerable<string> Parts(PluginCommandDeclared command)
    {
        yield return $"{command.Name} {command.About.Json()}";
        if (command.Title.AsWritten != command.Name || command.Title.Tags.Count > 0) yield return $"title {command.Title.Json()}";
        if (command.Interactive) yield return "asks the person";
        if (command.Tool) yield return "for agents";
        if (command.Writes) yield return "changes things";
        if (command.Adopts is { } adopts) yield return $"takes a git identity's token for {string.Join(", ", adopts.Hosts)}";
        if (command.Args.Count > 0)
        {
            yield return "args " + string.Join(", ", command.Args.Select(arg =>
                $"{arg.Name} ({arg.Type}{(arg.Choices.Count > 0 ? ": " + string.Join("|", arg.Choices) : "")}{(arg.Required ? ", required" : "")})"));
        }

        if (command.On.Count > 0) yield return $"on {string.Join(", ", command.On)}";
        if (command.Confirm is { } confirm) yield return $"confirm {confirm.Json()}";
    }

    private static string Said(PluginStorageDeclared? storage) =>
        storage is null ? "" : storage.For.Json() + (storage.Forget is { } forget ? $"; forget {forget}" : "");

    private static string Said(PluginViewDeclared? view) => view is null ? "" : string.Join("; ", (IEnumerable<string>)
    [
        $"{view.Id} {view.Title.Json()}", string.Join(", ", view.Where), string.Join(", ", view.Places),
        .. view.Context.Count > 0 ? [$"told {string.Join(", ", view.Context)}"] : Array.Empty<string>(),
        .. view.Icon is { } icon ? [$"icon {icon}"] : Array.Empty<string>(),
    ]);
}
