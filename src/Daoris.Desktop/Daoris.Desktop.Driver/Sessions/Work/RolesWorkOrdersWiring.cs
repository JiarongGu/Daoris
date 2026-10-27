using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class RolesWorkOrdersOperations : IRolesWorkOrders
{
    [ModuleInitializer]
    internal static void Install() => RolesWorkOrders.Current = new RolesWorkOrdersOperations();

    public ParkQuestion? QuestionOf(string home, string session) => new QuestionFiles(home).Read(session);

    public async Task<SupervisorAskWords> AskWordsAsync(ServiceClient service, string? from, string questId, CancellationToken ct)
    {
        var words = await AskWords.ReadAsync(service, from, ct).ConfigureAwait(false);
        var goAheads = words?.GoAheads is { } held
            ? string.Join("\n", held.Select(each => $"#{each.Number} {each.Kind} on {each.On}: {each.Act} — "
                + (each.Answer is null ? "open" : each.Answer.Approved ? "approved" : "refused")))
            : "unread";
        return new SupervisorAskWords(goAheads, AskWordsText.Beneath(words, questId));
    }
}
