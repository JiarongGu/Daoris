using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>What the intake's room asks of Setup, installed when the driver loads (<see cref="IWorkOrdersSetup"/>).</summary>
internal sealed class WorkOrdersSetupOperations : IWorkOrdersSetup
{
    [ModuleInitializer]
    internal static void Install() => WorkOrdersSetup.Current = new WorkOrdersSetupOperations();

    public IIntakeDescription? Describe(string root) => SelfDescriptionFiles.Read(root);
}
