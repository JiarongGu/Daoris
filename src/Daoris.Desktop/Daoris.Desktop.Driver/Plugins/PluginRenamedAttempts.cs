namespace Daoris.Driver;

/// <summary>
/// A renamed plugin's attempts still in flight, carried to its new id: each attempt not settled that names the old id names the new
/// one, its step's kind with it (<see cref="PluginFormerKinds.KindOf"/>), so the work process now running asks after it at its next look,
/// the generation that took it no longer current. A settled attempt is history and stays as written.
/// </summary>
/// <remarks>Written only by the process holding the home's lease (<see cref="NodeRecordFiles.Edit{T}"/>); a terminal's rename alone
/// carries nothing, and the next loop's start does, since every start moves what the installed manifests still declare.</remarks>
internal static class PluginRenamedAttempts
{
    /// <returns>True where it carried any attempt; null where none named the old id or nothing could be written.</returns>
    public static bool? Carry(string home, string former, string now)
    {
        var records = new NodeRecordFiles(home);
        var formerly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [former] = now };
        bool Named(NodeAttempt attempt) =>
            attempt.State != AttemptStates.Settled && string.Equals(attempt.Plugin, former, StringComparison.OrdinalIgnoreCase);

        var carried = 0;
        foreach (var record in records.All().Records.Where(record => record.Attempts.Any(Named)))
        {
            try
            {
                var (refused, count) = records.Edit(record.Unit, current =>
                {
                    var named = current.Attempts.Count(Named);
                    if (named == 0) return ((NodeRecord?)null, 0);
                    return (current with
                    {
                        Attempts = [.. current.Attempts.Select(attempt => Named(attempt)
                            ? attempt with { Plugin = now, Call = attempt.Call with { Step = PluginFormerKinds.KindOf(attempt.Call.Step, formerly) } }
                            : attempt)],
                    }, named);
                });
                if (refused is null) carried += count;
            }
            catch (IOException)
            {
                // Its record's lock held by another edit throughout: carried at the next start, which moves what is still declared.
            }
        }

        return carried > 0 ? true : null;
    }
}
