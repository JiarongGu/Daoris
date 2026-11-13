namespace Daoris.Knowledge;

/// <summary>A deployable named in a plan: its id is unique only inside its repository.</summary>
public sealed record DeliveryRef(string Repository, string Id)
{
    public override string ToString() => $"{Repository}/{Id}";
}
