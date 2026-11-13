using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Participants;

// The records a local host keeps for the driver's helper and partner sessions, and the return of their answers to the session each
// serves. Every part of a request may be left out, and the desk refuses the blank naming which. A shared host maps none, and a caller
// off this machine is answered not found.

/// <summary>
/// The driver's helper session opens its record: a chat that serves the asker, in a clone of its own, as its adapter and account.
/// </summary>
public sealed record OpenHelper(
    string? Asker, string? Door, string? Tree, string? Account = null, string? DoorVersion = null, string? BaseCommit = null,
    string? ScoutIn = null)
    : IRoute<HelperActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/helpers";
}

/// <summary>
/// The driver opens a supervisor's turn's record: a chat on no quest marked with the attempt it decides for, at the subject's repository
/// or the machine's (no repository and no workspace), serving the session the subject waits on where it names one, bound to its role.
/// </summary>
public sealed record OpenSupervise(
    string? Decides, string? Role, string? Repository, string? Workspace, string? Serves, string? Door, string? Tree,
    string? Account = null, string? DoorVersion = null, bool? Machine = null)
    : IRoute<HelperActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/supervise";
}

/// <summary>The driver opens a step's session's record: a chat on no quest at the run's place, marked with the attempt it performs.</summary>
public sealed record OpenStepSession(
    string? Performs, string? Repository, string? Workspace, string? Door, string? Account = null, string? DoorVersion = null)
    : IRoute<HelperActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/step-sessions";
}

/// <summary>A helper's answer, handed to the session it serves as a word marked as the helper's, once.</summary>
/// <param name="Text">Daoris's words around the helper's claim.</param>
/// <param name="Running">The driver that runs a working driven asker hands an urgent ask's answer for its run's next step.</param>
/// <param name="Level">The ask's level, kept beside the word.</param>
/// <param name="Settled">True asks to be told a key the line settled.</param>
public sealed record HandHelper(string Helper, string? Text, bool? Running = null, string? Level = null, bool? Settled = null)
    : IRoute<HelperActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/helpers/{helper}/hand";
}

/// <summary>
/// The driver opens a partner's record for one piece, idempotent by the piece's id: a chat that serves the lead and carries the piece,
/// in a clone of its own with an entry of its own.
/// </summary>
public sealed record OpenPartner(
    string? Lead, string? Piece, string? Door, string? Tree, string? Account = null, string? DoorVersion = null, string? BaseCommit = null)
    : IRoute<HelperActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/partners";
}

/// <summary>A piece's answer, handed to its lead's line: ordinary, under the piece's key, never urgent.</summary>
public sealed record HandPartner(string Partner, string? Text, bool? Settled = null) : IRoute<HelperActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/partners/{partner}/hand";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpenHelper))]
[JsonSerializable(typeof(HandHelper))]
[JsonSerializable(typeof(OpenPartner))]
[JsonSerializable(typeof(OpenSupervise))]
[JsonSerializable(typeof(OpenStepSession))]
[JsonSerializable(typeof(HandPartner))]
[JsonSerializable(typeof(HelperActionResponse))]
internal sealed partial class ParticipantJson : JsonSerializerContext;
