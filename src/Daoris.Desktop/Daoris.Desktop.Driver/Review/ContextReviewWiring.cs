using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>What Context asks of the review, installed when the driver loads: the gates a brief reads a check by (<see cref="IContextReview"/>).</summary>
internal sealed class ContextReviewOperations : IContextReview
{
    [ModuleInitializer]
    internal static void Install() => ContextReview.Current = new ContextReviewOperations();

    public string GatesFile => HeavyGates.FileName;

    public IReadOnlyList<HeavyGate> DeclaredGates(string checkout) => HeavyGates.Read(checkout);

    public IReadOnlyList<HeavyGate>? MappedGates(DriverConfig config, string repository) => HeavyMapping.For(config, repository);
}
