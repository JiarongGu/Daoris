using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The repository's own reached runner, read from its accepted declaration's <c>reached</c> entry: the command that runs only what a change
/// reaches (its <c>{base}</c> is the ref the change is judged against), its folder, whether it is clock-bound, and the marker of the last
/// line it prints, whose JSON says the classes run, replayed, flaked and red. A repository declaring none has its gates run whole.
/// </summary>
internal sealed record ReachedRunner(string Run, string? Cwd, bool Clock, string Outcome)
{
    /// <summary>The marker a runner's outcome line starts with where its declaration names none.</summary>
    internal const string DefaultOutcome = "reached-classes: ";

    /// <summary>The declaration's reached runner; null where it declares no command or its text does not read.</summary>
    internal static ReachedRunner? Of(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("reached", out var entry) || entry.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? Text(string name) => entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
            return Text("run") is { } run
                ? new ReachedRunner(run.Trim(), Text("cwd")?.Trim(), entry.TryGetProperty("clock", out var clock) && clock.ValueKind == JsonValueKind.True, Text("outcome") ?? DefaultOutcome)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The command with its <c>{base}</c> given <paramref name="baseRef"/>; null where the ref is not a plain one, which is never put in a command.</summary>
    internal string? CommandFor(string baseRef) => PlainRef.IsMatch(baseRef) ? Run.Replace("{base}", baseRef, StringComparison.Ordinal) : null;

    private static readonly Regex PlainRef = new(@"^[A-Za-z0-9][A-Za-z0-9._/-]*$", RegexOptions.CultureInvariant);
}
