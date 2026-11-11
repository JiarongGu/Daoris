using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris repository excludes &lt;folder&gt; --add|--remove &lt;glob&gt;</c> as the card shows it: the files a folder department's manifest
/// passes by, changed in one row of this machine's registry, and its manifest read again with them. Refused before any card where the
/// name is no registered folder or the edit would change nothing; shown with the change (each glob added with a plus, taken back with a minus) and what the folder then leaves out, or that it leaves out nothing of its own.
/// </summary>
public sealed class FolderExcludesView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "REGISTRY_EXCLUDES";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var named = PressPayload.Text(payload, "repository");
        if (named.Length == 0) return PressViewed.Refused("an excludes change names the folder, as Repositories lists it.");
        var (add, remove) = (Globs(payload, "add"), Globs(payload, "remove"));
        if (add.Count == 0 && remove.Count == 0) return PressViewed.Refused($"an excludes change names a glob to add or to take back in `{named}`.");

        var registry = await world.RegistryAsync(cancellationToken).ConfigureAwait(false);
        if (registry.FirstOrDefault(row => string.Equals(row.Repository, named, StringComparison.OrdinalIgnoreCase)) is not { } found)
        {
            return PressViewed.Refused($"`{named}` is not registered on this machine — use a folder's name as Repositories lists it.");
        }

        if (found.Kind != PlaceKind.Folder) return PressViewed.Refused($"`{found.Repository}` is not a folder: excludes are a folder department's.");
        var before = found.Excludes ?? [];
        var after = ExcludesEdit.Apply(before, add, remove);
        if (after.SequenceEqual(before, StringComparer.Ordinal)) return PressViewed.Refused($"`{found.Repository}` already leaves out exactly that, so there is nothing to change.");

        var command = string.Join(' ', ["daoris repository excludes", ShellWord.Of(found.Repository, ShellWord.Name),
            .. add.Select(glob => $"--add {ShellWord.Of(glob, "<glob>")}"), .. remove.Select(glob => $"--remove {ShellWord.Of(glob, "<glob>")}")]);
        var change = string.Join(", ", [.. add.Select(glob => $"+ {glob}"), .. remove.Select(glob => $"- {glob}")]);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["repository"] = found.Repository, ["change"] = change };
        if (after.Count == 0) return PressViewed.Shown(new PressView("folder-excludes-none", facts, command));
        facts["now"] = string.Join(", ", after);
        return PressViewed.Shown(new PressView("folder-excludes", facts, command));
    }

    private static IReadOnlyList<string> Globs(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!.Trim()).Where(each => each.Length > 0)]
            : [];
}
