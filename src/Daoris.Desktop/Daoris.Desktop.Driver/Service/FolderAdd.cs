using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Adding a folder that is no git checkout as a department, from the terminal: the registration the window's add sends (its kind, its
/// root to a service on this machine only, the person's own excludes and what the folder's own <c>daoris.json</c> declares).
/// </summary>
public static class FolderAdd
{
    /// <summary>What the add would send for the folder at <paramref name="root"/> to <paramref name="service"/>.</summary>
    /// <exception cref="RegistryRefusal">The folder is a git checkout, which <c>connect</c> registers, or its declaration does not read.</exception>
    public static ConnectPlan Plan(string root, string? workspace, IReadOnlyList<string> excludes, string service)
    {
        if (!Directory.Exists(root))
        {
            throw new RegistryRefusal(RegistryRefusal.ManifestUnreadable, $"{root} is not a folder on this machine, so there is nothing to register.", 1);
        }

        if (PlaceKind.At(root) == PlaceKind.Git)
        {
            throw new RegistryRefusal(
                RegistryRefusal.ManifestUnreadable,
                "this is a git checkout: register it with `daoris repository connect`, which reads its line. `daoris repository add` is for a folder that is no repository.",
                1);
        }

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        var local = RegistryConnect.IsLocal(service);
        var body = Declared(name, FolderDeclarationReader.Read(root), local ? root : null);
        body["kind"] = PlaceKind.Folder;
        if (local) body["root"] = root;
        if (!string.IsNullOrEmpty(workspace)) body["workspace"] = workspace;
        if (excludes.Count > 0) body["excludes"] = new JsonArray([.. excludes.Select(glob => (JsonNode)JsonValue.Create(glob))]);
        return new ConnectPlan(name, service, body);
    }

    private static JsonObject Declared(string name, LineFiles files, string? root)
    {
        if (files.FileProblem is { } odd) throw new RegistryRefusal(RegistryRefusal.ManifestUnreadable, odd, 2);
        if (files.Manifest is null) return new JsonObject { ["repository"] = name, ["adopted"] = false };
        var composed = LineRegistration.Compose(name, files.Manifest, files.Lanes, root, localService: root is not null);
        if (composed.ManifestProblem is { } problem) throw new RegistryRefusal(RegistryRefusal.ManifestUnreadable, problem, 2);
        if (composed.LanesProblems.Count > 0)
        {
            throw new RegistryRefusal(RegistryRefusal.LanesUnreadable, $"{DeclarationNames.Lanes} cannot be read: {string.Join("; ", composed.LanesProblems)}", 1);
        }

        var body = composed.Body!;
        if (!composed.Declares) body["adopted"] = false;
        return body;
    }
}
