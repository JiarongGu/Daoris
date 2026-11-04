namespace Daoris.Contracts;

/// <summary>
/// A route of the service's HTTP API, declared once as its request record: the method, the path, and the answer's type. The
/// host binds a request into the record, the driver sends the record, and the page's types are written from it, so no one
/// writes the path or a wire name a second time.
/// </summary>
/// <remarks>
/// A property whose wire name a <c>{name}</c> segment of <see cref="Path"/> names travels in the path. The rest travel in the
/// query of a GET or DELETE, and in the body otherwise. A property that may be left out is nullable or has a default; one that
/// is neither is required, and a request without it is refused as the framework's binding refused one, 400 with no body.
/// </remarks>
/// <typeparam name="TAnswer">What a success answers; a refusal answers <see cref="ErrorResponse"/>.</typeparam>
public interface IRoute<TAnswer>
{
    /// <summary>The HTTP method, upper case.</summary>
    static abstract string Method { get; }

    /// <summary>The path's template, as the host maps it.</summary>
    static abstract string Path { get; }
}
