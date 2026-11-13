namespace Daoris.Knowledge;

/// <summary>A resource a deployable provides or consumes, and how: <c>owns</c> or <c>migrates</c> provided; <c>mirror</c>, <c>calls</c> or <c>reads</c> consumed.</summary>
public sealed record DeliveryUse(string Resource, string Mode)
{
    /// <summary>Provided: the resource's shape.</summary>
    public const string Owns = "owns";

    /// <summary>Provided: the resource's schema changes.</summary>
    public const string Migrates = "migrates";

    /// <summary>Consumed: maps a schema without migrating it.</summary>
    public const string Mirror = "mirror";

    /// <summary>Consumed: an endpoint, which names a neighbour and proves no order.</summary>
    public const string Calls = "calls";

    /// <summary>Consumed: a table, a cache key or a flag.</summary>
    public const string Reads = "reads";
}
