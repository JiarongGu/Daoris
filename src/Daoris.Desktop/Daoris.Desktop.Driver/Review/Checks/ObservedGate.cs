namespace Daoris.Driver;

/// <summary>The repository's gate named by Daoris's hand-back reading, regardless of its heavy mark.</summary>
internal static class ObservedGate
{
    internal static HeavyChoice Choose(string checkout, string? name)
    {
        if (name is null) return new HeavyChoice([], "an observed check needs a gate's name.", 2);
        var declared = HeavyGates.Read(checkout);
        return declared.FirstOrDefault(gate => gate.Name == name) is { } gate
            ? new HeavyChoice([gate], null, 0)
            : new HeavyChoice([], $"{DeclarationNames.Gates} declares no gate `{name}`; its gates: {string.Join(", ", declared.Select(each => each.Name))}.", 2);
    }
}
