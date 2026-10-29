namespace Daoris.Driver;

/// <summary>
/// The owner's words and go-aheads of an ask, as the supervisor's print says them: the go-aheads' lines (<c>unread</c> where the ask's record
/// did not read) and the owner's own words beneath the quest.
/// </summary>
internal sealed record SupervisorAskWords(string GoAheads, string Beneath);

/// <summary>
/// What the roles ask of Work orders, which keeps the question files and the ask's record and sits above Roles (FOUNDATION1 mod-xi, D212):
/// the supervisor's print reads the session's question and the ask's words. Work orders implements this and installs it when the driver loads
/// (<c>WorkOrders/Work/RolesWorkOrdersWiring.cs</c>); each operation does exactly what its caller did before it was a port.
/// </summary>
internal interface IRolesWorkOrders
{
    /// <summary>The session's question as its file holds it, or null (see <see cref="QuestionFiles.Read"/>).</summary>
    ParkQuestion? QuestionOf(string home, string session);

    /// <summary>
    /// The ask <paramref name="from"/> names, read through <paramref name="service"/> as the print reads it (see <c>AskWords.ReadAsync</c>), its
    /// go-aheads and its owner's words beneath the quest <paramref name="questId"/>.
    /// </summary>
    Task<SupervisorAskWords> AskWordsAsync(ServiceClient service, string? from, string questId, CancellationToken ct);
}

internal static class RolesWorkOrders
{
    private static IRolesWorkOrders? _current;

    internal static IRolesWorkOrders Current
    {
        get => _current ?? throw new InvalidOperationException("no roles work-orders operations are installed: Work orders installs them when the driver loads");
        set => _current = value;
    }
}
