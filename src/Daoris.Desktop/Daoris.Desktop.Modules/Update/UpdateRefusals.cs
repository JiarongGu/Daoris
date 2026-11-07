using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The refusals the updater keeps, each for its build and the person's word it was refused under (<see cref="InstallUpdate"/>): a look
/// says a kept one again and checks nothing, and a new word, a press of the screen's or the terminal's, drops it, so the build is checked
/// again, since what refused it (an unresolved delivery, a launcher that would not start) may have moved. Not thread-safe: the updater
/// holds its gate.
/// </summary>
internal sealed class UpdateRefusals
{
    private readonly Dictionary<string, (UpdateProblem Problem, UpdateRequest? Asked)> _kept = new(StringComparer.Ordinal);

    /// <summary>The refusal kept for <paramref name="build"/> under <paramref name="asked"/>; null, and none kept, where the word has moved since.</summary>
    public UpdateProblem? For(string build, UpdateRequest? asked)
    {
        if (!_kept.TryGetValue(build, out var kept)) return null;
        if (kept.Asked == asked) return kept.Problem;
        _kept.Remove(build);
        return null;
    }

    /// <summary>Keep <paramref name="problem"/> for <paramref name="build"/>, refused under <paramref name="asked"/>.</summary>
    public void Keep(string build, UpdateRequest? asked, UpdateProblem problem) => _kept[build] = (problem, asked);
}
