using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The update's refusal of a going back, read of the deliveries (<see cref="InstallRollback"/>).</summary>
internal sealed class DeliveryGoingBack : IGoingBackRefusal
{
    [ModuleInitializer]
    internal static void Install() => GoingBackRefusals.Current = new DeliveryGoingBack();

    public (string Code, string Sentence)? Refused(string home, string? staged, string? running) => InstallRollback.Refused(home, staged, running);
}
