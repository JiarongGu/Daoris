using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The course's read of a line's facts, installed when the driver loads (<see cref="ILineFactsReads"/>).</summary>
internal sealed class LineFactsOperations : ILineFactsReads
{
    [ModuleInitializer]
    internal static void Install() => LineFactsReads.Current = new LineFactsOperations();

    public LineFacts Of(string home, string session, string? recordsJson) => LineCountsRead.FactsOf(home, session, recordsJson);

    public Task<LineFacts> OfAsync(ServiceClient service, string home, string session, CancellationToken ct) =>
        LineCountsRead.FactsAsync(service, home, session, ct);
}
