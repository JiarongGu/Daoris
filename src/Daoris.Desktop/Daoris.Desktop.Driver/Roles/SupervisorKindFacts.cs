using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The facts of the kinds whose subjects are not a question or a referred point (landing, allow, files): each kind's row reads its own
/// subject as Daoris holds it and hands the parts here, to be fenced as <see cref="SupervisorFacts"/>'s are, with the line's course and
/// commits after them where the subject belongs to a session, so an agent supervisor is handed what a terminal's print holds.
/// </summary>
public static class SupervisorKindFacts
{
    /// <summary>
    /// <paramref name="parts"/>, each a label and its text, fenced under <paramref name="fence"/>, then the course and the commits of
    /// <paramref name="session"/> where it names a session whose record reads.
    /// </summary>
    public static async Task<string> ComposeAsync(
        ServiceClient service, string home, string session, string fence, IReadOnlyList<(string Label, string Text)> parts, CancellationToken ct)
    {
        var print = new StringBuilder();
        foreach (var (label, text) in parts) SessionsSuperviseCommand.Part(print, fence, label, text);
        if (session.Length > 0 && await service.RecordAsync(session, ct).ConfigureAwait(false) is { } record)
        {
            await SupervisorFacts.CourseAsync(service, home, session, record, fence, print, ct).ConfigureAwait(false);
        }

        return print.ToString();
    }
}
