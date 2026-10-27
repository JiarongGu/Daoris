namespace Daoris.Driver;

/// <summary>The supplied facts used to read what Daoris left in the person's tool home.</summary>
public interface IToolHomeWorld
{
    string? Home { get; }
    Func<string, string?> Process { get; }
    Func<string, string?>? Account { get; }
    string? ToolHome { get; }
    string ToolHomeFrom { get; }
    string? ToolHomeWhy { get; }
    IReadOnlyList<ToolOrigin>? ToolOrigins { get; }
}
