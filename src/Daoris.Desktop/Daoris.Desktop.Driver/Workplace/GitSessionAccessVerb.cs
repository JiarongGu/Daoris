using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris git access --session &lt;id&gt;</c>: what git access a running session holds, each class with its value and where
/// it comes from, and with <c>write=deny</c> and the like the person's <i>Tighten…</i> from the terminal. Only narrows, so no widening
/// is pressed through the window; a session's own environment is refused before it gets here (<see cref="GitAccessCommand"/>).
/// </summary>
internal static class GitSessionAccessVerb
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Say, or tighten and say, the access <paramref name="session"/> holds.</summary>
    /// <exception cref="DriverException">No such session record, or a tightening that does not narrow.</exception>
    public static int Run(string home, string session, IReadOnlyList<KeyValuePair<string, string?>> set, bool json, TextWriter output)
    {
        var record = set.Count == 0
            ? GitSessionAccess.Read(home, session) ?? throw new DriverException($"no running session {session} holds git access here.")
            : GitSessionAccess.Tighten(home, session, set.ToDictionary(each => each.Key, each => each.Value ?? "", StringComparer.Ordinal));
        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(record.Classes.Select(each => new { each.Class, each.Value, each.Source }), JsonOptions));
            return 0;
        }

        if (set.Count > 0) output.WriteLine("git: this session's git access is tightened.");
        foreach (var each in record.Classes) output.WriteLine($"{each.Class,-10} {GitAccessCommand.Said(each)}");
        return 0;
    }
}
