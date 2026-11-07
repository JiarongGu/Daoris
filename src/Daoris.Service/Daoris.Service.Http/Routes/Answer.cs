using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>What a route's handler answers: its answer at 200, or a <see cref="Refusal"/> at its status.</summary>
public sealed class Answer<T>
{
    private Answer(int status, T? value, object? refused, bool bare) => (Status, Value, Refused, _bare) = (status, value, refused, bare);

    private readonly bool _bare;

    public int Status { get; }

    public T? Value { get; }

    /// <summary>The body a refusal answers: the house's <see cref="ErrorResponse"/>, or the route's own refusal record.</summary>
    public object? Refused { get; }

    public static implicit operator Answer<T>(T value) => new(StatusCodes.Status200OK, value, null, false);

    public static implicit operator Answer<T>(Refusal refusal) => new(refusal.Status, default, refusal.Body, false);

    /// <summary>
    /// A status the route answers in a shape of its own: a refusal record that carries a word beside its sentence, or an answer a
    /// state conflict still wears. A null <paramref name="body"/> is a bare status with nothing written, as a bare 404 is.
    /// </summary>
    public static Answer<T> Else(int status, object? body) => new(status, default, body, body is null);

    /// <summary>Written as the framework's results wrote one: the status, then the JSON with its content type.</summary>
    internal Task WriteAsync(HttpContext http, JsonSerializerOptions json)
    {
        http.Response.StatusCode = Status;
        if (_bare) return Task.CompletedTask;

        return Refused is { } refused
            ? http.Response.WriteAsJsonAsync(refused, json.GetTypeInfo(refused.GetType()), cancellationToken: http.RequestAborted)
            : http.Response.WriteAsJsonAsync(Value!, (JsonTypeInfo<T>)json.GetTypeInfo(typeof(T)), cancellationToken: http.RequestAborted);
    }
}

/// <summary>A refusal in the house's shape, which every route's <see cref="Answer{T}"/> takes.</summary>
public readonly record struct Refusal(int Status, ErrorResponse Body)
{
    public static Refusal BadRequest(string error, string? code = null) => new(StatusCodes.Status400BadRequest, new ErrorResponse(error, code));

    public static Refusal NotFound(string error, string? code = null) => new(StatusCodes.Status404NotFound, new ErrorResponse(error, code));

    /// <summary>A state the request found the record in, the lock's own shape: the losing side of a race reads it as someone got there first.</summary>
    public static Refusal Conflict(string error, string? code = null) => new(StatusCodes.Status409Conflict, new ErrorResponse(error, code));

    /// <summary>Written as a handler that answers <see cref="IResult"/> writes one.</summary>
    public IResult Result() => Results.Json(Body, statusCode: Status);
}
