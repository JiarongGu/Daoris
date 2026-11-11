namespace Daoris.Desktop;

/// <summary>
/// A route that is a look in one form and a press in another: where the payload's <paramref name="field"/> holds one of
/// <paramref name="values"/> (each written as JSON), it is <paramref name="class"/>; every other form takes its mark's class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
internal sealed class ClassedByAttribute(string route, string field, PipeClass @class, params string[] values) : Attribute
{
    public string Route { get; } = route;

    public string Field { get; } = field;

    public PipeClass Class { get; } = @class;

    public string[] Values { get; } = values;

    /// <summary>Fields that, said beside <see cref="Field"/>, make the payload no form it names, since the handler reads them first.</summary>
    public string[] Unless { get; set; } = [];
}

/// <summary>
/// What a person's or a window's route marks <c>owner-set</c>: the payload's fields that name what the owner acts on, by their kind,
/// or <see cref="None"/> with its closed reason (<see cref="OwnerNone"/>). A route naming two fields of one
/// kind says the mark twice.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
internal sealed class OwnerAttribute(string route) : Attribute
{
    public string Route { get; } = route;

    public string? Session { get; set; }

    /// <summary>A field holding a list of sessions.</summary>
    public string? Sessions { get; set; }

    public string? Ask { get; set; }

    public string? Entry { get; set; }

    public string? Unit { get; set; }

    public string? Repository { get; set; }

    /// <summary>A field holding a list of repositories.</summary>
    public string? Repositories { get; set; }

    public string? Workspace { get; set; }

    public string? Quest { get; set; }

    public string? None { get; set; }

    public IEnumerable<OwnerField> Fields()
    {
        if (Session is not null) yield return new(Session, OwnerKinds.Session);
        if (Sessions is not null) yield return new(Sessions, OwnerKinds.Session, Many: true);
        if (Ask is not null) yield return new(Ask, OwnerKinds.Ask);
        if (Entry is not null) yield return new(Entry, OwnerKinds.Entry);
        if (Unit is not null) yield return new(Unit, OwnerKinds.Unit);
        if (Repository is not null) yield return new(Repository, OwnerKinds.Repository);
        if (Repositories is not null) yield return new(Repositories, OwnerKinds.Repository, Many: true);
        if (Workspace is not null) yield return new(Workspace, OwnerKinds.Workspace);
        if (Quest is not null) yield return new(Quest, OwnerKinds.Quest);
    }
}

/// <summary>
/// A person's route whose <c>setting</c> ground names no value it sets, so it is never <c>already</c>: an edit merged into a rule, an
/// act beside the value, a list edited in place, or a key the read redacts. <paramref name="why"/> says which.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
internal sealed class NotAlreadyAttribute(string route, string why) : Attribute
{
    public string Route { get; } = route;

    public string Why { get; } = why;
}

/// <summary>A module that is a window's whole (its frame, its shells, its browser, its log): refused over the pipe, and naming nothing a role acts on.</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class WindowModuleAttribute : Attribute;

/// <summary>
/// The class of one route of a module that answers by a switch rather than by marked handlers, on its class: what
/// <see cref="DriverRouteAttribute"/> says for the driver's. A route such a module does not name is the person's.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal sealed class ModuleRouteAttribute(string route, PipeClass @class) : Attribute
{
    private PipeDrain? _drain;

    public string Route { get; } = route;

    public PipeClass Class { get; } = @class;

    /// <summary>What an update's drain does with it: said for a person's or a role's route, and only for one.</summary>
    public PipeDrain Drain { get => _drain ?? PipeDrain.Waits; set => _drain = value; }

    public PipeDrain? DrainSaid => _drain;
}
