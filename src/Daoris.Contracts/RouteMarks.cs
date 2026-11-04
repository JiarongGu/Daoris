namespace Daoris.Contracts;

/// <summary>
/// A route whose body may be left out: an absent or <c>null</c> body binds as one naming nothing. Without it, a route that reads
/// a body refuses a request with none, 400 with no body, as the framework refused a required body parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OptionalBodyAttribute : Attribute;

/// <summary>
/// A route whose handler reads its own body, as the record's body values are documentation of it and the binder reads none: a body
/// read no further than its limit, or taken as the text a wire reader judges whole.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OwnBodyAttribute : Attribute;

/// <summary>A value of a route that takes a body which travels in the query instead, as a handler's simple parameter did.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class FromQueryAttribute : Attribute;

/// <summary>
/// The page's type for a value, as TypeScript spells it, where the wire's own type says less than the host keeps to: the words a
/// status takes, or the shape of a part the host writes as JSON text.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class PageTypeAttribute(string typeScript) : Attribute
{
    public string TypeScript { get; } = typeScript;
}

/// <summary>Where one of a route's values travels.</summary>
public enum RouteTravel
{
    Path,
    Query,
    Body,
}
