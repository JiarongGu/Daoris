using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The gates a change passes before it moves its line, read from the declaration's own <c>admission</c> key: <c>always</c> and <c>suites</c>
/// run for every change, each <c>byPaths</c> check where a changed path matches one of its globs, or one of the declared wide paths
/// (<c>verifyBudget.fullRunReasons.widePaths</c>, which change how every gate runs). The merge tool selects its blocking gates from the same key,
/// so a run's check, which judges the work as merged before its landing, and a merge hold a change to one list.
/// </summary>
internal static class AdmissionGates
{
    /// <summary>
    /// The admitted gates in the order the declaration writes them; <paramref name="Missing"/> the names the admission lists that the declaration
    /// does not declare; <paramref name="Problem"/> why an admission that is written does not read, where no gate is admitted.
    /// </summary>
    internal sealed record Admitted(IReadOnlyList<DeclaredGate> Gates, IReadOnlyList<string> Missing, string? Problem = null);

    private sealed record PathCheck(string Gate, IReadOnlyList<string> Paths);

    private sealed record Reading(IReadOnlyList<string> Always, IReadOnlyList<PathCheck> Checks, IReadOnlyList<string> Wide);

    /// <summary>The gates of <paramref name="declared"/> that <paramref name="text"/>'s admission holds a change touching <paramref name="touched"/> to; none where it declares no admission.</summary>
    internal static Admitted Of(IReadOnlyList<DeclaredGate> declared, string? text, IReadOnlyCollection<string> touched)
    {
        if (string.IsNullOrWhiteSpace(text)) return new([], []);
        Reading read;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("admission", out var admission)) return new([], []);
            if (Read(admission, document.RootElement) is not { } reading) return new([], [], "the declaration's `admission` does not read: it needs `always` and `suites` (lists of gate names) and `byPaths` (a list of checks, each with its `gate` and `paths`)");
            read = reading;
        }
        catch (JsonException)
        {
            return new([], []);
        }

        var names = new HashSet<string>(read.Always, StringComparer.Ordinal);
        foreach (var check in read.Checks)
        {
            if (Reached(check.Paths, touched) || Reached(read.Wide, touched)) names.Add(check.Gate);
        }

        var admitted = declared.Where(gate => names.Contains(gate.Name)).ToList();
        return new(admitted, [.. names.Where(name => declared.All(gate => gate.Name != name)).Order(StringComparer.Ordinal)]);
    }

    private static bool Reached(IReadOnlyList<string> globs, IReadOnlyCollection<string> touched) =>
        touched.Any(path => globs.Any(glob => KindPaths.Matches(glob, path)));

    private static Reading? Read(JsonElement admission, JsonElement root)
    {
        if (admission.ValueKind != JsonValueKind.Object) return null;
        var always = Names(admission, "always");
        var suites = Names(admission, "suites");
        if (always is null || suites is null || !admission.TryGetProperty("byPaths", out var listed) || listed.ValueKind != JsonValueKind.Array) return null;
        var checks = new List<PathCheck>();
        foreach (var each in listed.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Text(each, "gate") is not { } gate || Names(each, "paths") is not { Count: > 0 } paths) return null;
            checks.Add(new PathCheck(gate, paths));
        }

        return new Reading([.. always, .. suites], checks, Wide(root));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static List<string>? Names(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return null;
        var names = new List<string>();
        foreach (var each in value.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.String || each.GetString() is not { Length: > 0 } text) return null;
            names.Add(text);
        }

        return names;
    }

    private static List<string> Wide(JsonElement root) =>
        root.TryGetProperty("verifyBudget", out var budget) && budget.ValueKind == JsonValueKind.Object
            && budget.TryGetProperty("fullRunReasons", out var reasons) && reasons.ValueKind == JsonValueKind.Object
            && Names(reasons, "widePaths") is { } wide
                ? wide
                : [];
}
