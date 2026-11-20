using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What the move's JSON stores share: the rename an agent's id has, a key renamed where it stood, and a file read and written whole.</summary>
internal static class AccountIdJson
{
    public static bool Same(string? one, string? other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>The rename of <paramref name="agent"/>'s <paramref name="id"/>, or null where it does not move.</summary>
    public static AccountIdRename? Of(IReadOnlyList<AccountIdRename> renames, string? agent, string? id) =>
        agent is null || id is null ? null : renames.FirstOrDefault(rename => Same(rename.Agent, agent) && Same(rename.From, id));

    /// <summary>The rename of the one agent whose <paramref name="id"/> moves, where only one agent's does: a record that names no agent.</summary>
    public static AccountIdRename? Only(IReadOnlyList<AccountIdRename> renames, string? id) =>
        renames.Where(rename => Same(rename.From, id)).ToList() is [var only] ? only : null;

    /// <summary>Each key of <paramref name="held"/> that names <paramref name="agent"/>, as written.</summary>
    public static IEnumerable<string> AgentKeys(JsonObject held, string agent) =>
        [.. held.Select(pair => pair.Key).Where(key => Same(key, agent))];

    /// <summary>The key <paramref name="from"/> renamed <paramref name="to"/> where it stood, every other key kept in its place.</summary>
    public static void RenameKey(JsonObject held, string from, string to)
    {
        var pairs = held.ToList();
        held.Clear();
        foreach (var (key, node) in pairs) held[key == from ? to : key] = node;
    }

    /// <summary>Whether <paramref name="node"/> is a string naming <paramref name="id"/>.</summary>
    public static bool Names(JsonNode? node, string id) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && Same(text, id);

    /// <summary>
    /// A JSON file walked: its root handed to <paramref name="edit"/>, and written back where anything is found and the walk writes, under
    /// the file's <see cref="KeepLock"/>. A missing file holds nothing; one that does not read stops the move before anything changes.
    /// </summary>
    public static IReadOnlyList<AccountIdReference> File(
        AccountIdHome at, string path, bool write, Func<JsonObject, string, List<AccountIdReference>> edit, bool make = false)
    {
        if (!make && !System.IO.File.Exists(path)) return [];
        using var held = write ? KeepLock.Take(path) : null;
        var (root, unreadable) = AccountFileNodes.Read(path);
        if (unreadable) throw new DriverException($"{at.Place(path)} does not read, so no account id was moved: mend or remove it, then run this again.");
        var found = edit(root, at.Place(path));
        if (write && found.Count > 0) JsonFile.Write(path, root);
        return found;
    }

    /// <summary>A count said with its noun, plural past one.</summary>
    public static string Counted(int count, string noun, string? plural = null) => count == 1 ? $"1 {noun}" : $"{count} {plural ?? noun + "s"}";
}
