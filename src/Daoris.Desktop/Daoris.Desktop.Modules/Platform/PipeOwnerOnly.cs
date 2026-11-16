using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The owner's records, which no agent sets (D305, D306): a place's risk and marks. A press of one that proves a session's key (Ask Daoris's
/// in full access included) goes on the owner's card in every mode, so it runs only on the owner's yes; a process in an agents' job that
/// proves none is refused; the window and the owner's own terminal set it.
/// </summary>
public static class PipeOwnerOnly
{
    /// <summary>The refusal's code for an owner's record pressed from a process in an agents' job.</summary>
    public const string Refused = "owner-only";

    public const string RefusedSentence =
        "This is the owner's to set: a place's risk and marks, and what agents may do, are widened by the owner alone. An agent's press goes on the owner's card through its own session's key. Nothing was changed.";

    /// <summary>Whether <paramref name="request"/> sets one of the owner's records.</summary>
    public static bool Governs(IpcRequest request) =>
        string.Equals(request.Module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) && request.Type == "SET_PLACE_RISK";
}
