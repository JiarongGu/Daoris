using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>What a route's handler answers: its answer at 200, or a <see cref="Refusal"/> at its status.</summary>
public sealed class Answer<T>
{
    private Answer(int status, T? value, ErrorResponse? refusal) => (Status, Value, Refused) = (status, value, refusal);

    public int Status { get; }

    public T? Value { get; }

    public ErrorResponse? Refused { get; }

    public static implicit operator Answer<T>(T value) => new(StatusCodes.Status200OK, value, null);

    public static implicit operator Answer<T>(Refusal refusal) => new(refusal.Status, default, refusal.Body);

    /// <summary>Written as the framework's results wrote one: the status, then the JSON with its content type.</summary>
    internal Task WriteAsync(HttpContext http, JsonSerializerOptions json)
    {
        http.Response.StatusCode = Status;
        return Refused is { } refused
            ? http.Response.WriteAsJsonAsync(refused, (JsonTypeInfo<ErrorResponse>)json.GetTypeInfo(typeof(ErrorResponse)), cancellationToken: http.RequestAborted)
            : http.Response.WriteAsJsonAsync(Value!, (JsonTypeInfo<T>)json.GetTypeInfo(typeof(T)), cancellationToken: http.RequestAborted);
    }
}

/// <summary>A refusal in the house's shape, which every route's <see cref="Answer{T}"/> takes.</summary>
public readonly record struct Refusal(int Status, ErrorResponse Body)
{
    public static Refusal BadRequest(string error, string? code = null) => new(StatusCodes.Status400BadRequest, new ErrorResponse(error, code));

    public static Refusal NotFound(string error, string? code = null) => new(StatusCodes.Status404NotFound, new ErrorResponse(error, code));
}
