using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class ParticipantsWorkOrdersOperations : IParticipantsWorkOrders
{
    [ModuleInitializer]
    internal static void Install() => ParticipantsWorkOrders.Current = new ParticipantsWorkOrdersOperations();

    public IReadOnlyList<string> ConnectorTools => BrainstormPermissions.ConnectorTools;
}
