using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts;

/// <summary>Every refusal's shape: the sentence a caller says, and where a refusal has one, a word a client reads instead of it.</summary>
/// <param name="Error">The sentence, verbatim.</param>
/// <param name="Code">A word for the refusal where it has one; absent elsewhere, so a reader that knows none reads the sentence.</param>
public sealed record ErrorResponse(string Error, string? Code = null);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class RefusalJson : JsonSerializerContext;
