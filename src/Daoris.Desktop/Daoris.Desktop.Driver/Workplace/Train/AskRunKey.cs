using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The delivery run of an ask in one repository: keyed by the ask and the repository, never by a quest's chain, and bound to the ask's
/// branch (<see cref="TrainBranch"/>) where a chain's run is bound to its session's. It is a run like any other, on the one runtime.
/// </summary>
public static class AskRunKey
{
    public const string Prefix = "ask-";

    /// <summary>The run's id for <paramref name="ask"/> in <paramref name="repository"/>; null where the pair names no run's file.</summary>
    public static string? Of(string ask, string repository)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(repository.ToLowerInvariant())))[..8];
        var key = $"{Prefix}{ask.TrimStart('#')}-{digest}";
        return WorkflowRunBindings.IsRunId(key) ? key : null;
    }

    /// <summary>Whether <paramref name="binding"/> is an ask's delivery run: its id is the one its ask and repository name.</summary>
    public static bool Is(WorkflowRunBinding binding) =>
        binding.Ask is { Length: > 0 } ask && string.Equals(binding.Run, Of(ask, binding.Repository), StringComparison.Ordinal);

    /// <summary>The unit standing for the ask's branch in <paramref name="run"/>, which no session made.</summary>
    public static string BranchUnit(string run) => $"unit-{run}";

    /// <summary>Whether <paramref name="unit"/> is such a branch: a session's branch unit never begins with the prefix of an ask's run.</summary>
    public static bool IsBranchUnit(string unit) => unit.StartsWith($"unit-{Prefix}", StringComparison.Ordinal);
}
